using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Finance.Services;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Domain.Entities.Finance;
using PosErp.Domain.Entities.Purchasing;
using PosErp.Domain.Entities.Inventory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace PosErp.Application.Features.Finance.Commands;

public record ProcessPurchaseReturnCommand(
    Guid StoreId,
    Guid SupplierId,
    Guid? GRNHeaderId,
    DateTime ReturnDate,
    List<PurchaseReturnItemInputDto> Items,
    Guid UserId
) : IRequest<Guid>, IRetryableRequest;

public record PurchaseReturnItemInputDto(
    Guid ProductId,
    Guid? BatchId,
    decimal Quantity
);

public record ProcessSalesReturnCommand(
    Guid StoreId,
    Guid InvoiceId,
    DateTime ReturnDate,
    string RefundMode, // CASH, UPI, CREDIT_NOTE
    List<SalesReturnItemInputDto> Items,
    Guid UserId,
    string? ManagerOverridePin = null
) : IRequest<Guid>, IRetryableRequest;

public record SalesReturnItemInputDto(
    Guid ProductId,
    Guid? BatchId,
    decimal Quantity,
    Guid? ItemId = null
);

public class ReturnCommandsHandler :
    IRequestHandler<ProcessPurchaseReturnCommand, Guid>,
    IRequestHandler<ProcessSalesReturnCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IFinancialPostingService _postingService;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IStockLedgerService _stockLedgerService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;

    public ReturnCommandsHandler(
        IApplicationDbContext context,
        IFinancialPostingService postingService,
        IDocumentSequenceService sequenceService,
        IStockLedgerService stockLedgerService,
        IPasswordHasher passwordHasher,
        IConfiguration? configuration = null)
    {
        _context = context;
        _postingService = postingService;
        _sequenceService = sequenceService;
        _stockLedgerService = stockLedgerService;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
    }

    public async Task<Guid> Handle(ProcessPurchaseReturnCommand request, CancellationToken cancellationToken)
    {
        var strategy = ((DbContext)_context).Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var supplier = await _context.Suppliers.FindAsync(new object[] { request.SupplierId }, cancellationToken);
            if (supplier == null) throw new InvalidOperationException("Supplier not found.");

            string returnNo = await _sequenceService.GenerateNextNumberAsync(request.StoreId, "PURCHASE_RETURN", cancellationToken);

            var purchaseReturn = new PurchaseReturn
            {
                StoreId = request.StoreId,
                SupplierId = request.SupplierId,
                GRNHeaderId = request.GRNHeaderId,
                ReturnNumber = returnNo,
                ReturnDate = request.ReturnDate.Date,
                Status = "POSTED",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.UserId
            };

            decimal totalSubTotal = 0;
            decimal totalTax = 0;
            decimal totalAmount = 0;

            foreach (var item in request.Items)
            {
                var product = await _context.Products
                    .Include(p => p.TaxSlab)
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken);
                if (product == null) throw new InvalidOperationException($"Product with ID {item.ProductId} not found.");

                ProductBatch? batch = null;
                if (item.BatchId.HasValue && item.BatchId.Value != Guid.Empty)
                {
                    batch = await _context.ProductBatches.FindAsync(new object[] { item.BatchId.Value }, cancellationToken);
                    if (batch == null) throw new InvalidOperationException($"Product batch with ID {item.BatchId} not found.");
                }
                else
                {
                    batch = await _context.ProductBatches
                        .Where(b => b.ProductId == item.ProductId)
                        .OrderByDescending(b => b.CreatedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                    
                    if (batch == null) throw new InvalidOperationException($"No batch found for Product ID {item.ProductId}. Returns require a batch to deduct from.");
                }

                // Make sure we have enough physical stock in this batch to return
                if (batch.AvailableQuantity < item.Quantity)
                {
                    throw new InvalidOperationException($"Insufficient batch quantity for return. Available: {batch.AvailableQuantity}, Requested: {item.Quantity}");
                }

                // Check original GRN cost if linked
                decimal unitCost = batch.CostPrice;
                if (request.GRNHeaderId.HasValue)
                {
                    var grnItem = await _context.GRNItems
                        .FirstOrDefaultAsync(gi => gi.GRNHeaderId == request.GRNHeaderId.Value && gi.ProductId == item.ProductId, cancellationToken);
                    if (grnItem != null)
                    {
                        unitCost = grnItem.UnitCost;
                    }
                }

                decimal cgstRate = product.TaxSlab?.CgstRate ?? 0;
                decimal sgstRate = product.TaxSlab?.SgstRate ?? 0;
                decimal cessRate = product.TaxSlab?.CessRate ?? 0;
                decimal totalTaxRate = cgstRate + sgstRate + cessRate;

                decimal lineSubTotal = unitCost * item.Quantity;
                decimal lineTax = Math.Round(lineSubTotal * (totalTaxRate / 100m), 2);
                decimal lineTotal = lineSubTotal + lineTax;

                purchaseReturn.Items.Add(new PurchaseReturnItem
                {
                    ProductId = item.ProductId,
                    BatchId = batch.Id,
                    Quantity = item.Quantity,
                    UnitCost = unitCost,
                    TaxAmount = lineTax,
                    TotalAmount = lineTotal
                });

                // Update physical batch quantities
                batch.AvailableQuantity -= item.Quantity;

                // Record stock movement (negative for return)
                await _stockLedgerService.RecordMovementAsync(
                    storeId: request.StoreId,
                    warehouseId: null,
                    terminalId: null,
                    businessDate: request.ReturnDate.Date,
                    productId: item.ProductId,
                    batchId: batch.Id,
                    movementType: "PURCHASE_RETURN",
                    quantity: -item.Quantity,
                    unitCost: unitCost,
                    expiryDate: batch.ExpiryDate,
                    referenceDocId: purchaseReturn.Id,
                    referenceNumber: returnNo,
                    userId: request.UserId,
                    cancellationToken: cancellationToken
                );

                totalSubTotal += lineSubTotal;
                totalTax += lineTax;
                totalAmount += lineTotal;
            }

            purchaseReturn.SubTotal = totalSubTotal;
            purchaseReturn.TaxAmount = totalTax;
            purchaseReturn.TotalAmount = totalAmount;

            _context.PurchaseReturns.Add(purchaseReturn);
            await _context.SaveChangesAsync(cancellationToken);

            // Double Entry Journal:
            // Debit Accounts Payable - Vendors 20100 (reduces liability)
            // Credit Inventory Asset 10300
            // Credit Input CGST 22030 (reversal of input credit)
            // Credit Input SGST 22040 (reversal of input credit)
            decimal cgstReversal = Math.Round(totalTax / 2m, 2);
            decimal sgstReversal = totalTax - cgstReversal;

            string apAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Accounts Payable", _configuration?["Finance:AccountDefaults:AccountsPayable"] ?? "20100", cancellationToken);
            string inventoryAccountCode = await ResolveAccountCodeAsync("ASSET", "Inventory", _configuration?["Finance:AccountDefaults:Inventory"] ?? "10300", cancellationToken);
            string inputCgstAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Input CGST", _configuration?["Finance:AccountDefaults:InputCGST"] ?? "22030", cancellationToken);
            string inputSgstAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Input SGST", _configuration?["Finance:AccountDefaults:InputSGST"] ?? "22040", cancellationToken);

            var journalLines = new List<JournalLineDto>
            {
                new() { AccountCode = apAccountCode, Description = $"Purchase Return {returnNo}", Debit = totalAmount, Credit = 0 },
                new() { AccountCode = inventoryAccountCode, Description = $"Inventory reversal for return {returnNo}", Debit = 0, Credit = totalSubTotal }
            };

            if (cgstReversal > 0)
                journalLines.Add(new() { AccountCode = inputCgstAccountCode, Description = $"Input CGST Reversal for return {returnNo}", Debit = 0, Credit = cgstReversal });
            if (sgstReversal > 0)
                journalLines.Add(new() { AccountCode = inputSgstAccountCode, Description = $"Input SGST Reversal for return {returnNo}", Debit = 0, Credit = sgstReversal });

            Guid jeId = await _postingService.PostJournalEntryWithUserAsync(
                request.StoreId,
                request.ReturnDate,
                $"Purchase Return to {supplier.Name} ({returnNo})",
                returnNo,
                journalLines,
                request.UserId,
                isDraft: false,
                cancellationToken,
                sourceModule: "PURCHASING",
                sourceDocType: "PURCHASE_RETURN",
                sourceDocId: purchaseReturn.Id
            );

            purchaseReturn.JournalEntryId = jeId;

            // Record GST Transaction (reduction in inputs/purchases)
            await _postingService.RecordGstTransactionAsync(
                request.StoreId,
                "PURCHASE_RETURN",
                returnNo,
                request.ReturnDate,
                totalSubTotal,
                -cgstReversal,
                -sgstReversal,
                0,
                supplier.Gstin,
                cancellationToken
            );

            // Post to Supplier Ledger (Debit reduces Accounts Payable)
            decimal runningBalance = await _context.SupplierLedger
                .Where(s => s.SupplierId == request.SupplierId && s.StoreId == request.StoreId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.RunningBalance)
                .FirstOrDefaultAsync(cancellationToken);

            runningBalance -= totalAmount;

            var ledgerEntry = new SupplierLedgerEntry
            {
                StoreId = request.StoreId,
                SupplierId = request.SupplierId,
                EntryDate = request.ReturnDate.Date,
                TransactionType = "DEBIT_NOTE",
                ReferenceNumber = returnNo,
                DebitAmount = totalAmount,
                CreditAmount = 0,
                RunningBalance = runningBalance,
                Description = $"Purchase Return {returnNo} posted",
                JournalEntryId = jeId,
                CreatedAt = DateTime.UtcNow
            };
            _context.SupplierLedger.Add(ledgerEntry);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return purchaseReturn.Id;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<Guid> Handle(ProcessSalesReturnCommand request, CancellationToken cancellationToken)
    {
        var strategy = ((DbContext)_context).Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Authorize caller or Manager Override PIN
                var callerUser = await _context.Users
                    .Join(_context.Roles,
                        u => u.RoleId,
                        r => r.Id,
                        (u, r) => new { User = u, Role = r })
                    .FirstOrDefaultAsync(x => x.User.Id == request.UserId, cancellationToken);

                bool isAutoAuthorized = callerUser != null && 
                    (callerUser.Role.Name == "Manager" || callerUser.Role.Name == "Owner" || callerUser.Role.Name == "Supervisor");

                if (!isAutoAuthorized)
                {
                    if (string.IsNullOrWhiteSpace(request.ManagerOverridePin))
                    {
                        throw new UnauthorizedAccessException("Manager or Owner authorization is required to process a sales return.");
                    }

                    // Verify manager PIN
                    var usersWithPin = await _context.Users
                        .Join(_context.Roles,
                            u => u.RoleId,
                            r => r.Id,
                            (u, r) => new { User = u, Role = r })
                        .Where(x => x.User.IsActive && !x.User.IsDeleted && x.User.PinHash != null &&
                            (x.Role.Name == "Supervisor" || x.Role.Name == "Manager" || x.Role.Name == "Owner"))
                        .Select(x => x.User)
                        .ToListAsync(cancellationToken);

                    bool pinVerified = false;
                    foreach (var user in usersWithPin)
                    {
                        if (_passwordHasher.VerifyPassword(request.ManagerOverridePin, user.PinHash!))
                        {
                            pinVerified = true;
                            break;
                        }
                    }

                    if (!pinVerified)
                    {
                        throw new UnauthorizedAccessException("Invalid Manager Override PIN. Access denied.");
                    }
                }
                var invoice = await _context.Invoices
                .Include(i => i.Items)
                .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);
            if (invoice == null) throw new InvalidOperationException("Invoice not found.");

            string returnNo = await _sequenceService.GenerateNextNumberAsync(request.StoreId, "SALES_RETURN", cancellationToken);

            var salesReturn = new SalesReturn
            {
                StoreId = request.StoreId,
                InvoiceId = request.InvoiceId,
                BusinessDate = invoice.BusinessDate,
                ReturnNumber = returnNo,
                ReturnDate = request.ReturnDate.Date,
                RefundMode = request.RefundMode,
                Status = "COMPLETED",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.UserId
            };

            decimal totalSubTotal = 0;
            decimal totalTax = 0;
            decimal totalAmount = 0;
            decimal totalCostReversal = 0; // for COGS reversal

            foreach (var item in request.Items)
            {
                var invItem = item.ItemId.HasValue
                    ? invoice.Items.FirstOrDefault(ii => ii.Id == item.ItemId.Value)
                    : invoice.Items.FirstOrDefault(ii => ii.ProductId == item.ProductId);
                if (invItem == null) throw new InvalidOperationException($"Invoice item was not found on original invoice.");

                var product = await _context.Products
                    .Include(p => p.TaxSlab)
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken);
                if (product == null) throw new InvalidOperationException($"Product with ID {item.ProductId} not found.");

                ProductBatch? batch = null;
                if (item.BatchId.HasValue && item.BatchId.Value != Guid.Empty)
                {
                    batch = await _context.ProductBatches.FindAsync(new object[] { item.BatchId.Value }, cancellationToken);
                    if (batch == null) throw new InvalidOperationException($"Product batch with ID {item.BatchId} not found.");
                }
                else
                {
                    // If no specific batch provided, find any available batch for restock
                    batch = await _context.ProductBatches
                        .Where(b => b.ProductId == item.ProductId)
                        .OrderBy(b => b.CreatedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                    
                    if (batch == null)
                        throw new InvalidOperationException(
                            $"BATCH_NOT_FOUND: No batch found for product {item.ProductId}. Return rejected.");
                }

                // Validate original quantities
                if (invItem.Quantity < item.Quantity)
                {
                    throw new InvalidOperationException($"Returned quantity exceeds original invoice quantity. Original: {invItem.Quantity}, Returned: {item.Quantity}");
                }

                decimal cgstRate = invItem.CgstRate;
                decimal sgstRate = invItem.SgstRate;
                decimal cessRate = invItem.CessRate;
                decimal totalTaxRate = cgstRate + sgstRate + cessRate;

                // Price is tax-inclusive in retail invoicing usually.
                // We compute the tax amount on the returned portion
                decimal lineTotal = invItem.UnitPrice * item.Quantity; // final total returned
                decimal lineSubTotal = totalTaxRate > 0 
                    ? Math.Round(lineTotal / (1 + (totalTaxRate / 100m)), 2)
                    : lineTotal;
                decimal lineTax = lineTotal - lineSubTotal;

                salesReturn.Items.Add(new SalesReturnItem
                {
                    ProductId = item.ProductId,
                    BatchId = batch.Id,
                    Quantity = item.Quantity,
                    UnitPrice = invItem.UnitPrice,
                    TaxAmount = lineTax,
                    TotalAmount = lineTotal
                });

                // Re-add physical batch quantities (restock returned items)
                batch.AvailableQuantity += item.Quantity;

                // Record stock movement (positive for return/restock)
                await _stockLedgerService.RecordMovementAsync(
                    storeId: request.StoreId,
                    warehouseId: null,
                    terminalId: null,
                    businessDate: request.ReturnDate.Date,
                    productId: item.ProductId,
                    batchId: batch.Id,
                    movementType: "SALES_RETURN",
                    quantity: item.Quantity,
                    unitCost: batch.CostPrice,
                    expiryDate: batch.ExpiryDate,
                    referenceDocId: salesReturn.Id,
                    referenceNumber: returnNo,
                    userId: request.UserId,
                    cancellationToken: cancellationToken
                );

                totalSubTotal += lineSubTotal;
                totalTax += lineTax;
                totalAmount += lineTotal;
                totalCostReversal += (batch.CostPrice * item.Quantity);
            }

            salesReturn.SubTotal = totalSubTotal;
            salesReturn.TaxAmount = totalTax;
            salesReturn.TotalAmount = totalAmount;
            salesReturn.RefundAmount = totalAmount;

            _context.SalesReturns.Add(salesReturn);
            await _context.SaveChangesAsync(cancellationToken);

            // Double Entry Journal:
            // 1. Revenue & Refund Reversal:
            // Debit Sales Revenue 4000 (SubTotal)
            // Debit Output CGST 2200
            // Debit Output SGST 2201
            // Credit Refund Account (Cash 1000, Bank 1100, or Customer WALLET/AR 20200)
            decimal cgstReversal = Math.Round(totalTax / 2m, 2);
            decimal sgstReversal = totalTax - cgstReversal;

            string resolvedRefundAccountCode = "10100";
            if (request.RefundMode == "UPI")
            {
                resolvedRefundAccountCode = await ResolveAccountCodeAsync("ASSET", "Current", _configuration?["Finance:AccountDefaults:DigitalBank"] ?? "10200", cancellationToken);
            }
            else if (request.RefundMode == "CREDIT_NOTE")
            {
                resolvedRefundAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Wallet", _configuration?["Finance:AccountDefaults:WalletLiability"] ?? "20200", cancellationToken);
            }
            else
            {
                resolvedRefundAccountCode = await ResolveAccountCodeAsync("ASSET", "Cash", _configuration?["Finance:AccountDefaults:Cash"] ?? "10100", cancellationToken);
            }

            string salesAccountCode = await ResolveAccountCodeAsync("REVENUE", "Sales Revenue", _configuration?["Finance:AccountDefaults:SalesRevenue"] ?? "40100", cancellationToken);
            string outputCgstAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Output CGST", _configuration?["Finance:AccountDefaults:OutputCGST"] ?? "22010", cancellationToken);
            string outputSgstAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Output SGST", _configuration?["Finance:AccountDefaults:OutputSGST"] ?? "22020", cancellationToken);
            string inventoryAccountCode = await ResolveAccountCodeAsync("ASSET", "Inventory Asset", _configuration?["Finance:AccountDefaults:Inventory"] ?? "10300", cancellationToken);
            string cogsAccountCode = await ResolveAccountCodeAsync("EXPENSE", "Cost of Goods Sold", _configuration?["Finance:AccountDefaults:Cogs"] ?? "50100", cancellationToken);

            var journalLines = new List<JournalLineDto>
            {
                new() { AccountCode = salesAccountCode, Description = $"Sales Return revenue reversal {returnNo}", Debit = totalSubTotal, Credit = 0 },
                new() { AccountCode = resolvedRefundAccountCode, Description = $"Refund for return {returnNo}", Debit = 0, Credit = totalAmount }
            };

            if (cgstReversal > 0)
                journalLines.Add(new() { AccountCode = outputCgstAccountCode, Description = $"Output CGST Reversal for return {returnNo}", Debit = cgstReversal, Credit = 0 });
            if (sgstReversal > 0)
                journalLines.Add(new() { AccountCode = outputSgstAccountCode, Description = $"Output SGST Reversal for return {returnNo}", Debit = sgstReversal, Credit = 0 });

            // 2. COGS & Inventory Reversal:
            // Debit Inventory Asset 10300 (totalCostReversal)
            // Credit COGS 5000 (totalCostReversal)
            if (totalCostReversal > 0)
            {
                journalLines.Add(new() { AccountCode = inventoryAccountCode, Description = $"Inventory restock from return {returnNo}", Debit = totalCostReversal, Credit = 0 });
                journalLines.Add(new() { AccountCode = cogsAccountCode, Description = $"COGS reversal for return {returnNo}", Debit = 0, Credit = totalCostReversal });
            }

            Guid jeId = await _postingService.PostJournalEntryWithUserAsync(
                request.StoreId,
                request.ReturnDate,
                $"Sales Return matching Invoice {invoice.InvoiceNumber} ({returnNo})",
                returnNo,
                journalLines,
                request.UserId,
                isDraft: false,
                cancellationToken,
                sourceModule: "POS",
                sourceDocType: "SALES_RETURN",
                sourceDocId: salesReturn.Id
            );

            salesReturn.JournalEntryId = jeId;

            // Record GST Transaction (reduction in output taxes/sales)
            await _postingService.RecordGstTransactionAsync(
                request.StoreId,
                "SALES_RETURN",
                returnNo,
                request.ReturnDate,
                -totalSubTotal,
                -cgstReversal,
                -sgstReversal,
                0,
                null,
                cancellationToken
            );

            // If Customer and CREDIT_NOTE, post to Customer Ledger (Credit customer, reduces outstanding receivables)
            if (invoice.CustomerId.HasValue && request.RefundMode == "CREDIT_NOTE")
            {
                var customer = await _context.Customers.FindAsync(new object[] { invoice.CustomerId.Value }, cancellationToken);
                if (customer != null)
                {
                    decimal runningBalance = await _context.CustomerLedger
                        .Where(c => c.CustomerId == invoice.CustomerId.Value && c.StoreId == request.StoreId)
                        .OrderByDescending(c => c.CreatedAt)
                        .Select(c => c.RunningBalance)
                        .FirstOrDefaultAsync(cancellationToken);

                    runningBalance -= totalAmount; // Credit reduces AR balance

                    var ledgerEntry = new CustomerLedgerEntry
                    {
                        StoreId = request.StoreId,
                        CustomerId = invoice.CustomerId.Value,
                        EntryDate = request.ReturnDate.Date,
                        TransactionType = "CREDIT_NOTE",
                        ReferenceNumber = returnNo,
                        DebitAmount = 0,
                        CreditAmount = totalAmount,
                        RunningBalance = runningBalance,
                        Description = $"Sales Return Credit Note {returnNo} posted",
                        JournalEntryId = jeId,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.CustomerLedger.Add(ledgerEntry);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return salesReturn.Id;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<string> ResolveAccountCodeAsync(string accountType, string namePattern, string fallbackCode, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .Where(a => a.IsActive && a.AccountType == accountType &&
                        !IAccountResolutionService.LegacyExcludedCodes.Contains(a.AccountCode) &&
                        !_context.Accounts.Any(sub => sub.ParentAccountId == a.Id && sub.IsActive))
            .OrderByDescending(a => a.AccountCode.Length)
            .ThenBy(a => a.AccountCode)
            .ToListAsync(cancellationToken);

        var matched = account.FirstOrDefault(a => a.Name.Equals(namePattern, StringComparison.OrdinalIgnoreCase))
                   ?? account.FirstOrDefault(a => a.Name.Contains(namePattern, StringComparison.OrdinalIgnoreCase))
                   ?? account.FirstOrDefault(a => a.AccountCode == fallbackCode);

        return matched?.AccountCode ?? fallbackCode;
    }
}
