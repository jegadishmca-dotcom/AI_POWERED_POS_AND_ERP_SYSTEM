using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Crm;

namespace PosErp.Application.Features.Loyalty.Jobs;

public interface ILoyaltyBackgroundJobs
{
    Task ExpirePointsJob();
    Task EvaluateTierDowngradeJob();
    Task BirthdayBonusJob();
    Task AnniversaryBonusJob();
    Task LoyaltyMaintenanceJob();
}

public class LoyaltyBackgroundJobs : ILoyaltyBackgroundJobs
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<LoyaltyBackgroundJobs> _logger;
    private readonly INotificationService _notificationService;

    public LoyaltyBackgroundJobs(
        IApplicationDbContext context,
        ILogger<LoyaltyBackgroundJobs> logger,
        INotificationService notificationService)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task ExpirePointsJob()
    {
        _logger.LogInformation("Starting Point Expiration Job");

        var config = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync();
        if (config == null || !config.IsActiveConfig || config.ExpiryMonths <= 0)
        {
            _logger.LogInformation("Point expiration is disabled or not configured.");
            return;
        }

        var expiryCutoff = DateTime.UtcNow.AddMonths(-config.ExpiryMonths);

        var customersToExpire = await _context.Customers
            .Where(c => c.RunningLoyaltyPoints > 0)
            .ToListAsync();

        int processedCount = 0;
        foreach (var customer in customersToExpire)
        {
            var lastActivity = await _context.LoyaltyLedger
                .Where(l => l.CustomerId == customer.Id)
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastActivity != null && lastActivity.CreatedAt < expiryCutoff)
            {
                var expiredAmount = customer.RunningLoyaltyPoints;
                var previousBalance = customer.RunningLoyaltyPoints;
                customer.RunningLoyaltyPoints = 0;

                var ledgerEntry = new LoyaltyLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    TransactionType = "Expiration",
                    PointsEarned = 0,
                    PointsRedeemed = expiredAmount,
                    PreviousBalance = previousBalance,
                    BalanceAfterTransaction = 0,
                    CreatedAt = DateTime.UtcNow,
                    Remarks = $"Points expired due to inactivity since {lastActivity.CreatedAt:yyyy-MM-dd}"
                };

                _context.LoyaltyLedger.Add(ledgerEntry);
                
                await _notificationService.SendSmsAsync(customer.Phone, $"Your {expiredAmount} loyalty points have expired due to inactivity.");
                processedCount++;
            }
        }

        await _context.SaveChangesAsync(CancellationToken.None);
        _logger.LogInformation($"Point Expiration Job completed. Processed {processedCount} customers.");
    }

    public async Task EvaluateTierDowngradeJob()
    {
        _logger.LogInformation("Starting Tier Downgrade Job");

        var config = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync();
        if (config == null || !config.IsActiveConfig || !config.EnableAutoTierEvaluation) return;

        var tiers = await _context.CustomerTiers.OrderByDescending(t => t.Level).ToListAsync();
        if (!tiers.Any()) return;

        var customers = await _context.Customers.Include(c => c.Tier).ToListAsync();
        int downgradedCount = 0;

        foreach (var customer in customers)
        {
            var oldTierName = customer.Tier?.Name ?? "Base";
            var currentTier = customer.Tier;
            
            var totalSpend = customer.LifetimeSpend; 
            
            var correctTier = tiers.FirstOrDefault(t => totalSpend >= t.MinimumSpend) ?? tiers.Last();

            if (currentTier != null && correctTier.Level < currentTier.Level)
            {
                customer.CustomerTierId = correctTier.Id;
                
                var ledgerEntry = new LoyaltyLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    TransactionType = "TierDowngrade",
                    PointsEarned = 0,
                    PointsRedeemed = 0,
                    PreviousBalance = customer.RunningLoyaltyPoints,
                    BalanceAfterTransaction = customer.RunningLoyaltyPoints,
                    CreatedAt = DateTime.UtcNow,
                    Remarks = $"Downgraded from {oldTierName} to {correctTier.Name} due to spend threshold"
                };
                _context.LoyaltyLedger.Add(ledgerEntry);
                
                await _notificationService.SendSmsAsync(customer.Phone, $"Your membership tier has been updated to {correctTier.Name}.");
                downgradedCount++;
            }
        }

        await _context.SaveChangesAsync(CancellationToken.None);
        _logger.LogInformation($"Tier Downgrade Job completed. Downgraded {downgradedCount} customers.");
    }

    public async Task BirthdayBonusJob()
    {
        _logger.LogInformation("Starting Birthday Bonus Job");

        var config = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync();
        if (config == null || !config.IsActiveConfig) return;

        var today = DateTime.UtcNow.Date;
        
        var customers = await _context.Customers
            .Where(c => c.Dob.HasValue && c.Dob.Value.Day == today.Day && c.Dob.Value.Month == today.Month)
            .ToListAsync();

        int awardedCount = 0;

        foreach (var customer in customers)
        {
            var alreadyAwarded = await _context.LoyaltyLedger
                .AnyAsync(l => l.CustomerId == customer.Id 
                            && l.TransactionType == "BirthdayBonus" 
                            && l.CreatedAt.Year == today.Year);

            if (!alreadyAwarded)
            {
                var previousBalance = customer.RunningLoyaltyPoints;
                var bonus = config.BirthdayBonusPoints;
                customer.RunningLoyaltyPoints += bonus;

                var ledgerEntry = new LoyaltyLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    TransactionType = "BirthdayBonus",
                    PointsEarned = bonus,
                    PointsRedeemed = 0,
                    PreviousBalance = previousBalance,
                    BalanceAfterTransaction = customer.RunningLoyaltyPoints,
                    CreatedAt = DateTime.UtcNow,
                    Remarks = "Annual Birthday Bonus"
                };

                _context.LoyaltyLedger.Add(ledgerEntry);
                await _notificationService.SendSmsAsync(customer.Phone, $"Happy Birthday! We have credited {bonus} points to your account.");
                awardedCount++;
            }
        }

        await _context.SaveChangesAsync(CancellationToken.None);
        _logger.LogInformation($"Birthday Bonus Job completed. Awarded to {awardedCount} customers.");
    }

    public async Task AnniversaryBonusJob()
    {
        _logger.LogInformation("Starting Anniversary Bonus Job");

        var config = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync();
        if (config == null || !config.IsActiveConfig) return;

        var today = DateTime.UtcNow.Date;

        var customers = await _context.Customers
            .Where(c => c.Anniversary.HasValue && c.Anniversary.Value.Day == today.Day && c.Anniversary.Value.Month == today.Month)
            .ToListAsync();

        int awardedCount = 0;

        foreach (var customer in customers)
        {
            var alreadyAwarded = await _context.LoyaltyLedger
                .AnyAsync(l => l.CustomerId == customer.Id 
                            && l.TransactionType == "AnniversaryBonus" 
                            && l.CreatedAt.Year == today.Year);

            if (!alreadyAwarded)
            {
                var previousBalance = customer.RunningLoyaltyPoints;
                var bonus = config.AnniversaryBonusPoints;
                customer.RunningLoyaltyPoints += bonus;

                var ledgerEntry = new LoyaltyLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    TransactionType = "AnniversaryBonus",
                    PointsEarned = bonus,
                    PointsRedeemed = 0,
                    PreviousBalance = previousBalance,
                    BalanceAfterTransaction = customer.RunningLoyaltyPoints,
                    CreatedAt = DateTime.UtcNow,
                    Remarks = "Annual Anniversary Bonus"
                };

                _context.LoyaltyLedger.Add(ledgerEntry);
                await _notificationService.SendSmsAsync(customer.Phone, $"Happy Anniversary! We have credited {bonus} points to your account.");
                awardedCount++;
            }
        }

        await _context.SaveChangesAsync(CancellationToken.None);
        _logger.LogInformation($"Anniversary Bonus Job completed. Awarded to {awardedCount} customers.");
    }

    public async Task LoyaltyMaintenanceJob()
    {
        _logger.LogInformation("Starting Loyalty Health Monitoring Job");

        var negativeBalances = await _context.Customers.Where(c => c.RunningLoyaltyPoints < 0).ToListAsync();
        foreach (var customer in negativeBalances)
        {
            _logger.LogWarning($"CRITICAL: Customer {customer.Id} ({customer.Name}) has negative balance: {customer.RunningLoyaltyPoints}");
        }

        var orphanedLedgers = await _context.LoyaltyLedger
            .Where(l => !_context.Customers.Any(c => c.Id == l.CustomerId))
            .CountAsync();
            
        if (orphanedLedgers > 0)
        {
            _logger.LogWarning($"CRITICAL: Found {orphanedLedgers} orphaned LoyaltyLedger entries.");
        }

        var report = new PosErp.Domain.Entities.Audit.AuditLog
        {
            Id = Guid.NewGuid(),
            Action = "LoyaltyHealthCheck",
            EntityType = "System",
            EntityId = "0",
            UserId = Guid.Empty,
            Timestamp = DateTime.UtcNow,
            Details = $"NegativeBalances: {negativeBalances.Count}, OrphanedLedgers: {orphanedLedgers}"
        };

        _context.AuditLogs.Add(report);
        await _context.SaveChangesAsync(CancellationToken.None);

        _logger.LogInformation("Loyalty Health Monitoring Job completed.");
    }
}
