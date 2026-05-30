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
        
        // SMART LOGIC: Only fetch products that have actual sales history in the last 30 days.
        // This prevents the 0.15 fallback from flooding the report with every product in the catalog.
        var salesData = await _context.StockLedger
            .Where(sl => sl.MovementType == "SALE" && sl.BusinessDate >= cutoffDate)
            .GroupBy(sl => sl.ProductId)
            .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(sl => -sl.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.TotalSold, cancellationToken);

        // Also identify products that were ever received via GRN (they are stocked products, not phantom catalog entries)
        var grnProductIds = await _context.GRNItems
            .Select(gi => gi.ProductId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Only consider products that have EITHER: recent sales data OR a GRN history (ever received)
        // This eliminates 35,000+ unused catalog entries from polluting the report
        var eligibleProductIds = salesData.Keys.Union(grnProductIds).ToHashSet();

        if (!eligibleProductIds.Any())
        {
            return Ok(new List<object>());
        }

        // Fetch current stock only for eligible products
        var currentStocks = await _context.StockLedger
            .Where(sl => eligibleProductIds.Contains(sl.ProductId))
            .GroupBy(sl => sl.ProductId)
            .Select(g => new { ProductId = g.Key, Stock = g.Sum(sl => sl.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Stock, cancellationToken);

        var products = await _context.Products
            .Where(p => !p.IsDeleted && p.IsActive && eligibleProductIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        var suppliers = await _context.Suppliers.Where(s => s.IsActive).ToListAsync(cancellationToken);
        var defaultSupplier = suppliers.FirstOrDefault() ?? new Supplier { Id = Guid.NewGuid(), Name = "Default Local Supplier" };
        var supplierLookup = suppliers.ToDictionary(s => s.Id);

        // Fetch supplier preference per product from GRN history (one query, in-memory lookup)
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

            // SMART LOGIC: Skip products with zero sales AND sufficient stock.
            // Only include products that have REAL sales velocity OR genuinely zero stock (ran out).
            if (avgDailySales == 0 && currentStock > 0)
            {
                continue; // No sales, still has stock — no action needed
            }

            // For out-of-stock products with no recent sales, use a conservative minimum
            // (they were stocked at some point since they have GRN history)
            if (avgDailySales == 0 && currentStock <= 0)
            {
                avgDailySales = 0.1m; // Very conservative - just enough to suggest a minimal reorder
            }

            decimal forecasted15DayDemand = avgDailySales * 15m;
            decimal safetyStock = avgDailySales * 5m;
            decimal requiredQuantity = (forecasted15DayDemand + safetyStock) - currentStock;

            if (requiredQuantity > 0)
            {
                int recommendedOrder = (int)Math.Ceiling(requiredQuantity);

                // Calculate urgency score: 0 = mildly low, higher = more critical
                // Products with 0 stock and high sales rate get highest urgency
                decimal urgencyScore = avgDailySales > 0 && currentStock <= 0
                    ? avgDailySales * 100m
                    : requiredQuantity;
                
                // Find supplier from GRN history
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
                    totalCost = recommendedOrder * product.PurchasePrice,
                    urgencyScore = urgencyScore
                });
            }
        }

        // Sort by urgency (most critical first) and cap at 150 results to prevent UI overload
        var sortedRecommendations = recommendations
            .Cast<dynamic>()
            .OrderByDescending(r => r.urgencyScore)
            .Take(150)
            .Select(r => (object)r)
            .ToList();

        return Ok(sortedRecommendations);
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
        // NOTE: BusinessDate is a PostgreSQL 'date' column. 
        // Use equality (==) with DateTime.Date values, matching the pattern used in GetTodayDashboardQuery.

        try
        {

        // ── Yesterday's Sales ─────────────────────────────────────────
        if (promptLower.Contains("yesterday"))
        {
            var yesterday = DateTime.UtcNow.Date.AddDays(-1);
            var yesterdayInvoices = await _context.Invoices
                .Where(i => i.BusinessDate == yesterday)
                .ToListAsync(cancellationToken);

            int count = yesterdayInvoices.Count;
            decimal total = yesterdayInvoices.Sum(i => i.TotalAmount);
            decimal cash = yesterdayInvoices.Sum(i => i.CashAmount);
            decimal upi = yesterdayInvoices.Sum(i => i.UpiAmount);

            return Ok(new
            {
                text = $"Yesterday's Summary ({yesterday:dd MMM yyyy}):\n" +
                       $"- Total Invoices: {count}\n" +
                       $"- Total Sales: ₹{total:N2}\n" +
                       $"- Cash Collections: ₹{cash:N2}\n" +
                       $"- UPI Collections: ₹{upi:N2}"
            });
        }

        // ── Last Week's Sales ─────────────────────────────────────────
        if (promptLower.Contains("last week") || promptLower.Contains("past week") || promptLower.Contains("previous week"))
        {
            var weekStart = DateTime.UtcNow.Date.AddDays(-7);
            var weekEnd = DateTime.UtcNow.Date.AddDays(-1);
            var weekInvoices = await _context.Invoices
                .Where(i => i.BusinessDate >= weekStart && i.BusinessDate <= weekEnd)
                .ToListAsync(cancellationToken);

            int count = weekInvoices.Count;
            decimal total = weekInvoices.Sum(i => i.TotalAmount);
            decimal cash = weekInvoices.Sum(i => i.CashAmount);
            decimal upi = weekInvoices.Sum(i => i.UpiAmount);
            decimal daily = count > 0 ? total / 7m : 0;

            return Ok(new
            {
                text = $"Last 7 Days Sales Summary ({weekStart:dd MMM} – {weekEnd:dd MMM yyyy}):\n" +
                       $"- Total Invoices: {count}\n" +
                       $"- Total Sales: ₹{total:N2}\n" +
                       $"- Avg Daily Sales: ₹{daily:N2}\n" +
                       $"- Cash Collections: ₹{cash:N2}\n" +
                       $"- UPI Collections: ₹{upi:N2}"
            });
        }

        // ── This Month's Sales ────────────────────────────────────────
        if (promptLower.Contains("this month") || promptLower.Contains("current month") || promptLower.Contains("monthly sales"))
        {
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var today = DateTime.UtcNow.Date;
            var monthInvoices = await _context.Invoices
                .Where(i => i.BusinessDate >= monthStart && i.BusinessDate <= today)
                .ToListAsync(cancellationToken);

            int count = monthInvoices.Count;
            decimal total = monthInvoices.Sum(i => i.TotalAmount);
            decimal cash = monthInvoices.Sum(i => i.CashAmount);
            decimal upi = monthInvoices.Sum(i => i.UpiAmount);
            int daysElapsed = (today - monthStart).Days + 1;
            decimal daily = daysElapsed > 0 ? total / daysElapsed : 0;

            return Ok(new
            {
                text = $"This Month's Sales ({monthStart:MMMM yyyy}):\n" +
                       $"- Total Invoices: {count}\n" +
                       $"- Total Sales: ₹{total:N2}\n" +
                       $"- Avg Daily Sales: ₹{daily:N2}\n" +
                       $"- Cash Collections: ₹{cash:N2}\n" +
                       $"- UPI Collections: ₹{upi:N2}"
            });
        }

        // ── Last Month's Sales ────────────────────────────────────────
        if (promptLower.Contains("last month") || promptLower.Contains("previous month"))
        {
            var lastMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1);
            var lastMonthEnd = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddDays(-1);
            var lastMonthInvoices = await _context.Invoices
                .Where(i => i.BusinessDate >= lastMonthStart && i.BusinessDate <= lastMonthEnd)
                .ToListAsync(cancellationToken);

            int count = lastMonthInvoices.Count;
            decimal total = lastMonthInvoices.Sum(i => i.TotalAmount);
            decimal cash = lastMonthInvoices.Sum(i => i.CashAmount);
            decimal upi = lastMonthInvoices.Sum(i => i.UpiAmount);
            int daysInMonth = (lastMonthEnd - lastMonthStart).Days + 1;
            decimal daily = daysInMonth > 0 ? total / daysInMonth : 0;

            return Ok(new
            {
                text = $"Last Month's Sales ({lastMonthStart:MMMM yyyy}):\n" +
                       $"- Total Invoices: {count}\n" +
                       $"- Total Sales: ₹{total:N2}\n" +
                       $"- Avg Daily Sales: ₹{daily:N2}\n" +
                       $"- Cash Collections: ₹{cash:N2}\n" +
                       $"- UPI Collections: ₹{upi:N2}"
            });
        }

        // ── Today's Sales ─────────────────────────────────────────────
        if (promptLower.Contains("today") || promptLower.Contains("today's"))
        {
            var today = DateTime.UtcNow.Date;
            var todayInvoices = await _context.Invoices
                .Where(i => i.BusinessDate == today)
                .ToListAsync(cancellationToken);

            int count = todayInvoices.Count;
            decimal total = todayInvoices.Sum(i => i.TotalAmount);
            decimal cash = todayInvoices.Sum(i => i.CashAmount);
            decimal upi = todayInvoices.Sum(i => i.UpiAmount);

            return Ok(new
            {
                text = $"Today's Summary ({today:dd MMM yyyy}):\n" +
                       $"- Total Invoices: {count}\n" +
                       $"- Total Sales: ₹{total:N2}\n" +
                       $"- Cash Collections: ₹{cash:N2}\n" +
                       $"- UPI Collections: ₹{upi:N2}"
            });
        }

        } // end try
        catch (Exception dateEx)
        {
            Console.WriteLine($"Chat date query error: {dateEx.Message}");
            return Ok(new { text = $"I encountered an issue querying sales data: {dateEx.Message}" });
        }

        // ── Total Sales ───────────────────────────────────────────────
        if (promptLower.Contains("total sales") || promptLower.Contains("total invoice") || promptLower.Contains("total revenue") || promptLower.Contains("sales figure"))
        {
            var allInvoices = await _context.Invoices.ToListAsync(cancellationToken);
            decimal total = allInvoices.Sum(i => i.TotalAmount);
            int count = allInvoices.Count;

            return Ok(new
            {
                text = $"Overall Sales Summary (All Time):\n" +
                       $"- Total Invoices Raised: {count}\n" +
                       $"- Total Revenue: ₹{total:N2}"
            });
        }

        // ── Top Sellers ────────────────────────────────────────────────
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
            var baseUrl = "http://pos_ollama:11434";
            try
            {
                // Check if pos_ollama responds quickly
                using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _httpClient.GetAsync(baseUrl, testCts.Token);
            }
            catch
            {
                baseUrl = "http://localhost:11434";
            }

            // 1. Fetch available models from /api/tags
            string activeModel = "llama2";
            bool hasModels = false;
            try
            {
                using var tagsClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var tagsResponse = await tagsClient.GetAsync($"{baseUrl}/api/tags", cancellationToken);
                if (tagsResponse.IsSuccessStatusCode)
                {
                    var tagsJson = await tagsResponse.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(tagsJson);
                    if (doc.RootElement.TryGetProperty("models", out var modelsArr) && modelsArr.ValueKind == JsonValueKind.Array)
                    {
                        var modelList = new List<string>();
                        foreach (var m in modelsArr.EnumerateArray())
                        {
                            if (m.TryGetProperty("name", out var nameProp))
                            {
                                modelList.Add(nameProp.GetString());
                            }
                        }

                        if (modelList.Count > 0)
                        {
                            // Use first model (prefer Qwen or Llama if present)
                            activeModel = modelList.FirstOrDefault(name => name.Contains("qwen") || name.Contains("llama")) ?? modelList[0];
                            hasModels = true;
                        }
                    }
                }
            }
            catch (Exception tagsEx)
            {
                Console.WriteLine($"Error fetching Ollama models: {tagsEx.Message}");
            }

            if (!hasModels)
            {
                // Ollama is online but has no models pulled yet
                return Ok(new
                {
                    text = "🤖 **Ollama LLM Service is Online**, but no AI models have been downloaded yet.\n\n" +
                           "Please pull a model on the host server to enable general question answering:\n" +
                           "```bash\n" +
                           "docker exec -it pos_ollama ollama pull qwen2.5:7b-instruct\n" +
                           "```\n" +
                           "*Meanwhile, you can use the quick prompt buttons below to query the database directly.*"
                });
            }

            // 2. Query the LLM with a 90-second timeout to handle CPU-only generation safely
            using var ollamaClient = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            var requestBody = new
            {
                model = activeModel,
                prompt = $"You are an expert AI Co-pilot assistant for the Apple Supermarket POS & ERP. Answer this query professionally in a retail store context. Query: {request.Prompt}",
                stream = false
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await ollamaClient.PostAsync($"{baseUrl}/api/generate", jsonContent, cancellationToken);
            
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
            Console.WriteLine($"Ollama generation error: {ex.Message}");
        }

        // C. Fallback response when Ollama is offline and query doesn't match rules
        return Ok(new
        {
            text = "I'm your AI Co-pilot. I can query the POS database for these questions:\n" +
                   "- *'Yesterday's sales'* or *'Today's sales'* for daily summaries\n" +
                   "- *'Total revenue'* for overall sales figures\n" +
                   "- *'Show top selling products'* for sales volume charts\n" +
                   "- *'Sales split'* or *'Cash vs UPI'* for payment breakdown\n" +
                   "- *'Show expiry alerts'* for soon-to-expire batch stocks\n" +
                   "- *'Low stock'* for items running low in the warehouse"
        });
    }
}
