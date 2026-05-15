using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Crm;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Crm.Services;

public interface ILoyaltyService
{
    Task<decimal> RecordPointsAsync(Guid customerId, Guid? storeId, string transactionType, decimal points, string referenceDocument, Guid? userId, CancellationToken cancellationToken);
    Task CalculateAndAwardPointsForInvoiceAsync(Guid invoiceId, Guid customerId, decimal invoiceTotal, CancellationToken cancellationToken);
}

public class LoyaltyService : ILoyaltyService
{
    private readonly IApplicationDbContext _context;

    public LoyaltyService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> RecordPointsAsync(Guid customerId, Guid? storeId, string transactionType, decimal points, string referenceDocument, Guid? userId, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers.FindAsync(new object[] { customerId }, cancellationToken);
        if (customer == null) throw new Exception("Customer not found.");

        var lastEntry = await _context.LoyaltyLedger
            .Where(l => l.CustomerId == customerId)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        decimal currentPoints = lastEntry?.RunningPoints ?? 0;

        if (transactionType == "BURN" && currentPoints + points < 0)
        {
            throw new Exception("Insufficient loyalty points.");
        }

        decimal newPoints = currentPoints + points;

        var entry = new LoyaltyLedgerEntry
        {
            CustomerId = customerId,
            StoreId = storeId,
            TransactionType = transactionType, // EARN (+), BURN (-), EXPIRED (-)
            Points = points,
            ReferenceDocument = referenceDocument,
            ExpiryDate = transactionType == "EARN" ? DateTime.UtcNow.AddDays(365) : null,
            RunningPoints = newPoints,
            CreatedBy = userId
        };

        _context.LoyaltyLedger.Add(entry);
        customer.RunningLoyaltyPoints = newPoints;

        return newPoints;
    }

    public async Task CalculateAndAwardPointsForInvoiceAsync(Guid invoiceId, Guid customerId, decimal invoiceTotal, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers.Include(c => c.Tier).FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer == null) return;

        // Base Earn Rate: e.g. 1 point per 100 Rupees
        decimal basePoints = invoiceTotal / 100m;
        
        // Apply Tier Multiplier
        decimal multiplier = customer.Tier?.PointsEarnMultiplier ?? 1.0m;
        decimal earnedPoints = Math.Floor(basePoints * multiplier);

        if (earnedPoints > 0)
        {
            await RecordPointsAsync(customerId, null, "EARN", earnedPoints, $"INV-{invoiceId}", null, cancellationToken);
        }
    }
}
