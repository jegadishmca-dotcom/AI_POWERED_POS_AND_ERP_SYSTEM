using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Crm;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Loyalty;

public record GetLoyaltyConfigQuery() : IRequest<LoyaltyProgramConfig>;

public class GetLoyaltyConfigHandler : IRequestHandler<GetLoyaltyConfigQuery, LoyaltyProgramConfig>
{
    private readonly IApplicationDbContext _context;
    public GetLoyaltyConfigHandler(IApplicationDbContext context) => _context = context;

    public async Task<LoyaltyProgramConfig> Handle(GetLoyaltyConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config == null)
        {
            config = new LoyaltyProgramConfig();
            _context.LoyaltyProgramConfigs.Add(config);
            await ((DbContext)_context).SaveChangesAsync(cancellationToken);
        }
        return config;
    }
}

public record UpdateLoyaltyConfigCommand(LoyaltyProgramConfig Config) : IRequest<LoyaltyProgramConfig>;

public class UpdateLoyaltyConfigHandler : IRequestHandler<UpdateLoyaltyConfigCommand, LoyaltyProgramConfig>
{
    private readonly IApplicationDbContext _context;
    public UpdateLoyaltyConfigHandler(IApplicationDbContext context) => _context = context;

    public async Task<LoyaltyProgramConfig> Handle(UpdateLoyaltyConfigCommand request, CancellationToken cancellationToken)
    {
        var config = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync(cancellationToken);
        if (config == null)
        {
            config = new LoyaltyProgramConfig();
            _context.LoyaltyProgramConfigs.Add(config);
        }
        
        config.EarnRatioSpendAmount = request.Config.EarnRatioSpendAmount;
        config.EarnRatioPoints = request.Config.EarnRatioPoints;
        config.RedeemRatioPoints = request.Config.RedeemRatioPoints;
        config.RedeemRatioDiscountAmount = request.Config.RedeemRatioDiscountAmount;
        config.MaxRedemptionPercentagePerInvoice = request.Config.MaxRedemptionPercentagePerInvoice;
        config.MaxRedemptionPerDay = request.Config.MaxRedemptionPerDay;
        config.MaxManualAdjustmentPerDay = request.Config.MaxManualAdjustmentPerDay;
        config.MaxBonusAllocationPerCustomer = request.Config.MaxBonusAllocationPerCustomer;
        config.EnableAutoTierEvaluation = request.Config.EnableAutoTierEvaluation;
        config.EnablePointExpiry = request.Config.EnablePointExpiry;
        config.ExpiryMonths = request.Config.ExpiryMonths;
        config.UpdatedAt = DateTime.UtcNow;

        await ((DbContext)_context).SaveChangesAsync(cancellationToken);
        return config;
    }
}

public record GetLoyaltyDashboardQuery() : IRequest<object>;

public class GetLoyaltyDashboardHandler : IRequestHandler<GetLoyaltyDashboardQuery, object>
{
    private readonly IApplicationDbContext _context;
    public GetLoyaltyDashboardHandler(IApplicationDbContext context) => _context = context;

    public async Task<object> Handle(GetLoyaltyDashboardQuery request, CancellationToken cancellationToken)
    {
        var totalMembers = await _context.Customers.CountAsync(cancellationToken);
        var activeMembers = await _context.Customers.CountAsync(c => c.MembershipStatus == "Active", cancellationToken);
        var dormantMembers = await _context.Customers.CountAsync(c => c.CustomerSegment == "Dormant", cancellationToken);
        
        var pointStats = await _context.LoyaltyLedger
            .GroupBy(x => 1)
            .Select(g => new {
                Issued = g.Where(x => x.PointsEarned > 0).Sum(x => x.PointsEarned),
                Redeemed = g.Where(x => x.PointsRedeemed > 0).Sum(x => x.PointsRedeemed)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var outstandingLiability = await _context.Customers.SumAsync(x => x.RunningLoyaltyPoints, cancellationToken);
        
        var config = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync(cancellationToken);
        var liabilityValue = config != null && config.RedeemRatioPoints > 0 
            ? (outstandingLiability / config.RedeemRatioPoints) * config.RedeemRatioDiscountAmount 
            : 0;
        
        var tierDist = await _context.Customers
            .Include(c => c.Tier)
            .GroupBy(c => c.Tier != null ? c.Tier.Name : "Base")
            .Select(g => new { Tier = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var topCustomers = await _context.Customers
            .OrderByDescending(c => c.LifetimeSpend)
            .Take(20)
            .Select(c => new { c.Id, c.Name, c.Phone, c.LifetimeSpend, c.RunningLoyaltyPoints, TierName = c.Tier != null ? c.Tier.Name : "Base" })
            .ToListAsync(cancellationToken);

        return new {
            TotalMembers = totalMembers,
            ActiveMembers = activeMembers,
            DormantMembers = dormantMembers,
            PointsIssued = pointStats?.Issued ?? 0,
            PointsRedeemed = pointStats?.Redeemed ?? 0,
            OutstandingLiability = outstandingLiability,
            LiabilityValue = liabilityValue,
            TierDistribution = tierDist,
            TopCustomers = topCustomers
        };
    }
}

public record GetLoyaltyLiabilityReportQuery() : IRequest<object>;

public class GetLoyaltyLiabilityReportHandler : IRequestHandler<GetLoyaltyLiabilityReportQuery, object>
{
    private readonly IApplicationDbContext _context;
    public GetLoyaltyLiabilityReportHandler(IApplicationDbContext context) => _context = context;

    public async Task<object> Handle(GetLoyaltyLiabilityReportQuery request, CancellationToken cancellationToken)
    {
        var outstandingPoints = await _context.Customers.SumAsync(c => c.RunningLoyaltyPoints, cancellationToken);
        var redeemedPoints = await _context.LoyaltyLedger.SumAsync(l => l.PointsRedeemed, cancellationToken);
        var expiredPoints = await _context.LoyaltyLedger.Where(l => l.TransactionType == "Expiration").SumAsync(l => l.PointsRedeemed, cancellationToken);
        
        var config = await _context.LoyaltyProgramConfigs.FirstOrDefaultAsync(cancellationToken);
        var liabilityValue = config != null && config.RedeemRatioPoints > 0 
            ? (outstandingPoints / config.RedeemRatioPoints) * config.RedeemRatioDiscountAmount 
            : 0;

        return new {
            OutstandingPoints = outstandingPoints,
            RedeemedPoints = redeemedPoints,
            ExpiredPoints = expiredPoints,
            LiabilityValue = liabilityValue,
            GeneratedAt = DateTime.UtcNow
        };
    }
}
