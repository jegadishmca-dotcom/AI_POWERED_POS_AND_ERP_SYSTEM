using Microsoft.AspNetCore.Mvc;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Purchasing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/procurement")]
public class ProcurementRecommendationController : ControllerBase
{
    private readonly IPurchaseRecommendationEngine _recommendationEngine;
    private readonly IApplicationDbContext _context;

    public ProcurementRecommendationController(IPurchaseRecommendationEngine recommendationEngine, IApplicationDbContext context)
    {
        _recommendationEngine = recommendationEngine;
        _context = context;
    }

    [HttpGet("recommendations")]
    public async Task<IActionResult> GetRecommendations([FromQuery] Guid storeId)
    {
        var recommendations = await _recommendationEngine.GenerateRecommendationsAsync(storeId, DateTime.UtcNow);
        return Ok(recommendations);
    }

    [HttpPost("generate-po")]
    public async Task<IActionResult> GeneratePurchaseOrders([FromQuery] Guid storeId)
    {
        var recommendations = await _recommendationEngine.GenerateRecommendationsAsync(storeId, DateTime.UtcNow);
        
        // Group by preferred supplier
        var groupedBySupplier = recommendations
            .Where(r => r.SupplierId.HasValue && r.RecommendedQuantity > 0)
            .GroupBy(r => r.SupplierId.Value)
            .ToList();

        var generatedOrders = new List<PurchaseOrderHeader>();

        foreach (var group in groupedBySupplier)
        {
            var po = new PurchaseOrderHeader
            {
                StoreId = storeId,
                SupplierId = group.Key,
                PoNumber = "PO-" + DateTime.UtcNow.Ticks.ToString().Substring(8),
                PoDate = DateTime.UtcNow,
                ExpectedDeliveryDate = DateTime.UtcNow.AddDays(7), // Should be based on lead time ideally
                Status = "DRAFT"
            };

            foreach (var item in group)
            {
                po.Items.Add(new PurchaseOrderItem
                {
                    ProductId = item.ProductId,
                    OrderedQuantity = item.RecommendedQuantity,
                    UnitCost = 0, // To be filled/fetched
                    TotalCost = 0
                });
            }

            _context.PurchaseOrders.Add(po);
            generatedOrders.Add(po);
        }

        await _context.SaveChangesAsync(default);

        return Ok(new { Message = $"Generated {generatedOrders.Count} draft POs.", Orders = generatedOrders });
    }
}
