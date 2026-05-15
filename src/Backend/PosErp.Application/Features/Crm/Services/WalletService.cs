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

        // Fetch latest ledger entry to safely calculate running balance
        var lastEntry = await _context.WalletLedger
            .Where(w => w.CustomerId == customerId)
            .OrderByDescending(w => w.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        decimal currentBalance = lastEntry?.RunningBalance ?? 0;
        
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
