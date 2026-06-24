using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/supplier/analytics")]
public class SupplierAnalyticsController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public SupplierAnalyticsController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("scorecards")]
    public async Task<IActionResult> GetScorecards()
    {
        // For demonstration, we calculate the scorecard dynamically from recent Purchase Orders and GRNs
        // In reality, this would be periodically calculated and saved in the SupplierScorecard table.
        
        var suppliers = await _context.Suppliers.ToListAsync();
        var scorecards = new System.Collections.Generic.List<object>();

        foreach (var supplier in suppliers)
        {
            var grns = await _context.GRNHeaders
                .Where(g => g.SupplierId == supplier.Id)
                .OrderByDescending(g => g.ReceivedDate)
                .Take(20)
                .ToListAsync();

            var totalPurchaseValue = await _context.PurchaseBills
                .Where(b => b.SupplierId == supplier.Id)
                .SumAsync(b => b.TotalAmount);

            decimal onTimePct = 100m;
            decimal fillRate = 100m;

            if (grns.Any())
            {
                // Dummy calculations for demonstration
                onTimePct = 95m; // E.g., comparing GRN.ReceivedDate vs PO.ExpectedDeliveryDate
                fillRate = 98m;  // E.g., comparing GRNItem.AcceptedQty vs POItem.OrderedQty
            }

            // Simple rating
            decimal rating = (onTimePct + fillRate) / 2m;

            scorecards.Add(new
            {
                SupplierId = supplier.Id,
                SupplierName = supplier.Name,
                DeliveryAccuracy = onTimePct,
                LeadTimeCompliance = 90m,
                FillRate = fillRate,
                PurchaseValue = totalPurchaseValue,
                QualityScore = 99m,
                SupplierRating = rating
            });
        }

        return Ok(scorecards.OrderByDescending(s => (decimal)((dynamic)s).SupplierRating));
    }
}
