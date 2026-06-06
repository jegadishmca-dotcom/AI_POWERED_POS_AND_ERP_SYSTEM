using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Crm;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Crm.Services;

public interface IWalletService
{
    Task<decimal> RecordTransactionAsync(Guid customerId, Guid? storeId, string transactionType, decimal amount, string referenceDocument, Guid? userId, CancellationToken cancellationToken);
}

public class WalletService : IWalletService
{
    private readonly IApplicationDbContext _context;

    public WalletService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> RecordTransactionAsync(Guid customerId, Guid? storeId, string transactionType, decimal amount, string referenceDocument, Guid? userId, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers.FindAsync(new object[] { customerId }, cancellationToken);
        if (customer == null) throw new Exception("Customer not found.");

        // BUG-06 FIX: Use SUM(Amount) across all ledger entries as the authoritative balance.
        // The previous approach read the last entry's RunningBalance, which is stale under concurrent
        // wallet redemptions (two simultaneous SPENDs could both pass the overdraft check using the
        // same pre-deduction balance). Summing is always consistent with the actual ledger state.
        decimal currentBalance = await _context.WalletLedger
            .Where(w => w.CustomerId == customerId)
            .SumAsync(w => w.Amount, cancellationToken);
        
        // Ensure spend doesn't exceed balance
        if (transactionType == "SPEND" && currentBalance + amount < 0)
        {
            throw new Exception("Insufficient wallet balance.");
        }

        decimal newBalance = currentBalance + amount;

        var ledgerEntry = new WalletLedgerEntry
        {
            CustomerId = customerId,
            StoreId = storeId,
            TransactionType = transactionType, // TOPUP (+), SPEND (-), REFUND (+)
            Amount = amount,
            ReferenceDocument = referenceDocument,
            RunningBalance = newBalance,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.WalletLedger.Add(ledgerEntry);
        
        // Update denormalized balance on Customer for fast UI reads
        customer.RunningWalletBalance = newBalance;

        return newBalance;
    }
}
