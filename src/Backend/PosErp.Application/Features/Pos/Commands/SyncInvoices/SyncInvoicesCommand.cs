using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Crm.Services;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Crm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands.SyncInvoices;

public record SyncInvoicesCommand(List<OfflineInvoiceDto> Invoices) : IRequest<SyncResult>;
public record SyncResult(int Synced, int Failed, List<string> Errors);

public record OfflineInvoiceDto(
    Guid Id,
    DateTime BusinessDate,
    string InvoiceNumber,
    Guid TerminalId,
    int TerminalSequence,
    Guid CashierId,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal RoundOff,
    decimal NetPayable,
    string PaymentMode,
    List<OfflineInvoiceItemDto> Items,
    Guid? CustomerId = null,
    string? CustomerName = null,
    string? CustomerPhone = null,
    decimal CashAmount = 0m,
    decimal UpiAmount = 0m,
    decimal CardAmount = 0m,
    decimal WalletAmountUsed = 0m,
    decimal PointsRedeemed = 0m,
    int? LoyaltyPointsEarned = null,
    int? LoyaltyPointsBalance = null
);

public record OfflineInvoiceItemDto(
    Guid Id,
    Guid ProductId,
    string? Barcode,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal CgstRate,
    decimal CgstAmount,
    decimal SgstRate,
    decimal SgstAmount,
    decimal CessRate,
    decimal CessAmount,
    decimal TotalAmount
);

public class SyncInvoicesCommandHandler : IRequestHandler<SyncInvoicesCommand, SyncResult>
{
    private readonly IApplicationDbContext _context;
    private readonly PosErp.Application.Features.Offers.Services.IOfferEngine _offerEngine;
    private readonly PosErp.Application.Features.Finance.Services.IFinancialPostingService _financialPostingService;
    private readonly IAccountResolutionService _accountResolutionService;
    private readonly PosErp.Application.Features.Inventory.Services.IStockLedgerService _stockLedgerService;
    private readonly IWalletService _walletService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly ILogger<SyncInvoicesCommandHandler>? _logger;

    public SyncInvoicesCommandHandler(
        IApplicationDbContext context, 
        PosErp.Application.Features.Offers.Services.IOfferEngine offerEngine,
        PosErp.Application.Features.Finance.Services.IFinancialPostingService financialPostingService,
        IAccountResolutionService accountResolutionService,
        PosErp.Application.Features.Inventory.Services.IStockLedgerService stockLedgerService,
        IWalletService walletService,
        ILoyaltyService loyaltyService,
        ILogger<SyncInvoicesCommandHandler>? logger = null)
    {
        _context = context;
        _offerEngine = offerEngine;
        _financialPostingService = financialPostingService;
        _accountResolutionService = accountResolutionService;
        _stockLedgerService = stockLedgerService;
        _walletService = walletService;
        _loyaltyService = loyaltyService;
        _logger = logger;
    }

    public async Task<SyncResult> Handle(SyncInvoicesCommand request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        int synced = 0;
        int failed = 0;

        foreach (var dto in request.Invoices)
        {
            var strategy = ((DbContext)_context).Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    // 1. Idempotency Check: if exact invoice ID is already saved, safe to skip
                    var existsById = await _context.Invoices.AnyAsync(i => i.Id == dto.Id, cancellationToken);
                    if (existsById)
                    {
                        synced++;
                        return; 
                    }

                    // 2. Sequence Collision Check: same terminal + sequence + date but DIFFERENT Id
                    var existsByComposite = await _context.Invoices.AnyAsync(i => 
                        i.TerminalId == dto.TerminalId && 
                        i.TerminalSequence == dto.TerminalSequence && 
                        i.BusinessDate.Date == dto.BusinessDate.Date,
                        cancellationToken);
                    
                    if (existsByComposite)
                    {
                        failed++;
                        errors.Add($"SEQUENCE_COLLISION: Offline invoice {dto.InvoiceNumber} (ID: {dto.Id}) collides with existing sequence {dto.TerminalSequence} for terminal {dto.TerminalId} on {dto.BusinessDate.Date:yyyy-MM-dd} but has a different ID — possible client clock drift, needs manual review.");
                        return;
                    }

                    // ... Invoice mapping same as before ...
                    var invoice = new Invoice {
                        Id = dto.Id, BusinessDate = dto.BusinessDate, InvoiceNumber = dto.InvoiceNumber,
                        TerminalId = dto.TerminalId, TerminalSequence = dto.TerminalSequence, CashierId = dto.CashierId,
                        SubTotal = dto.SubTotal, DiscountAmount = dto.DiscountAmount, TaxAmount = dto.TaxAmount,
                        TotalAmount = dto.TotalAmount, RoundOff = dto.RoundOff, NetPayable = dto.NetPayable,
                        PaymentMode = dto.PaymentMode, Status = "COMPLETED", StoreId = Guid.Empty,
                        CustomerId = dto.CustomerId,
                        CashAmount = dto.CashAmount,
                        UpiAmount = dto.UpiAmount,
                        CardAmount = dto.CardAmount,
                        WalletAmount = dto.WalletAmountUsed
                    };

                    // Fetch products info to compute COGS and record movements
                    var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();
                    var productsInfo = await _context.Products
                        .Where(p => productIds.Contains(p.Id))
                        .ToDictionaryAsync(p => p.Id, cancellationToken);

                    foreach (var itemDto in dto.Items)
                    {
                        // Resolve Batch Id for FIFO/Expiry selection
                        Guid? selectedBatchId = null;
                        DateTime? expiryDate = null;
                        var batchStocks = new List<(Guid Id, DateTime? ExpiryDate, decimal Stock)>();

                        var activeBatches = await _context.ProductBatches
                            .Where(b => b.ProductId == itemDto.ProductId && b.IsActive)
                            .ToListAsync(cancellationToken);

                        if (activeBatches.Any())
                        {
                            foreach (var b in activeBatches)
                            {
                                var stock = await _context.StockLedger
                                    .Where(sl => sl.ProductId == itemDto.ProductId && sl.BatchId == b.Id)
                                    .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;
                                batchStocks.Add((b.Id, b.ExpiryDate, stock));
                            }

                            // 1. Prefer batch with positive stock and earliest expiry
                            var bestBatchId = batchStocks
                                .Where(x => x.Stock > 0)
                                .OrderBy(x => x.ExpiryDate.HasValue ? 0 : 1)
                                .ThenBy(x => x.ExpiryDate)
                                .Select(x => (Guid?)x.Id)
                                .FirstOrDefault();

                            // 2. If no batch has positive stock, fall back to any active batch ordered by expiry
                            if (bestBatchId == null)
                            {
                                bestBatchId = batchStocks
                                    .OrderBy(x => x.ExpiryDate.HasValue ? 0 : 1)
                                    .ThenBy(x => x.ExpiryDate)
                                    .Select(x => (Guid?)x.Id)
                                    .FirstOrDefault();
                            }

                            selectedBatchId = bestBatchId;
                        }

                        if (selectedBatchId.HasValue)
                        {
                            var selectedBatch = await _context.ProductBatches.FindAsync(new object[] { selectedBatchId.Value }, cancellationToken);
                            if (selectedBatch != null)
                            {
                                expiryDate = selectedBatch.ExpiryDate;
                                selectedBatch.AvailableQuantity -= itemDto.Quantity;
                            }
                        }

                        // Record stock ledger movement under "SALE_OFFLINE_FORCED" type
                        decimal unitCost = productsInfo.TryGetValue(itemDto.ProductId, out var prod) ? prod.PurchasePrice : 0m;
                        
                        await _stockLedgerService.RecordMovementAsync(
                            storeId: Guid.Empty,
                            warehouseId: null,
                            terminalId: dto.TerminalId,
                            businessDate: dto.BusinessDate.Date,
                            productId: itemDto.ProductId,
                            batchId: selectedBatchId,
                            movementType: "SALE_OFFLINE_FORCED",
                            quantity: -itemDto.Quantity,
                            unitCost: unitCost,
                            expiryDate: expiryDate,
                            referenceDocId: dto.Id,
                            referenceNumber: dto.InvoiceNumber,
                            userId: dto.CashierId,
                            cancellationToken: cancellationToken
                        );

                        // Check if the resulting stock balance goes negative using pre-calculated in-memory values
                        decimal currentBatchStock = 0;
                        if (selectedBatchId.HasValue)
                        {
                            var batchInfo = batchStocks.FirstOrDefault(x => x.Id == selectedBatchId.Value);
                            currentBatchStock = batchInfo.Stock;
                        }
                        decimal newBalance = currentBatchStock - itemDto.Quantity;

                        if (newBalance < 0)
                        {
                            string productName = prod?.Name ?? itemDto.ProductName;
                            _logger?.LogError("STOCK_DISCREPANCY: Offline checkout for product {ProductId} ({ProductName}) forced stock balance negative to {NewBalance} on terminal {TerminalId} using movement type SALE_OFFLINE_FORCED. Manual audit required.", 
                                itemDto.ProductId, productName, newBalance, dto.TerminalId);
                        }

                        invoice.Items.Add(new InvoiceItem {
                            Id = itemDto.Id, BusinessDate = dto.BusinessDate, ProductId = itemDto.ProductId, Barcode = itemDto.Barcode,
                            ProductName = itemDto.ProductName, Quantity = itemDto.Quantity, UnitPrice = itemDto.UnitPrice,
                            DiscountAmount = itemDto.DiscountAmount, CgstRate = itemDto.CgstRate, CgstAmount = itemDto.CgstAmount,
                            SgstRate = itemDto.SgstRate, SgstAmount = itemDto.SgstAmount, CessRate = itemDto.CessRate,
                            CessAmount = itemDto.CessAmount, TotalAmount = itemDto.TotalAmount
                        });
                    }
                    _context.Invoices.Add(invoice);

                    // Resolve account codes dynamically
                    string cashAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("ASSET", "Cash", "10100", cancellationToken);
                    string digitalAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("ASSET", "Current", "10200", cancellationToken);
                    string salesAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("REVENUE", "Sales", "40100", cancellationToken);
                    string outputCgstAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("LIABILITY", "Output CGST", "22010", cancellationToken);
                    string outputSgstAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("LIABILITY", "Output SGST", "22020", cancellationToken);
                    string inventoryAssetAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("ASSET", "Inventory Asset", "10300", cancellationToken);
                    string cogsAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("EXPENSE", "Cost of Goods Sold", "50100", cancellationToken);
                    string walletAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("LIABILITY", "Wallet Liabilities", "20200", cancellationToken);
                    string loyaltyAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("LIABILITY", "Loyalty Points", "20300", cancellationToken);

                    // Compute points discount ONCE to share between GL debits and loyalty mutations
                    var loyaltyConfig = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync(cancellationToken) ?? new LoyaltyProgramConfig();
                    decimal pointsDiscount = 0m;
                    if (dto.PointsRedeemed > 0)
                    {
                        pointsDiscount = Math.Round((dto.PointsRedeemed / loyaltyConfig.RedeemRatioPoints) * loyaltyConfig.RedeemRatioDiscountAmount, 2, MidpointRounding.AwayFromZero);
                    }

                    // Process Wallet & Loyalty ledger updates if a customer is linked (Sub-Fix 37b)
                    if (dto.CustomerId.HasValue)
                    {
                        var customerId = dto.CustomerId.Value;

                        // FIRST STEP: Acquire FOR UPDATE row lock on customer to serialize concurrent updates
                        await ((DbContext)_context).Database.ExecuteSqlRawAsync(
                            "SELECT 1 FROM customers WHERE id = {0} FOR UPDATE", 
                            new object[] { customerId }, cancellationToken);

                        var customer = await _context.Customers.FindAsync(new object[] { customerId }, cancellationToken);
                        if (customer == null)
                        {
                            throw new Exception($"Customer with ID {customerId} not found.");
                        }

                        string finalInvoiceRef = dto.InvoiceNumber.StartsWith("INV-", StringComparison.OrdinalIgnoreCase)
                            ? dto.InvoiceNumber
                            : $"INV-{dto.InvoiceNumber}";

                        // Process Wallet Spend
                        if (dto.WalletAmountUsed > 0)
                        {
                            decimal currentWallet = customer.RunningWalletBalance;
                            decimal newWalletBalance = currentWallet - dto.WalletAmountUsed;
                            if (newWalletBalance < 0)
                            {
                                _logger?.LogWarning("WALLET_DISCREPANCY: Synced invoice {InvoiceNumber} spent {WalletAmountUsed} from customer {CustomerId} wallet, driving balance negative to {NewBalance}.", 
                                    dto.InvoiceNumber, dto.WalletAmountUsed, customerId, newWalletBalance);
                            }

                            // Record spend ledger transaction directly to allow negative forced sync
                            customer.RunningWalletBalance = newWalletBalance;
                            var walletLedger = new WalletLedgerEntry
                            {
                                CustomerId = customerId,
                                StoreId = Guid.Empty,
                                TransactionType = "SPEND",
                                Amount = -dto.WalletAmountUsed,
                                ReferenceDocument = finalInvoiceRef,
                                RunningBalance = newWalletBalance,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.WalletLedger.Add(walletLedger);
                        }

                        // Process Loyalty points redemption
                        if (dto.PointsRedeemed > 0)
                        {
                            if (customer.MembershipStatus == "Blocked" || customer.MembershipStatus == "Inactive")
                            {
                                throw new Exception("Customer account is not eligible for points redemption.");
                            }

                            decimal currentPts = customer.RunningLoyaltyPoints;
                            decimal newPtsBalance = currentPts - dto.PointsRedeemed;
                            if (newPtsBalance < 0)
                            {
                                _logger?.LogWarning("LOYALTY_BALANCE_DISCREPANCY: Synced invoice {InvoiceNumber} redeemed {PointsRedeemed} points from customer {CustomerId}, driving balance negative to {NewBalance}.", 
                                    dto.InvoiceNumber, dto.PointsRedeemed, customerId, newPtsBalance);
                            }

                            // Sum all points redeemed today via explicit join
                            decimal todayRedeemed = await (
                                from l in _context.LoyaltyLedger
                                join i in _context.Invoices on l.InvoiceId equals i.Id
                                where l.CustomerId == customer.Id 
                                   && l.TransactionType == "Redeem Points" 
                                   && i.BusinessDate == dto.BusinessDate.Date
                                select l.PointsRedeemed
                            ).SumAsync(cancellationToken);

                            // Check cumulative daily limit and log warning
                            if (todayRedeemed + dto.PointsRedeemed > loyaltyConfig.MaxRedemptionPerDay)
                            {
                                _logger?.LogWarning("LOYALTY_LIMIT_BREACH: Offline redemption of {Redeemed} points for customer {CustomerId} exceeded the daily limit of {Limit} on date {Date}. manual review required.", 
                                    dto.PointsRedeemed, customerId, loyaltyConfig.MaxRedemptionPerDay, dto.BusinessDate.Date.ToString("yyyy-MM-dd"));
                            }

                            customer.RunningLoyaltyPoints -= dto.PointsRedeemed;
                            customer.LastRedemptionDate = DateTime.UtcNow;

                            var ptsLedger = new LoyaltyLedgerEntry
                            {
                                CustomerId = customer.Id,
                                StoreId = Guid.Empty,
                                TransactionType = "Redeem Points",
                                PointsEarned = 0,
                                PointsRedeemed = dto.PointsRedeemed,
                                PreviousBalance = currentPts,
                                BalanceAfterTransaction = customer.RunningLoyaltyPoints,
                                Points = 0 - dto.PointsRedeemed,
                                RunningPoints = customer.RunningLoyaltyPoints,
                                InvoiceId = invoice.Id,
                                ReferenceDocument = finalInvoiceRef,
                                Remarks = $"Redeemed {dto.PointsRedeemed} points during checkout."
                            };
                            _context.LoyaltyLedger.Add(ptsLedger);
                        }

                        // Calculate and award points earned on cash-paid basis (dto.NetPayable)
                        await _loyaltyService.CalculateAndAwardPointsForInvoiceAsync(invoice.Id, customerId, dto.NetPayable, cancellationToken);

                        // Compare re-computed earned points vs client-submitted estimation for drift check
                        var earnedEntry = _context.LoyaltyLedger.Local
                            .FirstOrDefault(l => l.InvoiceId == invoice.Id && l.TransactionType == "Earn Points");

                        if (earnedEntry != null && dto.LoyaltyPointsEarned.HasValue)
                        {
                            if (earnedEntry.PointsEarned != dto.LoyaltyPointsEarned.Value)
                            {
                                _logger?.LogWarning("LOYALTY_DRIFT: Frontend estimated points earned ({FrontendEarned}) differed from server recalculated earned points ({ServerEarned}) for invoice {InvoiceNumber}.", 
                                    dto.LoyaltyPointsEarned.Value, earnedEntry.PointsEarned, dto.InvoiceNumber);
                            }
                        }
                    }

                    decimal totalCogs = 0;
                    foreach (var itemDto in dto.Items)
                    {
                        if (productsInfo.TryGetValue(itemDto.ProductId, out var product))
                        {
                            if (product.PurchasePrice > 0)
                            {
                                totalCogs += itemDto.Quantity * product.PurchasePrice;
                            }
                            else
                            {
                                _logger?.LogWarning("Skipping COGS calculation for Product {ProductId} ({ProductName}) because purchase price is 0 or less.", itemDto.ProductId, product.Name);
                            }
                        }
                    }

                    // Financial Double-Entry Posting for Sync
                    decimal totalCess = dto.Items.Sum(i => i.CessAmount);
                    decimal cgst = Math.Round((dto.TaxAmount - totalCess) / 2m, 2);
                    decimal sgst = dto.TaxAmount - totalCess - cgst;
                    decimal taxableValue = dto.TotalAmount - dto.TaxAmount;

                    var journalLines = new List<PosErp.Application.Features.Finance.Services.JournalLineDto>();
                    
                    decimal totalSplitTenders = dto.CashAmount + dto.UpiAmount + dto.CardAmount + dto.WalletAmountUsed;
                    if (totalSplitTenders == 0 && dto.NetPayable > 0)
                    {
                        if (dto.PaymentMode.Equals("Cash", StringComparison.OrdinalIgnoreCase))
                        {
                            journalLines.Add(new PosErp.Application.Features.Finance.Services.JournalLineDto 
                            { 
                                AccountCode = cashAccountCode, 
                                Description = "Cash Tender", 
                                Debit = dto.NetPayable, 
                                Credit = 0 
                            });
                        }
                        else
                        {
                            journalLines.Add(new PosErp.Application.Features.Finance.Services.JournalLineDto 
                            { 
                                AccountCode = digitalAccountCode, 
                                Description = "Digital Tender", 
                                Debit = dto.NetPayable, 
                                Credit = 0 
                            });
                        }
                    }
                    else
                    {
                        if (dto.CashAmount > 0)
                        {
                            journalLines.Add(new PosErp.Application.Features.Finance.Services.JournalLineDto 
                            { 
                                AccountCode = cashAccountCode, 
                                Description = "Cash Tender", 
                                Debit = dto.CashAmount, 
                                Credit = 0 
                            });
                        }

                        decimal digitalTender = dto.UpiAmount + dto.CardAmount;
                        if (digitalTender > 0)
                        {
                            journalLines.Add(new PosErp.Application.Features.Finance.Services.JournalLineDto 
                            { 
                                AccountCode = digitalAccountCode, 
                                Description = "Digital Tender", 
                                Debit = digitalTender, 
                                Credit = 0 
                            });
                        }

                        if (dto.WalletAmountUsed > 0)
                        {
                            journalLines.Add(new PosErp.Application.Features.Finance.Services.JournalLineDto 
                            { 
                                AccountCode = walletAccountCode, 
                                Description = "Wallet Tender Reconcile", 
                                Debit = dto.WalletAmountUsed, 
                                Credit = 0 
                            });
                        }
                    }

                    if (pointsDiscount > 0)
                    {
                        journalLines.Add(new PosErp.Application.Features.Finance.Services.JournalLineDto 
                        { 
                            AccountCode = loyaltyAccountCode, 
                            Description = "Loyalty Points Tender Reconcile", 
                            Debit = pointsDiscount, 
                            Credit = 0 
                        });
                    }

                    if (totalCogs > 0)
                    {
                        journalLines.Add(new PosErp.Application.Features.Finance.Services.JournalLineDto { AccountCode = cogsAccountCode, Description = "Cost of Goods Sold", Debit = totalCogs, Credit = 0 });
                        journalLines.Add(new PosErp.Application.Features.Finance.Services.JournalLineDto { AccountCode = inventoryAssetAccountCode, Description = "Inventory Asset Reduction", Debit = 0, Credit = totalCogs });
                    }

                    journalLines.Add(new PosErp.Application.Features.Finance.Services.JournalLineDto { AccountCode = salesAccountCode, Description = "Sales Revenue", Debit = 0, Credit = taxableValue });
                    
                    // For double entry posting, we split total tax (including Cess) between CGST/SGST to match system Chart of Accounts
                    decimal ledgerCgst = Math.Round(dto.TaxAmount / 2m, 2);
                    decimal ledgerSgst = dto.TaxAmount - ledgerCgst;
                    if (ledgerCgst > 0) journalLines.Add(new PosErp.Application.Features.Finance.Services.JournalLineDto { AccountCode = outputCgstAccountCode, Description = "Output CGST", Debit = 0, Credit = ledgerCgst });
                    if (ledgerSgst > 0) journalLines.Add(new PosErp.Application.Features.Finance.Services.JournalLineDto { AccountCode = outputSgstAccountCode, Description = "Output SGST", Debit = 0, Credit = ledgerSgst });

                    await _financialPostingService.PostJournalEntryAsync(
                        null, dto.BusinessDate.Date, $"Offline POS Invoice {dto.InvoiceNumber}", $"INV-{dto.Id}", journalLines, cancellationToken);

                    await _financialPostingService.RecordGstTransactionAsync(
                        null, "SALE", dto.InvoiceNumber, dto.BusinessDate.Date, taxableValue, cgst, sgst, totalCess, null, cancellationToken);

                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    synced++;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    ((DbContext)_context).ChangeTracker.Clear();
                    failed++;
                    errors.Add($"Failed to sync {dto.InvoiceNumber}: {ex.Message}");
                }
            });
        }

        return new SyncResult(synced, failed, errors);
    }
}

