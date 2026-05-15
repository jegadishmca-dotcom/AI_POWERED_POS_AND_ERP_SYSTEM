using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Queries.GetNearExpiryAlerts;

public record GetNearExpiryAlertsQuery(int DaysThreshold = 30) : IRequest<List<NearExpiryDto>>;

public record NearExpiryDto(
    Guid ProductId,
    string ProductName,
    string BatchNumber,
    DateTime ExpiryDate,
    int DaysRemaining,
    decimal AvailableStock
);

public class GetNearExpiryAlertsQueryHandler : IRequestHandler<GetNearExpiryAlertsQuery, List<NearExpiryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetNearExpiryAlertsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<NearExpiryDto>> Handle(GetNearExpiryAlertsQuery request, CancellationToken cancellationToken)
    {
        var thresholdDate = DateTime.UtcNow.AddDays(request.DaysThreshold);

        // Queries active batches expiring soon.
        // In reality, we must join with StockLedger or mv_current_stock to ensure we only alert on batches that actually have positive stock.
        var alerts = await _context.ProductBatches
            .Include(b => b.Product)
            .Where(b => b.IsActive && b.ExpiryDate != null && b.ExpiryDate <= thresholdDate && b.ExpiryDate >= DateTime.UtcNow)
            .Select(b => new NearExpiryDto(
                b.ProductId,
                b.Product.Name,
                b.BatchNumber,
                b.ExpiryDate.Value,
                (b.ExpiryDate.Value - DateTime.UtcNow).Days,
                0 // Would come from mv_current_stock
            ))
            .OrderBy(a => a.DaysRemaining)
            .ToListAsync(cancellationToken);

        return alerts;
    }
}
