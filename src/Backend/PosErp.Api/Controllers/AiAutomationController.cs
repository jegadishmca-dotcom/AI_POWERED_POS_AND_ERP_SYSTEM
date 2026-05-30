using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Purchasing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AiAutomationController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

    public AiAutomationController(IApplicationDbContext context)
    {
        _context = context;
    }

    public record ChatRequest(string Prompt);

    public record MarkdownApplyItem(Guid ProductId, Guid BatchId, decimal NewPrice);
    public record ApplyMarkdownsRequest(List<MarkdownApplyItem> Markdowns);

    public record GeneratePoItem(Guid ProductId, Guid SupplierId, decimal Quantity, decimal UnitCost);
    public record GeneratePoRequest(List<GeneratePoItem> Items);

    // ── 0. OLLAMA STATUS CHECK ────────────────────────────────────────
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        bool ollamaOnline = false;
        try
        {
            var ollamaUrl = "http://pos_ollama:11434";
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                var response = await _httpClient.GetAsync(ollamaUrl, cts.Token);
                ollamaOnline = response.IsSuccessStatusCode;
            }
            catch
            {
                ollamaUrl = "http://localhost:11434";
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                var response = await _httpClient.GetAsync(ollamaUrl, cts.Token);
                ollamaOnline = response.IsSuccessStatusCode;
            }
        }
        catch
        {
            ollamaOnline = false;
        }

        return Ok(new { ollamaOnline });
    }


    // ── 1. DEMAND FORECASTING & REPLENISHMENT ─────────────────────────
    [HttpGet("forecast-replenishment")]
    public async Task<IActionResult> GetForecastReplenishment(CancellationToken cancellationToken)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        
        // Fetch sales quantity per product in last 30 days (sales are stored as negative quantities in ledger)
        var salesData = await _context.StockLedger
            .Where(sl => sl.MovementType == "SALE" && sl.BusinessDate >= cutoffDate)
            .GroupBy(sl => sl.ProductId)
            .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(sl => -sl.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.TotalSold, cancellationToken);

        // Fetch current active stock for all products
        var currentStocks = await _context.StockLedger
            .GroupBy(sl => sl.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(sl => sl.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Stock, cancellationToken);

        var products = await _context.Products
            .Where(p => !p.IsDeleted && p.IsActive)
            .ToListAsync(cancellationToken);

        var suppliers = await _context.Suppliers.Where(s => s.IsActive).ToListAsync(cancellationToken);
        var defaultSupplier = suppliers.FirstOrDefault() ?? new Supplier { Id = Guid.NewGuid(), Name = "Default Local Supplier" };
        var supplierLookup = suppliers.ToDictionary(s => s.Id);

        // Optimized: Fetch the latest GRN items per product to determine the preferred supplier, all in one query.
        var grnItemsData = await _context.GRNItems
            .Select(gi => new { gi.ProductId, gi.GRNHeader.SupplierId, gi.GRNHeader.CreatedAt })
            .ToListAsync(cancellationToken);

        var productPreferredSuppliers = grnItemsData
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CreatedAt).First().SupplierId
            );

        var recommendations = new List<object>();

        foreach (var product in products)
        {
            salesData.TryGetValue(product.Id, out decimal totalSoldIn30Days);
            currentStocks.TryGetValue(product.Id, out decimal currentStock);

            decimal avgDailySales = totalSoldIn30Days / 30.0m;
            // Fallback for new products or low-sales items
            if (avgDailySales == 0)
            {
                avgDailySales = 0.15m; 
            }

            decimal forecasted15DayDemand = avgDailySales * 15m;
            decimal safetyStock = avgDailySales * 5m;
            decimal requiredQuantity = (forecasted15DayDemand + safetyStock) - currentStock;

            if (requiredQuantity > 0)
            {
                int recommendedOrder = (int)Math.Ceiling(requiredQuantity);
                
                // Find supplier from last GRN of this product
                Guid supplierId = defaultSupplier.Id;
                string supplierName = defaultSupplier.Name;

                if (productPreferredSuppliers.TryGetValue(product.Id, out Guid preferredSupplierId) &&
                    supplierLookup.TryGetValue(preferredSupplierId, out var supplier))
                {
                    supplierId = supplier.Id;
                    supplierName = supplier.Name;
                }

                recommendations.Add(new
                {
                    productId = product.Id,
                    productCode = product.ProductCode,
                    productName = product.Name,
                    currentStock = currentStock,
                    avgDailySales = Math.Round(avgDailySales, 2),
                    forecastedDemand = Math.Round(forecasted15DayDemand, 1),
                    recommendedOrderQty = recommendedOrder,
                    supplierId = supplierId,
                    supplierName = supplierName,
                    unitCost = product.PurchasePrice,
                    totalCost = recommendedOrder * product.PurchasePrice
                });
            }
        }

        return Ok(recommendations);
    }


    // ── 2. AUTO GENERATE DRAFT PURCHASE ORDERS ────────────────────────
    [HttpPost("generate-po")]
    public async Task<IActionResult> GeneratePurchaseOrders([FromBody] GeneratePoRequest request, CancellationToken cancellationToken)
    {
        if (request?.Items == null || !request.Items.Any())
        {
            return BadRequest("No items selected for PO generation.");
        }

        var groupedBySupplier = request.Items.GroupBy(i => i.SupplierId);
        var generatedPoNumbers = new List<string>();

        foreach (var supplierGroup in groupedBySupplier)
        {
            var supplierId = supplierGroup.Key;
            var poNumber = $"PO-AUTO-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}";
            
            var poHeader = new PurchaseOrderHeader
            {
                Id = Guid.NewGuid(),
                SupplierId = supplierId,
                PoNumber = poNumber,
                PoDate = DateTime.UtcNow.Date,
                ExpectedDeliveryDate = DateTime.UtcNow.AddDays(7).Date,
                Status = "DRAFT",
                CreatedAt = DateTime.UtcNow,
                TotalAmount = supplierGroup.Sum(item => item.Quantity * item.UnitCost)
            };

            foreach (var item in supplierGroup)
            {
                poHeader.Items.Add(new PurchaseOrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    OrderedQuantity = item.Quantity,
                    ReceivedQuantity = 0,
                    UnitCost = item.UnitCost,
                    TotalCost = item.Quantity * item.UnitCost
                });
            }

            _context.PurchaseOrders.Add(poHeader);
            generatedPoNumbers.Add(poNumber);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, poNumbers = generatedPoNumbers });
    }

    // ── 3. NEAR-EXPIRY MARKDOWN PRICING SUGGESTIONS ──────────────────
    [HttpGet("near-expiry-markdowns")]
    public async Task<IActionResult> GetNearExpiryMarkdowns(CancellationToken cancellationToken)
    {
        var targetDate = DateTime.UtcNow.AddDays(30).Date;
        var today = DateTime.UtcNow.Date;

        // Fetch batches expiring in the next 30 days
        var expiringBatches = await _context.ProductBatches
            .Include(pb => pb.Product)
            .Where(pb => pb.IsActive && pb.ExpiryDate.HasValue && pb.ExpiryDate.Value <= targetDate && pb.ExpiryDate.Value >= today)
            .ToListAsync(cancellationToken);

        var batchIds = expiringBatches.Select(b => (Guid?)b.Id).ToList();

        // Optimized: Fetch current stock for all target batches in one query
        var batchStocks = await _context.StockLedger
            .Where(sl => sl.BatchId.HasValue && batchIds.Contains(sl.BatchId))
            .GroupBy(sl => sl.BatchId)
            .Select(g => new { BatchId = g.Key.Value, Stock = g.Sum(sl => sl.Quantity) })
            .ToDictionaryAsync(x => x.BatchId, x => x.Stock, cancellationToken);

        var markdowns = new List<object>();

        foreach (var batch in expiringBatches)
        {
            batchStocks.TryGetValue(batch.Id, out decimal currentStock);
            if (currentStock <= 0) continue;

            int daysRemaining = (batch.ExpiryDate.Value - today).Days;
            
            // Markdown rule: closer to expiry = higher discount
            decimal discountPercent = 10m; // 15-30 days left
            if (daysRemaining < 7)
            {
                discountPercent = 50m; // less than a week left
            }
            else if (daysRemaining <= 14)
            {
                discountPercent = 25m; // 7-14 days left
            }

            decimal originalPrice = batch.Product.SellingPrice;
            decimal suggestedPrice = Math.Round(originalPrice * (1.0m - (discountPercent / 100m)), 2);

            markdowns.Add(new
            {
                productId = batch.ProductId,
                productCode = batch.Product.ProductCode,
                productName = batch.Product.Name,
                batchId = batch.Id,
                batchNumber = batch.BatchNumber,
                expiryDate = batch.ExpiryDate.Value.ToString("yyyy-MM-dd"),
                daysLeft = daysRemaining,
                currentStock = currentStock,
                originalPrice = originalPrice,
                suggestedPrice = suggestedPrice,
                discountPercent = discountPercent
            });
        }

        return Ok(markdowns);
    }


    // ── 4. APPLY DISCOUNTS TO MAIN PRODUCT CATALOG ───────────────────
    [HttpPost("apply-markdowns")]
    public async Task<IActionResult> ApplyMarkdowns([FromBody] ApplyMarkdownsRequest request, CancellationToken cancellationToken)
    {
        if (request?.Markdowns == null || !request.Markdowns.Any())
        {
            return BadRequest("No markdowns provided.");
        }

        foreach (var item in request.Markdowns)
        {
            var product = await _context.Products.FindAsync(new object[] { item.ProductId }, cancellationToken);
            if (product != null)
            {
                product.SellingPrice = item.NewPrice;
                product.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true });
    }

    // ── 5. CONVERSATIONAL ERP ASSISTANT (CHAT) ────────────────────────
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Prompt))
        {
            return BadRequest("Prompt cannot be empty.");
        }

        string promptLower = request.Prompt.ToLower().Trim();
        
        // A. Hybrid NLP Rule-based router for live DB analytics
        if (promptLower.Contains("top sales") || promptLower.Contains("top selling") || promptLower.Contains("best seller") || promptLower.Contains("top 5"))
        {
            var topSales = await _context.StockLedger
                .Where(sl => sl.MovementType == "SALE")
                .GroupBy(sl => sl.ProductId)
                .Select(g => new { ProductId = g.Key, Qty = g.Sum(sl => -sl.Quantity) })
                .OrderByDescending(x => x.Qty)
                .Take(5)
                .ToListAsync(cancellationToken);

            var productIds = topSales.Select(x => x.ProductId).ToList();
            var productNames = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

            var chartData = topSales.Select(x => new
            {
                name = productNames.TryGetValue(x.ProductId, out var name) ? name : "Unknown",
                value = (double)x.Qty
            }).ToList();

            return Ok(new
            {
                text = "Here are the top 5 selling products based on sales volume over all transactions:",
                chartType = "BAR",
                chartData = chartData
            });
        }
        
        if (promptLower.Contains("payment") || promptLower.Contains("cash vs upi") || promptLower.Contains("sales split") || promptLower.Contains("sales breakdown"))
        {
            var invoices = await _context.Invoices.ToListAsync(cancellationToken);
            decimal cashTotal = invoices.Sum(i => i.CashAmount);
            decimal upiTotal = invoices.Sum(i => i.UpiAmount);
            decimal cardTotal = invoices.Sum(i => i.CardAmount);
            decimal walletTotal = invoices.Sum(i => i.WalletAmount);

            var chartData = new List<object>
            {
                new { name = "Cash", value = (double)cashTotal },
                new { name = "UPI", value = (double)upiTotal },
                new { name = "Card", value = (double)cardTotal },
                new { name = "Wallet", value = (double)walletTotal }
            };

            return Ok(new
            {
                text = $"Sales distribution breakdown by payment mode:\n- Cash: ₹{cashTotal:N2}\n- UPI: ₹{upiTotal:N2}\n- Card: ₹{cardTotal:N2}\n- Wallet: ₹{walletTotal:N2}",
                chartType = "PIE",
                chartData = chartData.Where(x => (double)x.GetType().GetProperty("value").GetValue(x) > 0).ToList()
            });
        }

        if (promptLower.Contains("expiry") || promptLower.Contains("expired") || promptLower.Contains("expiring"))
        {
            var targetDate = DateTime.UtcNow.AddDays(30).Date;
            var today = DateTime.UtcNow.Date;
            
            var expiring = await _context.ProductBatches
                .Include(pb => pb.Product)
                .Where(pb => pb.IsActive && pb.ExpiryDate.HasValue && pb.ExpiryDate.Value <= targetDate && pb.ExpiryDate.Value >= today)
                .Take(5)
                .ToListAsync(cancellationToken);

            if (!expiring.Any())
            {
                return Ok(new { text = "Good news! No active product batches are expiring within the next 30 days." });
            }

            var batchIds = expiring.Select(b => (Guid?)b.Id).ToList();
            var batchStocks = await _context.StockLedger
                .Where(sl => sl.BatchId.HasValue && batchIds.Contains(sl.BatchId))
                .GroupBy(sl => sl.BatchId)
                .Select(g => new { BatchId = g.Key.Value, Stock = g.Sum(sl => sl.Quantity) })
                .ToDictionaryAsync(x => x.BatchId, x => x.Stock, cancellationToken);

            var textBuilder = new StringBuilder("Here are the batches expiring soon:\n");
            var chartData = new List<object>();

            foreach (var batch in expiring)
            {
                batchStocks.TryGetValue(batch.Id, out decimal stock);

                int daysLeft = (batch.ExpiryDate.Value - today).Days;
                textBuilder.AppendLine($"- **{batch.Product.Name}** (Batch: {batch.BatchNumber}) - Expiring in {daysLeft} days. (Stock: {stock})");
                chartData.Add(new { name = $"{batch.Product.Name} ({batch.BatchNumber})", value = daysLeft });
            }

            return Ok(new
            {
                text = textBuilder.ToString(),
                chartType = "BAR",
                chartData = chartData
            });
        }

        if (promptLower.Contains("stock") || promptLower.Contains("low stock") || promptLower.Contains("inventory levels"))
        {
            var products = await _context.Products.Where(p => !p.IsDeleted).ToListAsync(cancellationToken);
            
            var productStocks = await _context.StockLedger
                .GroupBy(sl => sl.ProductId)
                .Select(g => new { ProductId = g.Key, Stock = g.Sum(sl => sl.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Stock, cancellationToken);

            var lowStockItems = new List<object>();
            var textBuilder = new StringBuilder("Items with low stock levels (< 10 units):\n");

            foreach (var p in products)
            {
                productStocks.TryGetValue(p.Id, out decimal stock);

                if (stock < 10)
                {
                    textBuilder.AppendLine($"- **{p.Name}** ({p.ProductCode}): {stock} units");
                    lowStockItems.Add(new { name = p.Name, value = (double)stock });
                }
            }

            if (lowStockItems.Count == 0)
            {
                return Ok(new { text = "All product stock levels are healthy (> 10 units)." });
            }

            return Ok(new
            {
                text = textBuilder.ToString(),
                chartType = "BAR",
                chartData = lowStockItems.Take(8).ToList()
            });
        }


        // B. Attempt connection to Ollama LLM
        try
        {
            var ollamaUrl = "http://pos_ollama:11434/api/generate";
            // Check if pos_ollama responds, otherwise try localhost (in case of local standalone testing)
            try
            {
                var responseTest = await _httpClient.GetAsync("http://pos_ollama:11434");
            }
            catch
            {
                ollamaUrl = "http://localhost:11434/api/generate";
            }

            var requestBody = new
            {
                model = "llama2", // or whatever model is pre-pulled, fallback friendly
                prompt = $"You are an expert AI Co-pilot assistant for the Apple Supermarket POS & ERP. Answer this query professionally in a retail store context. Query: {request.Prompt}",
                stream = false
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(ollamaUrl, jsonContent, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var resString = await response.Content.ReadAsStringAsync(cancellationToken);
                var doc = JsonDocument.Parse(resString);
                var responseText = doc.RootElement.GetProperty("response").GetString();
                return Ok(new { text = responseText });
            }
        }
        catch (Exception ex)
        {
            // Log or ignore to fall back
            Console.WriteLine($"Ollama error: {ex.Message}");
        }

        // C. Fallback response when Ollama is offline
        return Ok(new
        {
            text = "I am your AI Assistant. I couldn't reach the Ollama LLM service, but I can query the POS database for you! Try asking me: \n" +
                   "- *'Show top selling products'* to view sales volume graphs.\n" +
                   "- *'Sales split'* or *'cash vs upi'* to view payment statistics.\n" +
                   "- *'Show expiry alerts'* to review soon-to-expire batch stocks.\n" +
                   "- *'Low stock'* to see items running low in the warehouse."
        });
    }
}
