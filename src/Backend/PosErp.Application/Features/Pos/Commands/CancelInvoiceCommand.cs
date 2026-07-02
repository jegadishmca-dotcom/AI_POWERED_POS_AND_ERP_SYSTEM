using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Finance.Services;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Application.Features.Crm.Services;
using PosErp.Domain.Entities.Finance;
using PosErp.Domain.Entities.Crm;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PosErp.Application.Features.Finance.Commands;

namespace PosErp.Application.Features.Pos.Commands;

public record CancelInvoiceCommand(
    Guid InvoiceId,
    Guid UserId
) : IRequest<bool>;

public class CancelInvoiceCommandHandler : IRequestHandler<CancelInvoiceCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IFinancialPostingService _postingService;
    private readonly IStockLedgerService _stockLedgerService;
    private readonly IWalletService _walletService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly IPeriodLockService _periodLockService;

    public CancelInvoiceCommandHandler(
        IApplicationDbContext context,
        IFinancialPostingService postingService,
        IStockLedgerService stockLedgerService,
        IWalletService walletService,
        ILoyaltyService loyaltyService,
        IPeriodLockService periodLockService)
    {
        _context = context;
        _postingService = postingService;
        _stockLedgerService = stockLedgerService;
        _walletService = walletService;
        _loyaltyService = loyaltyService;
        _periodLockService = periodLockService;
    }

    public async Task<bool> Handle(CancelInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice == null)
            throw new InvalidOperationException("Invoice not found.");

        if (invoice.Status == "CANCELLED")
            throw new InvalidOperationException("Invoice is already cancelled.");

        if (invoice.Status == "HOLD")
            throw new InvalidOperationException("Held invoices cannot be cancelled via this endpoint.");

        // Guard: Block if there are any returns processed for this invoice
        var hasReturns = await _context.SalesReturns
            .AnyAsync(sr => sr.InvoiceId == invoice.Id, cancellationToken);
        if (hasReturns)
        {
            throw new InvalidOperationException(
                "This invoice has an active sales return. Process remaining item returns individually rather than cancelling the invoice.");
        }

        // Period lock check - validated on the date the reversal is actually posted (today)
        if (!invoice.StoreId.HasValue)
            throw new InvalidOperationException("Invoice has no store assignment and cannot be cancelled.");
        Guid storeId = invoice.StoreId.Value;
        await _periodLockService.CheckPeriodLockAsync(storeId, DateTime.UtcNow.Date, cancellationToken);

        var strategy = ((DbContext)_context).Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // 1. Update status
                invoice.Status = "CANCELLED";
                invoice.UpdatedAt = DateTime.UtcNow;
                invoice.UpdatedBy = request.UserId;

                // 2. Stock Restoration via StockLedgerService.RecordMovementAsync
                var originalMovements = await _context.StockLedger
                    .Where(sl => sl.ReferenceDocumentId == invoice.Id && (sl.MovementType == "SALE" || sl.MovementType == "SALE_OVERRIDE" || sl.MovementType == "SALE_OFFLINE_FORCED"))
                    .ToListAsync(cancellationToken);

                foreach (var movement in originalMovements)
                {
                    // Call RecordMovementAsync (acquires product row-level lock and inserts StockLedger Entry)
                    await _stockLedgerService.RecordMovementAsync(
                        storeId: storeId,
                        warehouseId: movement.WarehouseId,
                        terminalId: movement.TerminalId,
                        businessDate: DateTime.UtcNow.Date,
                        productId: movement.ProductId,
                        batchId: movement.BatchId,
                        movementType: "SALE_CANCEL",
                        quantity: Math.Abs(movement.Quantity), // Positive quantity to restore stock
                        unitCost: movement.UnitCost,
                        expiryDate: movement.ExpiryDate,
                        referenceDocId: invoice.Id,
                        referenceNumber: $"CAN-{invoice.InvoiceNumber}",
                        userId: request.UserId,
                        cancellationToken: cancellationToken
                    );

                    // Re-add physical batch quantities (restock cancelled items)
                    if (movement.BatchId.HasValue)
                    {
                        var batch = await _context.ProductBatches.FindAsync(new object[] { movement.BatchId.Value }, cancellationToken);
                        if (batch != null)
                        {
                            batch.AvailableQuantity += Math.Abs(movement.Quantity);
                        }
                    }
                }

                // 3. Loyalty Points Reversal
                if (invoice.CustomerId.HasValue)
                {
                    var customer = await _context.Customers.FindAsync(new object[] { invoice.CustomerId.Value }, cancellationToken);
                    if (customer != null)
                    {
                        var loyaltyEntries = await _context.LoyaltyLedger
                            .Where(l => l.InvoiceId == invoice.Id)
                            .ToListAsync(cancellationToken);

                        decimal earnedPoints = loyaltyEntries.Sum(l => l.PointsEarned);
                        decimal redeemedPoints = loyaltyEntries.Sum(l => l.PointsRedeemed);

                        if (earnedPoints > 0)
                        {
                            // Revert earned points through LoyaltyService using same record method (negated earnedPoints, 0 redeemed)
                            await _loyaltyService.RecordPointsAsync(
                                customer.Id,
                                storeId,
                                "Cancel Earned Points",
                                -earnedPoints,
                                0,
                                invoice.InvoiceNumber,
                                $"Reversal of points earned for cancelled invoice {invoice.InvoiceNumber}.",
                                invoice.Id,
                                request.UserId,
                                cancellationToken
                            );
                        }

                        if (redeemedPoints > 0)
                        {
                            // Refund/restore redeemed points using RecordPointsAsync (always safe)
                            await _loyaltyService.RecordPointsAsync(
                                customer.Id, 
                                storeId, 
                                "Refund Points", 
                                redeemedPoints, 
                                0, 
                                invoice.InvoiceNumber, 
                                $"Refund of points redeemed for cancelled invoice {invoice.InvoiceNumber}.", 
                                invoice.Id, 
                                request.UserId, 
                                cancellationToken
                            );
                        }
                    }
                }

                // 4. Wallet Reversal (Refund SPEND transactions)
                if (invoice.CustomerId.HasValue && invoice.WalletAmount > 0)
                {
                    // Refund the wallet amount paid
                    await _walletService.RecordTransactionAsync(
                        invoice.CustomerId.Value,
                        storeId,
                        "REFUND",
                        invoice.WalletAmount, // positive amount to restore balance
                        invoice.InvoiceNumber,
                        request.UserId,
                        cancellationToken
                    );
                }

                // 5. Customer Credit Sale AR Reversal
                // If this was a credit sale, reverse the customer ledger entry
                decimal creditSaleAmount = invoice.NetPayable - invoice.CashAmount - invoice.UpiAmount - invoice.CardAmount - invoice.WalletAmount;
                if (creditSaleAmount > 0 && invoice.CustomerId.HasValue)
                {
                    var customer = await _context.Customers.FindAsync(new object[] { invoice.CustomerId.Value }, cancellationToken);
                    if (customer != null)
                    {
                        decimal currentLedgerBal = await _context.CustomerLedger
                            .Where(c => c.CustomerId == customer.Id)
                            .OrderByDescending(c => c.CreatedAt)
                            .Select(c => c.RunningBalance)
                            .FirstOrDefaultAsync(cancellationToken);

                        currentLedgerBal -= creditSaleAmount;

                        var ledgerEntry = new CustomerLedgerEntry
                        {
                            StoreId = storeId,
                            CustomerId = customer.Id,
                            EntryDate = DateTime.UtcNow.Date,
                            TransactionType = "CANCELLATION",
                            ReferenceNumber = invoice.InvoiceNumber,
                            DebitAmount = 0,
                            CreditAmount = creditSaleAmount, // Reversal is a Credit to AR
                            RunningBalance = currentLedgerBal,
                            Description = $"AR Reversal for Cancelled Invoice {invoice.InvoiceNumber}",
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.CustomerLedger.Add(ledgerEntry);
                    }
                }

                // 6. Journal Reversal (Offsetting Swapped Entry)
                var originalJournal = await _context.JournalEntries
                    .FirstOrDefaultAsync(je => je.ReferenceDocument == $"INV-{invoice.Id}", cancellationToken);

                if (originalJournal != null)
                {
                    // Query posted lines to swap debits/credits
                    var originalLines = await (from jel in _context.JournalEntryLines
                                               join acc in _context.Accounts on jel.AccountId equals acc.Id
                                               where jel.JournalEntryId == originalJournal.Id
                                               select new JournalLineDto
                                               {
                                                   AccountCode = acc.AccountCode,
                                                   Description = $"Reversal of: {jel.Description}",
                                                   Debit = jel.CreditAmount,  // Original credit becomes debit
                                                   Credit = jel.DebitAmount,  // Original debit becomes credit
                                                   CostCenterId = jel.CostCenterId
                                               })
                                               .ToListAsync(cancellationToken);

                    if (originalLines.Count > 0)
                    {
                        await _postingService.PostJournalEntryAsync(
                            storeId: storeId,
                            date: DateTime.UtcNow.Date,
                            description: $"Reversal of POS Invoice {invoice.InvoiceNumber}",
                            refDoc: $"CAN-{invoice.InvoiceNumber}",
                            lines: originalLines,
                            cancellationToken: cancellationToken
                        );
                    }
                }

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
