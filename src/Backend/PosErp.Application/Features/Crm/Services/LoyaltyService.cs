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
    Task<decimal> RecordPointsAsync(Guid customerId, Guid? storeId, string transactionType, decimal pointsEarned, decimal pointsRedeemed, string referenceDocument, string remarks, Guid? invoiceId, Guid? userId, CancellationToken cancellationToken);
    Task CalculateAndAwardPointsForInvoiceAsync(Guid invoiceId, Guid customerId, decimal invoiceTotal, CancellationToken cancellationToken);
}

public class LoyaltyService : ILoyaltyService
{
    private readonly IApplicationDbContext _context;

    public LoyaltyService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> RecordPointsAsync(Guid customerId, Guid? storeId, string transactionType, decimal pointsEarned, decimal pointsRedeemed, string referenceDocument, string remarks, Guid? invoiceId, Guid? userId, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers.FindAsync(new object[] { customerId }, cancellationToken);
        if (customer == null) throw new Exception("Customer not found.");

        if (customer.MembershipStatus == "Blocked")
        {
            if (pointsRedeemed > 0) throw new Exception("Blocked customers cannot redeem points.");
        }

        var config = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync(cancellationToken) ?? new LoyaltyProgramConfig();

        decimal currentPoints = customer.RunningLoyaltyPoints;

        if (pointsRedeemed > 0 && currentPoints < pointsRedeemed)
        {
            throw new Exception("Insufficient loyalty points.");
        }

        decimal newPoints = currentPoints + pointsEarned - pointsRedeemed;

        var entry = new LoyaltyLedgerEntry
        {
            CustomerId = customerId,
            StoreId = storeId,
            TransactionType = transactionType, // Earn Points, Redeem Points, Manual Adjustment, etc.
            PointsEarned = pointsEarned,
            PointsRedeemed = pointsRedeemed,
            PreviousBalance = currentPoints,
            BalanceAfterTransaction = newPoints,
            ReferenceDocument = referenceDocument,
            InvoiceId = invoiceId,
            Remarks = remarks,
            ExpiryDate = (pointsEarned > 0 && config != null && config.EnablePointExpiry) 
                         ? DateTime.UtcNow.AddMonths(config.ExpiryMonths) 
                         : null,
            CreatedBy = userId
        };

        _context.LoyaltyLedger.Add(entry);
        customer.RunningLoyaltyPoints = newPoints;
        customer.LifetimePointsEarned += pointsEarned;
        
        if (pointsEarned > 0) customer.LastPointsEarnedDate = DateTime.UtcNow;
        if (pointsRedeemed > 0) customer.LastRedemptionDate = DateTime.UtcNow;

        return newPoints;
    }

    public async Task CalculateAndAwardPointsForInvoiceAsync(Guid invoiceId, Guid customerId, decimal invoiceTotal, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers.Include(c => c.Tier).FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer == null) return;

        var config = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync(cancellationToken) ?? new LoyaltyProgramConfig();
        
        decimal earnRatioSpendAmount = config.EarnRatioSpendAmount > 0 ? config.EarnRatioSpendAmount : 100m;
        decimal basePoints = (invoiceTotal / earnRatioSpendAmount) * config.EarnRatioPoints;
        
        // Apply Tier Multiplier
        decimal multiplier = customer.Tier?.PointsEarnMultiplier ?? 1.0m;
        decimal earnedPoints = Math.Floor(basePoints * multiplier);

        if (earnedPoints > 0)
        {
            await RecordPointsAsync(customerId, null, "Earn Points", earnedPoints, 0, $"INV-{invoiceId}", "Points earned from purchase", invoiceId, null, cancellationToken);
        }
        
        // Auto Tier Evaluation (Upgrade only on checkout)
        if (config.EnableAutoTierEvaluation)
        {
            var tiers = await _context.CustomerTiers.OrderByDescending(t => t.MinimumSpend).ToListAsync(cancellationToken);
            foreach (var tier in tiers)
            {
                if (customer.LifetimeSpend >= tier.MinimumSpend)
                {
                    if (customer.CustomerTierId != tier.Id)
                    {
                        customer.CustomerTierId = tier.Id;
                        await RecordPointsAsync(customerId, null, "Tier Upgrade Bonus", 0, 0, "", $"Upgraded to {tier.Name}", null, null, cancellationToken);
                        // In real system, we'd trigger INotificationService here
                    }
                    break;
                }
            }
        }
    }
}
