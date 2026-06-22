using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Finance;
using PosErp.Application.Features.Offers.Services;
using PosErp.Application.Features.Crm.Services;
using PosErp.Application.Features.Offers.Models;
using PosErp.Application.Features.Finance.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PosErp.Application.Features.Inventory.Services;

namespace PosErp.Application.Features.Pos.Commands.SyncInvoices;

public record CreateInvoiceCommand(
    string InvoiceNumber,
    Guid TerminalId,
    Guid CashierId,
    Guid? CustomerId,
    string? PromoCode,
    decimal WalletAmountUsed,
    decimal CashAmount,
    decimal UpiAmount,
    decimal CardAmount,
    decimal RoundOff,
    decimal NetPayable,
    string PaymentMode,
    List<InvoiceItemDto> Items,
    string? SupervisorOverridePin = null
) : IRequest<Guid>;

public record InvoiceItemDto(Guid ProductId, decimal Quantity, decimal UnitPrice, Guid? BatchId);

public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IOfferEngine _offerEngine;
    private readonly IWalletService _walletService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly IFinancialPostingService _financialPostingService;
    private readonly PosErp.Application.Features.Inventory.Services.IStockLedgerService _stockLedgerService;
    private readonly IPasswordHasher _passwordHasher;

    public CreateInvoiceCommandHandler(
        IApplicationDbContext context, 
        IOfferEngine offerEngine, 
        IWalletService walletService, 
        ILoyaltyService loyaltyService,
        IFinancialPostingService financialPostingService,
        PosErp.Application.Features.Inventory.Services.IStockLedgerService stockLedgerService,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _offerEngine = offerEngine;
        _walletService = walletService;
        _loyaltyService = loyaltyService;
        _financialPostingService = financialPostingService;
        _stockLedgerService = stockLedgerService;
        _passwordHasher = passwordHasher;
    }

    public async Task<Guid> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var customer = request.CustomerId.HasValue 
                ? await _context.Customers.Include(c => c.Tier).FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value) 
                : null;

            var productIds = request.Items.Select(i => i.ProductId).ToList();
            var productsInfo = await _context.Products
                .Include(p => p.TaxSlab)
                .Include(p => p.Barcodes)  // H2: needed to populate InvoiceItem.Barcode
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var cartEvaluation = new CartEvaluationDto
            {
                Items = request.Items.Select(i => new CartItemEvaluationDto
                {
                    ProductId = i.ProductId,
                    CategoryId = productsInfo.TryGetValue(i.ProductId, out var pInfo) ? pInfo.CategoryId : null,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };

            cartEvaluation = await _offerEngine.EvaluateOffersAsync(cartEvaluation, customer?.Tier?.Name, request.PromoCode, cancellationToken);

            decimal totalTender = request.WalletAmountUsed + request.CashAmount + request.UpiAmount + request.CardAmount;
            if (request.PaymentMode == "CREDIT")
            {
                if (customer == null)
                    throw new Exception("A customer must be selected for credit sales.");

                var currentBalance = await _context.CustomerLedger
                    .Where(c => c.CustomerId == customer.Id)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => c.RunningBalance)
                    .FirstOrDefaultAsync(cancellationToken);

                if (currentBalance + request.NetPayable > customer.CreditLimit)
                {
                    throw new Exception($"CREDIT_LIMIT_EXCEEDED: Credit limit exceeded. Limit: {customer.CreditLimit:F2}, Current Balance: {currentBalance:F2}, New Sale: {request.NetPayable:F2}.");
                }

                // In a credit sale, the remaining amount is charged to credit
                totalTender += (request.NetPayable - request.WalletAmountUsed);
            }

            // M3: validate against NetPayable (what frontend computed as the final bill amount)
            // Allow a small ₹1 tolerance for rounding edge-cases on split payments
            if (totalTender < request.NetPayable - 1m)
                throw new Exception($"Total tender (₹{totalTender:F2}) is less than the invoice amount (₹{request.NetPayable:F2}).");

            // Retrieve the current active business date
            var activeDateSession = await _context.StoreBusinessDates
                .FirstOrDefaultAsync(d => d.StoreId == Guid.Empty && d.Status == "OPEN", cancellationToken);

            if (activeDateSession == null)
            {
                throw new Exception("No active business date is open. Please open a business date before recording transactions.");
            }

            var today = activeDateSession.BusinessDate;
            var lastSeq = await _context.Invoices
                .Where(i => i.TerminalId == request.TerminalId && i.BusinessDate == today)
                .Select(i => (int?)i.TerminalSequence)
                .MaxAsync(cancellationToken) ?? 0;
            var nextSeq = lastSeq + 1;

            var invoiceId = Guid.NewGuid();
            var invoice = new Invoice
            {
                Id = invoiceId,
                InvoiceNumber = request.InvoiceNumber,
                TerminalId = request.TerminalId,
                CashierId = request.CashierId,
                TerminalSequence = nextSeq,
                CustomerId = customer?.Id,
                BusinessDate = today,
                // SubTotal = sum of post-discount line totals (what's printed in the item section)
                SubTotal = cartEvaluation.Items.Sum(i => i.FinalLineTotal),
                // DiscountAmount = total discount applied (for reporting purposes)
                DiscountAmount = cartEvaluation.TotalDiscount,
                // TaxAmount will be set after the items loop (sum of per-item CGST + SGST)
                TaxAmount = 0,
                // TotalAmount and NetPayable will also be set after items loop
                TotalAmount = 0,
                RoundOff = request.RoundOff,
                NetPayable = request.NetPayable,
                PaymentMode = request.PaymentMode,
                CashAmount = request.CashAmount,
                UpiAmount = request.UpiAmount,
                CardAmount = request.CardAmount,
                WalletAmount = request.WalletAmountUsed,
                Status = "COMPLETED"
            };

            foreach (var item in cartEvaluation.Items)
            {
                var product = productsInfo.TryGetValue(item.ProductId, out var p) ? p : null;
                decimal cgstRate = product?.TaxSlab?.CgstRate ?? 0;
                decimal sgstRate = product?.TaxSlab?.SgstRate ?? 0;
                decimal cessRate = product?.TaxSlab?.CessRate ?? 0;
                decimal totalTaxRate = cgstRate + sgstRate + cessRate;

                decimal taxableAmount = totalTaxRate > 0 
                    ? item.FinalLineTotal / (1 + (totalTaxRate / 100m)) 
                    : item.FinalLineTotal;

                // Tax is computed on the post-discount line total (FinalLineTotal) using tax-inclusive formula
                decimal cgstAmount = Math.Round(taxableAmount * (cgstRate / 100m), 2);
                decimal sgstAmount = Math.Round(taxableAmount * (sgstRate / 100m), 2);
                decimal cessAmount = Math.Round(taxableAmount * (cessRate / 100m), 2);
                // H2: Populate Barcode from the product's first barcode entry
                string? primaryBarcode = product?.Barcodes?.FirstOrDefault()?.BarcodeValue;

                invoice.Items.Add(new InvoiceItem
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    ProductId = item.ProductId,
                    ProductName = product?.Name ?? string.Empty,
                    Barcode = primaryBarcode,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Total = item.LineTotal,
                    DiscountAmount = item.DiscountAmount,
                    FinalTotal = item.FinalLineTotal,
                    BusinessDate = today,
                    CgstRate = cgstRate,
                    CgstAmount = cgstAmount,
                    SgstRate = sgstRate,
                    SgstAmount = sgstAmount,
                    CessRate = cessRate,
                    CessAmount = cessAmount
                });
            }

            // Now that all items are built with their CGST/SGST/Cess amounts,
            // set invoice-level TaxAmount and TotalAmount from actual item sums.
            // This ensures the stored values match exactly what is printed on the receipt.
            invoice.TaxAmount = invoice.Items.Sum(i => i.CgstAmount + i.SgstAmount + i.CessAmount);
            
            // SubTotal is the ex-tax final total (discounted total minus tax)
            invoice.SubTotal = invoice.Items.Sum(i => i.FinalTotal - (i.CgstAmount + i.SgstAmount + i.CessAmount));
            
            // TotalAmount = discounted subtotal + actual tax (pre-round-off amount billed) which matches sum of FinalTotals
            invoice.TotalAmount = invoice.SubTotal + invoice.TaxAmount;
            // NetPayable (already set from frontend, includes round-off) is the source of truth
            // but TotalAmount without round-off is used for revenue reporting accuracy.

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync(cancellationToken); 

            // Deduct Stock
            Guid storeId = Guid.Empty;
            var rules = InventoryRulesManager.GetRules();

            if (rules.PreventNegativeStock)
            {
                foreach (var item in cartEvaluation.Items)
                {
                    var product = productsInfo.TryGetValue(item.ProductId, out var p) ? p : null;
                    var productName = product?.Name ?? "Unknown Product";

                    var availableStock = await _context.StockLedger
                        .Where(sl => sl.ProductId == item.ProductId && sl.StoreId == storeId)
                        .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;

                    if (availableStock < item.Quantity)
                    {
                        bool overrideApproved = false;
                        if (!string.IsNullOrWhiteSpace(request.SupervisorOverridePin))
                        {
                            var usersWithPin = await _context.Users
                                .Join(_context.Roles,
                                    u => u.RoleId,
                                    r => r.Id,
                                    (u, r) => new { User = u, Role = r })
                                .Where(x => x.User.IsActive && !x.User.IsDeleted && x.User.PinHash != null &&
                                    (x.Role.Name == "Supervisor" || x.Role.Name == "Manager" || x.Role.Name == "Owner"))
                                .Select(x => x.User)
                                .ToListAsync(cancellationToken);

                            foreach (var user in usersWithPin)
                            {
                                if (_passwordHasher.VerifyPassword(request.SupervisorOverridePin, user.PinHash!))
                                {
                                    overrideApproved = true;
                                    break;
                                }
                            }
                        }

                        if (!overrideApproved)
                        {
                            throw new Exception($"INSUFFICIENT_STOCK: Item '{productName}' is out of stock. Available: {availableStock}, Requested: {item.Quantity}. Scan a supervisor PIN to override.");
                        }
                    }
                }
            }

            foreach (var item in cartEvaluation.Items)
            {
                var originalItem = request.Items.FirstOrDefault(x => x.ProductId == item.ProductId);
                Guid? selectedBatchId = originalItem?.BatchId;
                var hasExpiry = productsInfo.TryGetValue(item.ProductId, out var pInfo) && pInfo.HasExpiry;
                DateTime? expiryDate = null;

                if (selectedBatchId == null && hasExpiry)
                {
                    var activeBatches = await _context.ProductBatches
                        .Where(b => b.ProductId == item.ProductId && b.IsActive)
                        .ToListAsync(cancellationToken);

                    if (activeBatches.Any())
                    {
                        var batchStocks = new List<(Guid Id, DateTime? ExpiryDate, decimal Stock)>();
                        foreach (var b in activeBatches)
                        {
                            var stock = await _context.StockLedger
                                .Where(sl => sl.ProductId == item.ProductId && sl.BatchId == b.Id)
                                .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;
                            batchStocks.Add((b.Id, b.ExpiryDate, stock));
                        }

                        var bestBatchId = batchStocks
                            .Where(x => x.Stock > 0)
                            .OrderBy(x => x.ExpiryDate.HasValue ? 0 : 1)
                            .ThenBy(x => x.ExpiryDate)
                            .Select(x => (Guid?)x.Id)
                            .FirstOrDefault();

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
                }

                if (selectedBatchId.HasValue)
                {
                    var selectedBatch = await _context.ProductBatches.FindAsync(new object[] { selectedBatchId.Value }, cancellationToken);
                    expiryDate = selectedBatch?.ExpiryDate;
                }

                // Check if this specific item breached stock level to log override status
                var itemStock = await _context.StockLedger
                    .Where(sl => sl.ProductId == item.ProductId && sl.StoreId == storeId)
                    .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;

                string movementTypeVal = (rules.PreventNegativeStock && itemStock < item.Quantity) ? "SALE_OVERRIDE" : "SALE";

                string invoiceRef = invoice.InvoiceNumber.StartsWith("INV-", StringComparison.OrdinalIgnoreCase)
                    ? invoice.InvoiceNumber
                    : $"INV-{invoice.InvoiceNumber}";

                await _stockLedgerService.RecordMovementAsync(
                    storeId: storeId,
                    warehouseId: null,
                    terminalId: request.TerminalId,
                    businessDate: today,
                    productId: item.ProductId,
                    batchId: selectedBatchId,
                    movementType: movementTypeVal,
                    quantity: -item.Quantity,
                    unitCost: pInfo?.PurchasePrice ?? 0m,
                    expiryDate: expiryDate,
                    referenceDocId: invoice.Id,
                    referenceNumber: invoiceRef,
                    userId: request.CashierId,
                    cancellationToken: cancellationToken
                );
            }

            string finalInvoiceRef = invoice.InvoiceNumber.StartsWith("INV-", StringComparison.OrdinalIgnoreCase)
                ? invoice.InvoiceNumber
                : $"INV-{invoice.InvoiceNumber}";

            if (request.WalletAmountUsed > 0 && customer != null)
                await _walletService.RecordTransactionAsync(customer.Id, null, "SPEND", -request.WalletAmountUsed, finalInvoiceRef, null, cancellationToken);

            // C6: Loyalty points calculated on NetPayable (actual amount charged)
            // This call is AFTER invoice.TotalAmount is set (post-items-loop), so points are non-zero
            if (customer != null)
                await _loyaltyService.CalculateAndAwardPointsForInvoiceAsync(invoice.Id, customer.Id, invoice.NetPayable, cancellationToken);

            // ==========================================
            // PHASE 4: FINANCIAL DOUBLE-ENTRY POSTING
            // ==========================================
            
            // C5: Use invoice-level tax amounts (computed from actual per-item CGST/SGST)
            // NOT stale cartEvaluation values which were pre-fix
            decimal totalCgst = invoice.Items.Sum(i => i.CgstAmount);
            decimal totalSgst = invoice.Items.Sum(i => i.SgstAmount);
            decimal totalCess = invoice.Items.Sum(i => i.CessAmount);

            // For double-entry posting, we split the total tax amount (CGST+SGST+Cess) between CGST/SGST
            decimal cgst = Math.Round(invoice.TaxAmount / 2m, 2);
            decimal sgst = invoice.TaxAmount - cgst; // ensure CGST+SGST = TaxAmount exactly
            decimal taxableValue = invoice.SubTotal; // SubTotal = sum of post-discount item totals (ex-tax)

            decimal creditSaleAmount = 0;
            decimal actualCashPaid = 0;

            if (request.PaymentMode == "CREDIT")
            {
                creditSaleAmount = invoice.NetPayable - request.WalletAmountUsed;
                invoice.DueDate = today.AddDays(30); // Default 30 credit days for customer AR
            }
            else
            {
                actualCashPaid = invoice.NetPayable - request.UpiAmount - request.CardAmount - request.WalletAmountUsed;
            }

            var journalLines = new List<JournalLineDto>();

            if (actualCashPaid > 0) journalLines.Add(new JournalLineDto { AccountCode = "1000", Description = "Cash Tender", Debit = actualCashPaid, Credit = 0 });
            if (request.UpiAmount > 0 || request.CardAmount > 0) journalLines.Add(new JournalLineDto { AccountCode = "1100", Description = "Digital Tender", Debit = request.UpiAmount + request.CardAmount, Credit = 0 });
            if (request.WalletAmountUsed > 0) journalLines.Add(new JournalLineDto { AccountCode = "2100", Description = "Wallet Redemption", Debit = request.WalletAmountUsed, Credit = 0 });
            if (creditSaleAmount > 0) journalLines.Add(new JournalLineDto { AccountCode = "20200", Description = $"Credit Sale AR for {customer?.Name}", Debit = creditSaleAmount, Credit = 0 });
            
            // Credits (Revenue & Tax Liability)
            // Sales Revenue = SubTotal (post-discount, ex-tax) + any round-off adjustment
            decimal revenueCredit = taxableValue + invoice.RoundOff;
            journalLines.Add(new JournalLineDto { AccountCode = "4000", Description = "Sales Revenue", Debit = 0, Credit = revenueCredit });
            if (cgst > 0) journalLines.Add(new JournalLineDto { AccountCode = "2200", Description = "Output CGST", Debit = 0, Credit = cgst });
            if (sgst > 0) journalLines.Add(new JournalLineDto { AccountCode = "2201", Description = "Output SGST", Debit = 0, Credit = sgst });

            // Post to customer ledger if credit sale
            if (creditSaleAmount > 0 && customer != null)
            {
                decimal currentLedgerBal = await _context.CustomerLedger
                    .Where(c => c.CustomerId == customer.Id)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => c.RunningBalance)
                    .FirstOrDefaultAsync(cancellationToken);

                currentLedgerBal += creditSaleAmount;

                var ledgerEntry = new CustomerLedgerEntry
                {
                    StoreId = storeId,
                    CustomerId = customer.Id,
                    EntryDate = today,
                    TransactionType = "INVOICE",
                    ReferenceNumber = invoice.InvoiceNumber,
                    DebitAmount = creditSaleAmount,
                    CreditAmount = 0,
                    RunningBalance = currentLedgerBal,
                    Description = $"Credit Sale Invoice {invoice.InvoiceNumber}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.CustomerLedger.Add(ledgerEntry);
                await _context.SaveChangesAsync(cancellationToken);
            }

            // Post Journal Entry
            Guid jeId = await _financialPostingService.PostJournalEntryAsync(
                null, DateTime.UtcNow, $"POS Invoice {invoice.InvoiceNumber}", $"INV-{invoice.Id}", journalLines, cancellationToken);

            if (creditSaleAmount > 0 && customer != null)
            {
                var ledgerEntry = await _context.CustomerLedger
                    .Where(c => c.CustomerId == customer.Id && c.ReferenceNumber == invoice.InvoiceNumber)
                    .FirstOrDefaultAsync(cancellationToken);
                if (ledgerEntry != null)
                {
                    ledgerEntry.JournalEntryId = jeId;
                }
            }

            // Post Dedicated Tax Transaction for GSTR Returns
            await _financialPostingService.RecordGstTransactionAsync(
                null, "SALE", invoice.InvoiceNumber, DateTime.UtcNow, taxableValue, totalCgst, totalSgst, totalCess, null, cancellationToken);

            // Flush ALL pending EF changes (loyalty ledger, wallet, financial lines) before committing the transaction
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return invoice.Id;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
