using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PosErp.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
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
using PosErp.Domain.Entities.Crm;
using Microsoft.Extensions.Configuration;

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
    decimal PointsRedeemed = 0,
    string? SupervisorOverridePin = null
) : IRequest<CreateInvoiceResponse>;

public record InvoiceItemDto(Guid ProductId, decimal Quantity, decimal UnitPrice, Guid? BatchId);
public record CreateInvoiceResponse(Guid InvoiceId, string InvoiceNumber);

public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, CreateInvoiceResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IOfferEngine _offerEngine;
    private readonly IWalletService _walletService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly IFinancialPostingService _financialPostingService;
    private readonly PosErp.Application.Features.Inventory.Services.IStockLedgerService _stockLedgerService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccountResolutionService _accountResolutionService;
    private readonly ILogger<CreateInvoiceCommandHandler>? _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public CreateInvoiceCommandHandler(
        IApplicationDbContext context, 
        IOfferEngine offerEngine, 
        IWalletService walletService, 
        ILoyaltyService loyaltyService,
        IFinancialPostingService financialPostingService,
        PosErp.Application.Features.Inventory.Services.IStockLedgerService stockLedgerService,
        IPasswordHasher passwordHasher,
        IAccountResolutionService accountResolutionService,
        IHttpContextAccessor? httpContextAccessor = null,
        ILogger<CreateInvoiceCommandHandler>? logger = null,
        IConfiguration? configuration = null)
    {
        _context = context;
        _offerEngine = offerEngine;
        _walletService = walletService;
        _loyaltyService = loyaltyService;
        _financialPostingService = financialPostingService;
        _stockLedgerService = stockLedgerService;
        _passwordHasher = passwordHasher;
        _accountResolutionService = accountResolutionService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<CreateInvoiceResponse> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var strategy = ((DbContext)_context).Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
            try
            {
            var customer = request.CustomerId.HasValue 
                ? await _context.Customers.Include(c => c.Tier).FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value) 
                : null;

            // Retrieve the current active business date
            var activeDateSession = await _context.StoreBusinessDates
                .FirstOrDefaultAsync(d => d.StoreId == Guid.Empty && d.Status == "OPEN", cancellationToken);

            if (activeDateSession == null)
            {
                throw new Exception("No active business date is open. Please open a business date before recording transactions.");
            }

            var today = activeDateSession.BusinessDate;

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

            bool isBirthday = customer?.Dob.HasValue == true && customer.Dob.Value.Month == DateTime.Today.Month;
            bool isAnniversary = customer?.Anniversary.HasValue == true && customer.Anniversary.Value.Month == DateTime.Today.Month;

            cartEvaluation = await _offerEngine.EvaluateOffersAsync(cartEvaluation, customer?.Tier?.Name, request.PromoCode, isBirthday, isAnniversary, cancellationToken);

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

            // Loyalty Redemption Validation
            var loyaltyConfig = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync(cancellationToken) ?? new LoyaltyProgramConfig();
            if (request.PointsRedeemed > 0)
            {
                if (customer == null) throw new Exception("Customer required for points redemption.");
                if (customer.MembershipStatus == "Blocked" || customer.MembershipStatus == "Inactive")
                    throw new Exception("Customer account is not eligible for points redemption.");
                    
                if (customer.RunningLoyaltyPoints < request.PointsRedeemed)
                    throw new Exception("Insufficient points balance.");
                    
                // Check max % redemption limit
                decimal maxAllowedDiscount = (request.NetPayable * loyaltyConfig.MaxRedemptionPercentagePerInvoice) / 100m;
                decimal discountValue = (request.PointsRedeemed / loyaltyConfig.RedeemRatioPoints) * loyaltyConfig.RedeemRatioDiscountAmount;
                
                if (discountValue > maxAllowedDiscount)
                    throw new Exception($"Redemption exceeds maximum allowed limit of {loyaltyConfig.MaxRedemptionPercentagePerInvoice}% per invoice.");

                // Acquire FOR UPDATE lock on customer row to protect against concurrent checkout race conditions
                await ((DbContext)_context).Database.ExecuteSqlRawAsync(
                    "SELECT 1 FROM customers WHERE id = {0} FOR UPDATE", 
                    new object[] { customer.Id }, 
                    cancellationToken);

                // Sum all points redeemed today via explicit join
                decimal todayRedeemed = await (
                    from l in _context.LoyaltyLedger
                    join i in _context.Invoices on l.InvoiceId equals i.Id
                    where l.CustomerId == customer.Id 
                       && l.TransactionType == "Redeem Points" 
                       && i.BusinessDate == today
                    select l.PointsRedeemed
                ).SumAsync(cancellationToken);

                // Check cumulative daily limit and throw typed business exception
                if (todayRedeemed + request.PointsRedeemed > loyaltyConfig.MaxRedemptionPerDay)
                {
                    throw new InvalidOperationException($"DAILY_REDEMPTION_LIMIT_EXCEEDED: Redemption exceeds daily limit of {loyaltyConfig.MaxRedemptionPerDay} points. Cumulative redemption today: {todayRedeemed + request.PointsRedeemed} points.");
                }
            }



            // M3: validate against NetPayable (what frontend computed as the final bill amount)
            // Allow a small ₹1 tolerance for rounding edge-cases on split payments
            if (totalTender < request.NetPayable - 1m)
                throw new Exception($"Total tender (₹{totalTender:F2}) is less than the invoice amount (₹{request.NetPayable:F2}).");

            // Acquire FOR UPDATE lock on the terminal row to serialize sequence generation on this terminal
            await ((DbContext)_context).Database.ExecuteSqlRawAsync(
                "SELECT 1 FROM terminals WHERE id = {0} FOR UPDATE", 
                new object[] { request.TerminalId }, 
                cancellationToken);

            var terminal = await _context.Terminals.FindAsync(new object[] { request.TerminalId }, cancellationToken);
            if (terminal == null)
                throw new Exception($"Terminal with ID {request.TerminalId} not found.");

            var lastSeq = await _context.Invoices
                .Where(i => i.TerminalId == request.TerminalId && i.BusinessDate == today)
                .Select(i => (int?)i.TerminalSequence)
                .MaxAsync(cancellationToken) ?? 0;
            var nextSeq = lastSeq + 1;

            bool isTest = !string.IsNullOrEmpty(request.InvoiceNumber) && request.InvoiceNumber.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase);
            if (isTest)
            {
                if (_httpContextAccessor?.HttpContext == null)
                {
                    throw new UnauthorizedAccessException("Unable to verify caller identity for custom test invoice numbers.");
                }

                var httpContext = _httpContextAccessor.HttpContext;
                var userIdStr = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? httpContext.User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(userIdStr)) throw new UnauthorizedAccessException("User is not authenticated.");
                var callerId = Guid.Parse(userIdStr);

                var callerUser = await _context.Users
                    .Join(_context.Roles, u => u.RoleId, r => r.Id, (u, r) => new { User = u, Role = r })
                    .FirstOrDefaultAsync(x => x.User.Id == callerId, cancellationToken);

                bool isAllowed = callerUser != null && (callerUser.Role.Name == "Owner" || callerUser.Role.Name == "Developer");
                if (!isAllowed)
                {
                    throw new UnauthorizedAccessException("Unauthorized: Cashier role is not permitted to submit custom test invoice numbers.");
                }
            }
            string generatedInvoiceNumber = isTest ? request.InvoiceNumber : $"INV-{terminal.TerminalCode}-{today:yyyyMMdd}-{nextSeq:D4}";

            var invoiceId = Guid.NewGuid();
            var invoice = new Invoice
            {
                Id = invoiceId,
                InvoiceNumber = generatedInvoiceNumber,
                TerminalId = request.TerminalId,
                CashierId = request.CashierId,
                TerminalSequence = nextSeq,
                CustomerId = customer?.Id,
                BusinessDate = today,
                StoreId = Guid.Empty,
                IsTest = isTest,
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
            
            Guid storeId = Guid.Empty;
            // Record Offer Usage Logs
            if (cartEvaluation.AppliedOfferIds.Any())
            {
                var distinctAppliedOffers = cartEvaluation.Items
                    .Where(i => i.AppliedOfferId.HasValue)
                    .GroupBy(i => new { i.AppliedOfferId, i.AppliedOfferName })
                    .Select(g => new
                    {
                        OfferId = g.Key.AppliedOfferId!.Value,
                        OfferName = g.Key.AppliedOfferName!,
                        DiscountAmount = g.Sum(i => i.DiscountAmount)
                    })
                    .ToList();

                // Fetch terminal for name
                var terminalName = terminal?.Name ?? "Unknown";

                decimal originalCartVal = cartEvaluation.Items.Sum(i => i.LineTotal);
                decimal finalCartVal = invoice.TotalAmount; // the final amount

                foreach (var applied in distinctAppliedOffers)
                {
                    // get version from offer versions
                    var version = await _context.OfferVersions
                        .Where(v => v.OfferId == applied.OfferId)
                        .OrderByDescending(v => v.VersionNumber)
                        .Select(v => v.VersionNumber)
                        .FirstOrDefaultAsync();

                    _context.OfferUsageLogs.Add(new Domain.Entities.Offers.OfferUsageLog
                    {
                        Id = Guid.NewGuid(),
                        OfferId = applied.OfferId,
                        OfferName = applied.OfferName,
                        OfferVersion = version > 0 ? version : 1,
                        InvoiceId = invoice.Id,
                        InvoiceNumber = invoice.InvoiceNumber,
                        InvoiceDate = invoice.CreatedAt,
                        CustomerId = invoice.CustomerId,
                        TerminalId = invoice.TerminalId,
                        TerminalName = terminalName,
                        CashierId = invoice.CashierId,
                        StoreId = storeId,
                        DiscountAmount = applied.DiscountAmount,
                        OriginalCartValue = originalCartVal,
                        FinalCartValue = finalCartVal,
                        RevenueInfluenced = finalCartVal
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            // Verify supervisor override PIN if provided
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
                    if (selectedBatch != null)
                    {
                        expiryDate = selectedBatch.ExpiryDate;
                        selectedBatch.AvailableQuantity -= item.Quantity;
                    }
                }

                string movementTypeVal = overrideApproved ? "SALE_OVERRIDE" : "SALE";

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

            decimal pointsDiscount = 0m;
            if (request.PointsRedeemed > 0 && customer != null)
            {
                pointsDiscount = Math.Round((request.PointsRedeemed / loyaltyConfig.RedeemRatioPoints) * loyaltyConfig.RedeemRatioDiscountAmount, 2, MidpointRounding.AwayFromZero);
                
                decimal currentPts = customer.RunningLoyaltyPoints;
                customer.RunningLoyaltyPoints -= request.PointsRedeemed;
                customer.LastRedemptionDate = DateTime.UtcNow;
                
                var ptsLedger = new LoyaltyLedgerEntry
                {
                    CustomerId = customer.Id,
                    StoreId = Guid.Empty, // Default store for now
                    TransactionType = "Redeem Points",
                    PointsEarned = 0,
                    PointsRedeemed = request.PointsRedeemed,
                    PreviousBalance = currentPts,
                    BalanceAfterTransaction = customer.RunningLoyaltyPoints,
                    Points = 0 - request.PointsRedeemed,           // NET change (negative for redemption)
                    RunningPoints = customer.RunningLoyaltyPoints,  // cumulative balance after redemption
                    InvoiceId = invoice.Id,
                    ReferenceDocument = finalInvoiceRef,
                    Remarks = $"Redeemed {request.PointsRedeemed} points during checkout."
                };
                _context.LoyaltyLedger.Add(ptsLedger);
            }

            // C6: Loyalty points calculated on NetPayable minus points redemption discount (cash paid basis)
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

            // GUARD: Skip journal posting entirely for zero-value invoices.
            // This can occur when all items have ₹0 selling price, or when offers
            // reduce the total to zero. Posting a zero-value journal would fail the
            // debit/credit balance check with "Journal entry amount must be greater than zero."
            bool shouldPostJournal = invoice.NetPayable > 0;

            if (shouldPostJournal)
            {
            // Resolve account codes dynamically
            string cashAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("ASSET", "Cash", _configuration?["Finance:AccountDefaults:Cash"] ?? "10100", cancellationToken);
            string digitalAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("ASSET", "Current", _configuration?["Finance:AccountDefaults:DigitalBank"] ?? "10200", cancellationToken);
            string walletAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("LIABILITY", "Wallet", _configuration?["Finance:AccountDefaults:WalletLiability"] ?? "20200", cancellationToken);
            string arAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("ASSET", "Receivable", _configuration?["Finance:AccountDefaults:AccountsReceivable"] ?? "10400", cancellationToken);
            string salesAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("REVENUE", "Sales", _configuration?["Finance:AccountDefaults:SalesRevenue"] ?? "40100", cancellationToken);
            string outputCgstAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("LIABILITY", "Output CGST", _configuration?["Finance:AccountDefaults:OutputCGST"] ?? "22010", cancellationToken);
            string outputSgstAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("LIABILITY", "Output SGST", _configuration?["Finance:AccountDefaults:OutputSGST"] ?? "22020", cancellationToken);
            string inventoryAssetAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("ASSET", "Inventory Asset", _configuration?["Finance:AccountDefaults:Inventory"] ?? "10300", cancellationToken);
            string cogsAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("EXPENSE", "Cost of Goods Sold", _configuration?["Finance:AccountDefaults:Cogs"] ?? "50100", cancellationToken);

            decimal totalCogs = 0;
            foreach (var item in cartEvaluation.Items)
            {
                if (productsInfo.TryGetValue(item.ProductId, out var product))
                {
                    if (product.PurchasePrice > 0)
                    {
                        totalCogs += item.Quantity * product.PurchasePrice;
                    }
                    else
                    {
                        _logger?.LogWarning("Skipping COGS calculation for Product {ProductId} ({ProductName}) because purchase price is 0 or less.", item.ProductId, product.Name);
                    }
                }
            }

            if (actualCashPaid > 0) journalLines.Add(new JournalLineDto { AccountCode = cashAccountCode, Description = "Cash Tender", Debit = actualCashPaid, Credit = 0 });
            if (request.UpiAmount > 0 || request.CardAmount > 0) journalLines.Add(new JournalLineDto { AccountCode = digitalAccountCode, Description = "Digital Tender", Debit = request.UpiAmount + request.CardAmount, Credit = 0 });
            if (request.WalletAmountUsed > 0) journalLines.Add(new JournalLineDto { AccountCode = walletAccountCode, Description = "Wallet Redemption", Debit = request.WalletAmountUsed, Credit = 0 });
            if (creditSaleAmount > 0) journalLines.Add(new JournalLineDto { AccountCode = arAccountCode, Description = $"Credit Sale AR for {customer?.Name}", Debit = creditSaleAmount, Credit = 0 });
            
            if (pointsDiscount > 0)
            {
                string loyaltyAccountCode = await _accountResolutionService.ResolveAccountCodeAsync("LIABILITY", "Loyalty Points", _configuration?["Finance:AccountDefaults:LoyaltyPoints"] ?? "20300", cancellationToken);
                journalLines.Add(new JournalLineDto { AccountCode = loyaltyAccountCode, Description = "Loyalty Points Redemption", Debit = pointsDiscount, Credit = 0 });
            }

            if (totalCogs > 0)
            {
                journalLines.Add(new JournalLineDto { AccountCode = cogsAccountCode, Description = "Cost of Goods Sold", Debit = totalCogs, Credit = 0 });
                journalLines.Add(new JournalLineDto { AccountCode = inventoryAssetAccountCode, Description = "Inventory Asset Reduction", Debit = 0, Credit = totalCogs });
            }
            
            // Credits (Revenue & Tax Liability)
            // Sales Revenue = SubTotal (post-discount, ex-tax) + any round-off adjustment
            decimal revenueCredit = taxableValue + invoice.RoundOff;
            if (revenueCredit > 0) journalLines.Add(new JournalLineDto { AccountCode = salesAccountCode, Description = "Sales Revenue", Debit = 0, Credit = revenueCredit });
            if (cgst > 0) journalLines.Add(new JournalLineDto { AccountCode = outputCgstAccountCode, Description = "Output CGST", Debit = 0, Credit = cgst });
            if (sgst > 0) journalLines.Add(new JournalLineDto { AccountCode = outputSgstAccountCode, Description = "Output SGST", Debit = 0, Credit = sgst });
            } // end shouldPostJournal

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

            // Post Journal Entry (only if there are lines to post i.e. non-zero invoice)
            Guid jeId = Guid.Empty;
            if (journalLines.Count > 0)
            {
                jeId = await _financialPostingService.PostJournalEntryAsync(
                    null, DateTime.UtcNow, $"POS Invoice {invoice.InvoiceNumber}", $"INV-{invoice.Id}", journalLines, cancellationToken);
            }

            if (creditSaleAmount > 0 && customer != null && jeId != Guid.Empty)
            {
                var ledgerEntry = await _context.CustomerLedger
                    .Where(c => c.CustomerId == customer.Id && c.ReferenceNumber == invoice.InvoiceNumber)
                    .FirstOrDefaultAsync(cancellationToken);
                if (ledgerEntry != null)
                {
                    ledgerEntry.JournalEntryId = jeId;
                }
            }

            // Post Dedicated Tax Transaction for GSTR Returns (only for non-zero invoices)
            if (shouldPostJournal)
                await _financialPostingService.RecordGstTransactionAsync(
                    null, "SALE", invoice.InvoiceNumber, DateTime.UtcNow, taxableValue, totalCgst, totalSgst, totalCess, null, cancellationToken);

            // Flush ALL pending EF changes (loyalty ledger, wallet, financial lines) before committing the transaction
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new CreateInvoiceResponse(invoice.Id, invoice.InvoiceNumber);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        });
    }
}
