using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Inventory;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/inventory/intelligence")]
public class InventoryIntelligenceController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public InventoryIntelligenceController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetInventoryHealth([FromQuery] Guid storeId)
    {
        // For simplicity, we assume we calculate this dynamically based on current stock ledger and thresholds.
        
        var allStocks = await _context.StockLedger
            .Where(s => s.StoreId == storeId || s.WarehouseId == storeId)
            .GroupBy(s => s.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.OrderByDescending(x => x.CreatedAt).FirstOrDefault().RunningBalance,
                UnitCost = g.OrderByDescending(x => x.CreatedAt).FirstOrDefault().UnitCost
            })
            .ToListAsync();

        decimal inventoryValue = allStocks.Sum(s => s.Quantity * s.UnitCost);
        
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        
        // Calculate Dead Stock Value (Items with qty > 0 but 0 sales in 30 days)
        var salesLast30Days = await _context.InvoiceItems
            .Where(i => i.Invoice.StoreId == storeId && i.BusinessDate >= thirtyDaysAgo)
            .Select(i => i.ProductId)
            .Distinct()
            .ToListAsync();

        var deadStock = allStocks.Where(s => s.Quantity > 0 && !salesLast30Days.Contains(s.ProductId)).ToList();
        decimal deadStockValue = deadStock.Sum(s => s.Quantity * s.UnitCost);

        // Expiry Risk (Stock expiring within 90 days)
        var ninetyDaysFromNow = DateTime.UtcNow.AddDays(90);
        var expiryStock = await _context.StockLedger
            .Where(s => (s.StoreId == storeId || s.WarehouseId == storeId) && s.ExpiryDate != null && s.ExpiryDate <= ninetyDaysFromNow)
            .GroupBy(s => new { s.ProductId, s.BatchId })
            .Select(g => new
            {
                Quantity = g.OrderByDescending(x => x.CreatedAt).FirstOrDefault().RunningBalance,
                UnitCost = g.OrderByDescending(x => x.CreatedAt).FirstOrDefault().UnitCost
            })
            .ToListAsync();

        decimal expiryRiskValue = expiryStock.Sum(s => s.Quantity * s.UnitCost);

        // Health Score Calculation
        int score = 100;
        if (inventoryValue > 0)
        {
            decimal deadStockRatio = deadStockValue / inventoryValue;
            if (deadStockRatio > 0.05m) score -= 10;
            if (deadStockRatio > 0.15m) score -= 15;
            
            decimal expiryRatio = expiryRiskValue / inventoryValue;
            if (expiryRatio > 0.05m) score -= 10;
        }
        
        score = Math.Max(0, score);

        return Ok(new
        {
            InventoryValue = inventoryValue,
            InventoryHealthScore = score,
            DeadStockValue = deadStockValue,
            ExpiryRiskValue = expiryRiskValue,
            ReorderValue = 0 // To be fetched from ReorderEngine in a real scenario
        });
    }

    [HttpGet("fast-moving")]
    public async Task<IActionResult> GetFastMoving([FromQuery] Guid storeId, [FromQuery] int days = 30)
    {
        var dateThreshold = DateTime.UtcNow.AddDays(-days);
        
        var fastMovers = await _context.InvoiceItems
            .Where(i => i.Invoice.StoreId == storeId && i.BusinessDate >= dateThreshold)
            .GroupBy(i => i.Product.Name)
            .Select(g => new
            {
                ProductName = g.Key,
                TotalSold = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.Quantity * x.UnitPrice)
            })
            .OrderByDescending(x => x.TotalSold)
            .Take(10)
            .ToListAsync();
            
        return Ok(fastMovers);
    }

    [HttpGet("slow-moving")]
    public async Task<IActionResult> GetSlowMoving([FromQuery] Guid storeId, [FromQuery] int days = 30)
    {
        var dateThreshold = DateTime.UtcNow.AddDays(-days);
        
        var soldProducts = await _context.InvoiceItems
            .Where(i => i.Invoice.StoreId == storeId && i.BusinessDate >= dateThreshold)
            .Select(i => i.ProductId)
            .Distinct()
            .ToListAsync();

        var slowMovers = await _context.Products
            .Where(p => p.StoreId == storeId && !soldProducts.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.Name,
                DaysWithoutSale = days
            })
            .Take(20)
            .ToListAsync();

        return Ok(slowMovers);
    }

    [HttpGet("dead-stock")]
    public async Task<IActionResult> GetDeadStock([FromQuery] Guid storeId)
    {
        // Similar to Slow moving but perhaps older (e.g., 90 days)
        var dateThreshold = DateTime.UtcNow.AddDays(-90);
        
        var soldProducts = await _context.InvoiceItems
            .Where(i => i.Invoice.StoreId == storeId && i.BusinessDate >= dateThreshold)
            .Select(i => i.ProductId)
            .Distinct()
            .ToListAsync();

        var deadStock = await _context.StockLedger
            .Where(s => (s.StoreId == storeId || s.WarehouseId == storeId) && !soldProducts.Contains(s.ProductId))
            .GroupBy(s => s.Product.Name)
            .Select(g => new
            {
                ProductName = g.Key,
                Quantity = g.OrderByDescending(x => x.CreatedAt).FirstOrDefault().RunningBalance
            })
            .Where(x => x.Quantity > 0)
            .Take(20)
            .ToListAsync();

        return Ok(deadStock);
    }

    [HttpGet("expiry")]
    public async Task<IActionResult> GetExpiryIntelligence([FromQuery] Guid storeId)
    {
        var today = DateTime.UtcNow.Date;
        
        var allBatches = await _context.StockLedger
            .Where(s => (s.StoreId == storeId || s.WarehouseId == storeId) && s.ExpiryDate != null)
            .GroupBy(s => new { s.Product.Name, s.BatchId, s.ExpiryDate })
            .Select(g => new
            {
                ProductName = g.Key.Name,
                ExpiryDate = g.Key.ExpiryDate.Value,
                Quantity = g.OrderByDescending(x => x.CreatedAt).FirstOrDefault().RunningBalance,
                UnitCost = g.OrderByDescending(x => x.CreatedAt).FirstOrDefault().UnitCost
            })
            .ToListAsync();

        var validBatches = allBatches.Where(b => b.Quantity > 0).ToList();

        decimal immRisk = 0, highRisk = 0, medRisk = 0, lowRisk = 0;

        foreach (var b in validBatches)
        {
            var daysLeft = (b.ExpiryDate - today).TotalDays;
            var value = b.Quantity * b.UnitCost;

            if (daysLeft >= 0 && daysLeft <= 7) immRisk += value;
            else if (daysLeft > 7 && daysLeft <= 30) highRisk += value;
            else if (daysLeft > 30 && daysLeft <= 60) medRisk += value;
            else if (daysLeft > 60 && daysLeft <= 90) lowRisk += value;
        }

        return Ok(new
        {
            ImmediateRisk = immRisk,
            HighRisk = highRisk,
            MediumRisk = medRisk,
            LowRisk = lowRisk
        });
    }

    [HttpGet("overstock")]
    public async Task<IActionResult> GetOverstockRisk([FromQuery] Guid storeId)
    {
        var policies = await _context.ProductStoreInventoryPolicies
            .Where(p => p.InventoryLocationId == storeId && p.MaxStockLevel > 0)
            .ToListAsync();

        var riskItems = new List<object>();

        foreach (var policy in policies)
        {
            var currentStock = await _context.StockLedger
                .Where(s => (s.StoreId == storeId || s.WarehouseId == storeId) && s.ProductId == policy.ProductId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.RunningBalance)
                .FirstOrDefaultAsync();

            if (currentStock > policy.MaxStockLevel)
            {
                riskItems.Add(new
                {
                    ProductId = policy.ProductId,
                    CurrentStock = currentStock,
                    MaxStock = policy.MaxStockLevel,
                    Excess = currentStock - policy.MaxStockLevel
                });
            }
        }

        return Ok(riskItems);
    }

    [HttpGet("understock")]
    public async Task<IActionResult> GetUnderstockRisk([FromQuery] Guid storeId)
    {
        var policies = await _context.ProductStoreInventoryPolicies
            .Where(p => p.InventoryLocationId == storeId)
            .ToListAsync();

        var riskItems = new List<object>();

        foreach (var policy in policies)
        {
            var currentStock = await _context.StockLedger
                .Where(s => (s.StoreId == storeId || s.WarehouseId == storeId) && s.ProductId == policy.ProductId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.RunningBalance)
                .FirstOrDefaultAsync();

            if (currentStock < policy.MinStockLevel)
            {
                riskItems.Add(new
                {
                    ProductId = policy.ProductId,
                    CurrentStock = currentStock,
                    MinStock = policy.MinStockLevel,
                    Shortage = policy.MinStockLevel - currentStock
                });
            }
        }

        return Ok(riskItems);
    }
}
