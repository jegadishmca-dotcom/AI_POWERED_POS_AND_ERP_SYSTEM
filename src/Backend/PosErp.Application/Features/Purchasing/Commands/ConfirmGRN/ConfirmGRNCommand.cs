using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Purchasing;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PosErp.Application.Features.Finance.Services;
using PosErp.Domain.Entities.Finance;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace PosErp.Application.Features.Purchasing.Commands.ConfirmGRN;

public record ConfirmGRNCommand(Guid GrnId, Guid? UserId) : IRequest<bool>;

public class ConfirmGRNCommandHandler : IRequestHandler<ConfirmGRNCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IStockLedgerService _stockLedgerService;
    private readonly IProductBatchService _batchService;
    private readonly IFinancialPostingService _financialPostingService;

    public ConfirmGRNCommandHandler(
        IApplicationDbContext context, 
        IStockLedgerService stockLedgerService, 
        IProductBatchService batchService,
        IFinancialPostingService financialPostingService)
    {
        _context = context;
        _stockLedgerService = stockLedgerService;
        _batchService = batchService;
        _financialPostingService = financialPostingService;
    }

    public async Task<bool> Handle(ConfirmGRNCommand request, CancellationToken cancellationToken)
    {
        var strategy = ((DbContext)_context).Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
            try 
            {
                var grn = await _context.GRNHeaders
                    .Include(g => g.Items)
                    .FirstOrDefaultAsync(g => g.Id == request.GrnId, cancellationToken);

                if (grn == null || grn.Status != "DRAFT") throw new Exception("Invalid GRN or not in DRAFT status");

                var po = await _context.PurchaseOrders
                    .Include(p => p.Items)
                    .FirstOrDefaultAsync(p => p.Id == grn.PurchaseOrderHeaderId, cancellationToken);
                    
                if (po == null) throw new Exception("Purchase Order not found");

                foreach (var item in grn.Items)
                {
                    if (item.AcceptedQuantity <= 0) continue;

                    // 1. Safe Batch Generation using proper Service (enforces HasExpiry check)
                    var batch = await _batchService.CreateOrGetBatchAsync(
                        grn.StoreId ?? Guid.Empty,
                        item.ProductId,
                        item.BatchNumber,
                        item.MfgDate,
                        item.ExpiryDate,
                        item.UnitCost,
                        cancellationToken
                    );

                    item.BatchId = batch.Id; 

                    // 2. Increment PO Received Quantity 
                    var poItem = po.Items.FirstOrDefault(p => p.Id == item.PurchaseOrderItemId);
                    if (poItem != null) poItem.ReceivedQuantity += item.AcceptedQuantity;

                    // 3. Record stock movement
                    await _stockLedgerService.RecordMovementAsync(
                        storeId: grn.StoreId ?? Guid.Empty,
                        warehouseId: null,
                        terminalId: null,
                        businessDate: grn.ReceivedDate,
                        productId: item.ProductId,
                        batchId: batch.Id,
                        movementType: "GRN",
                        quantity: item.AcceptedQuantity, 
                        unitCost: item.UnitCost,
                        expiryDate: item.ExpiryDate,
                        referenceDocId: grn.Id,
                        referenceNumber: grn.GrnNumber,
                        userId: request.UserId,
                        cancellationToken: cancellationToken
                    );
                }

                grn.Status = "CONFIRMED";
                
                bool isFullReceipt = po.Items.All(p => p.ReceivedQuantity >= p.OrderedQuantity);
                po.Status = isFullReceipt ? "FULL_GRN" : "PARTIAL_GRN";

                // --- NEW FINANCIAL POSTING ---
                var supplier = await _context.Suppliers.FindAsync(new object[] { grn.SupplierId }, cancellationToken);
                if (supplier == null) throw new Exception("Supplier not found");

                int creditDays = 30; // default
                if (!string.IsNullOrEmpty(supplier.PaymentTerms))
                {
                    var match = Regex.Match(supplier.PaymentTerms, @"\d+");
                    if (match.Success)
                    {
                        int.TryParse(match.Value, out creditDays);
                    }
                }

                DateTime dueDate = grn.ReceivedDate.AddDays(creditDays);
                decimal itemsCost = grn.Items.Sum(i => i.TotalCost);
                decimal cgstRate = 0.09m;
                decimal sgstRate = 0.09m;
                decimal cgstAmount = itemsCost * cgstRate;
                decimal sgstAmount = itemsCost * sgstRate;
                decimal totalAmount = itemsCost + cgstAmount + sgstAmount;

                var billNumber = string.IsNullOrWhiteSpace(grn.SupplierInvoiceNumber) ? $"BILL-{grn.GrnNumber}" : grn.SupplierInvoiceNumber;

                var bill = new PurchaseBillHeader
                {
                    StoreId = grn.StoreId,
                    SupplierId = grn.SupplierId,
                    GRNHeaderId = grn.Id,
                    BillNumber = billNumber,
                    BillDate = grn.ReceivedDate.Date,
                    DueDate = dueDate.Date,
                    SubTotal = itemsCost,
                    TaxAmount = cgstAmount + sgstAmount,
                    TotalAmount = totalAmount,
                    Status = "PENDING_PAYMENT",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.UserId
                };

                foreach (var item in grn.Items)
                {
                    bill.Items.Add(new PurchaseBillItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.AcceptedQuantity,
                        UnitCost = item.UnitCost,
                        TaxAmount = item.TotalCost * (cgstRate + sgstRate),
                        TotalAmount = item.TotalCost * (1 + cgstRate + sgstRate)
                    });
                }

                _context.PurchaseBills.Add(bill);
                await _context.SaveChangesAsync(cancellationToken);

                // Post double-entry journal entry:
                string inventoryAccountCode = await ResolveAccountCodeAsync("ASSET", "Inventory", "10300", cancellationToken);
                string inputCgstAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Input CGST", "22030", cancellationToken);
                string inputSgstAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Input SGST", "22040", cancellationToken);
                string apAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Accounts Payable", "20100", cancellationToken);

                var lines = new List<JournalLineDto>
                {
                    new() { AccountCode = inventoryAccountCode, Description = $"Inventory addition from GRN {grn.GrnNumber}", Debit = itemsCost, Credit = 0 },
                    new() { AccountCode = inputCgstAccountCode, Description = $"Input CGST on Bill {billNumber}", Debit = cgstAmount, Credit = 0 },
                    new() { AccountCode = inputSgstAccountCode, Description = $"Input SGST on Bill {billNumber}", Debit = sgstAmount, Credit = 0 },
                    new() { AccountCode = apAccountCode, Description = $"Accounts Payable vendor {supplier.Name}", Debit = 0, Credit = totalAmount }
                };

                Guid jeId = await _financialPostingService.PostJournalEntryWithUserAsync(
                    grn.StoreId,
                    grn.ReceivedDate,
                    $"Supplier Purchase Bill {billNumber} matching GRN {grn.GrnNumber}",
                    billNumber,
                    lines,
                    request.UserId,
                    isDraft: false,
                    cancellationToken,
                    sourceModule: "PURCHASING",
                    sourceDocType: "PURCHASE_BILL",
                    sourceDocId: bill.Id
                );

                // Add record to supplier ledger (Credit vendor)
                decimal runningBalance = await _context.SupplierLedger
                    .Where(s => s.SupplierId == grn.SupplierId && s.StoreId == grn.StoreId)
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => s.RunningBalance)
                    .FirstOrDefaultAsync(cancellationToken);

                runningBalance += totalAmount; // Credit increases accounts payable liability

                var ledgerEntry = new SupplierLedgerEntry
                {
                    StoreId = grn.StoreId ?? Guid.Empty,
                    SupplierId = grn.SupplierId,
                    EntryDate = grn.ReceivedDate.Date,
                    TransactionType = "BILL",
                    ReferenceNumber = billNumber,
                    DebitAmount = 0,
                    CreditAmount = totalAmount,
                    RunningBalance = runningBalance,
                    Description = $"Purchase Bill {billNumber} matched to GRN {grn.GrnNumber}",
                    JournalEntryId = jeId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.SupplierLedger.Add(ledgerEntry);
                // -----------------------------

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new Exception("Concurrency conflict during GRN confirmation. Please retry.", ex);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<string> ResolveAccountCodeAsync(string accountType, string namePattern, string fallbackCode, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .Where(a => a.IsActive && a.AccountType == accountType)
            .ToListAsync(cancellationToken);

        var matched = account.FirstOrDefault(a => a.Name.Contains(namePattern, StringComparison.OrdinalIgnoreCase))
                   ?? account.FirstOrDefault(a => a.AccountCode == fallbackCode);

        return matched?.AccountCode ?? fallbackCode;
    }
}
