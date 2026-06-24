using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/offers/analytics")]
[Authorize(Roles = "Owner,Manager")]
public class OfferAnalyticsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public OfferAnalyticsController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("usage")]
    public async Task<IActionResult> GetOfferUsageMetrics(CancellationToken cancellationToken)
    {
        var logs = await _context.OfferUsageLogs.ToListAsync(cancellationToken);

        var metrics = logs.GroupBy(l => l.OfferId)
            .Select(g => new
            {
                OfferId = g.Key,
                OfferName = g.First().OfferName,
                TimesApplied = g.Count(),
                TotalDiscountGiven = g.Sum(l => l.DiscountAmount),
                RevenueInfluenced = g.Sum(l => l.RevenueInfluenced),
                AverageBasketValue = g.Average(l => l.RevenueInfluenced),
                LastAppliedDate = g.Max(l => l.CreatedAt)
            }).ToList();

        return Ok(metrics);
    }
}
