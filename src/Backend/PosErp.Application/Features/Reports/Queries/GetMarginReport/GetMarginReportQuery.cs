using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Reports.Queries.GetMarginReport;

public record GetMarginReportQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<MarginReportDto>;

public class MarginReportDto
{
    public MarginSummaryDto Summary { get; set; } = new();
    public List<CategoryMarginDto> CategoryMargins { get; set; } = new();
    public List<ProductMarginDto> ProductMargins { get; set; } = new();
}

public class MarginSummaryDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal MarginPercentage { get; set; }
}

public class CategoryMarginDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public decimal MarginPercentage { get; set; }
}

public class ProductMarginDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public decimal MarginPercentage { get; set; }
}

public class GetMarginReportQueryHandler : IRequestHandler<GetMarginReportQuery, MarginReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetMarginReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MarginReportDto> Handle(GetMarginReportQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate?.Date ?? DateTime.UtcNow.Date.AddDays(-30);
        var toDate = request.ToDate?.Date ?? DateTime.UtcNow.Date;

        var categories = await _context.Categories
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        // Fetch completed invoice items in the range, including the product info for cost basis
        var items = await _context.InvoiceItems
            .Include(ii => ii.Product)
            .Where(ii => ii.Invoice.BusinessDate >= fromDate && ii.Invoice.BusinessDate <= toDate && ii.Invoice.Status == "COMPLETED")
            .Select(ii => new {
                ProductId = ii.ProductId,
                ProductName = ii.ProductName,
                CategoryId = ii.Product != null ? ii.Product.CategoryId : null,
                Quantity = ii.Quantity,
                Revenue = ii.TotalAmount,
                Cost = ii.Quantity * (ii.Product != null ? ii.Product.PurchasePrice : 0m)
            })
            .ToListAsync(cancellationToken);

        if (!items.Any())
        {
            return new MarginReportDto();
        }

        // 1. Overall Summary
        decimal totalRevenue = items.Sum(x => x.Revenue);
        decimal totalCost = items.Sum(x => x.Cost);
        decimal totalProfit = totalRevenue - totalCost;
        decimal overallMargin = totalRevenue > 0 ? (totalProfit / totalRevenue) * 100 : 0;

        var summary = new MarginSummaryDto
        {
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            TotalProfit = totalProfit,
            MarginPercentage = overallMargin
        };

        // 2. Category margins
        var categoryMargins = items
            .GroupBy(x => (x.CategoryId.HasValue && categories.TryGetValue(x.CategoryId.Value, out var name)) ? name : "General")
            .Select(g => {
                var rev = g.Sum(x => x.Revenue);
                var cost = g.Sum(x => x.Cost);
                var profit = rev - cost;
                return new CategoryMarginDto
                {
                    CategoryName = g.Key,
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = rev,
                    Cost = cost,
                    Profit = profit,
                    MarginPercentage = rev > 0 ? (profit / rev) * 100 : 0
                };
            })
            .OrderByDescending(x => x.Profit)
            .ToList();

        // 3. Product margins
        var productMargins = items
            .GroupBy(x => new { x.ProductId, x.ProductName })
            .Select(g => {
                var rev = g.Sum(x => x.Revenue);
                var cost = g.Sum(x => x.Cost);
                var profit = rev - cost;
                return new ProductMarginDto
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    QuantitySold = g.Sum(x => x.Quantity),
                    Revenue = rev,
                    Cost = cost,
                    Profit = profit,
                    MarginPercentage = rev > 0 ? (profit / rev) * 100 : 0
                };
            })
            .OrderByDescending(x => x.Profit)
            .Take(15) // Top 15 profitable items
            .ToList();

        return new MarginReportDto
        {
            Summary = summary,
            CategoryMargins = categoryMargins,
            ProductMargins = productMargins
        };
    }
}
