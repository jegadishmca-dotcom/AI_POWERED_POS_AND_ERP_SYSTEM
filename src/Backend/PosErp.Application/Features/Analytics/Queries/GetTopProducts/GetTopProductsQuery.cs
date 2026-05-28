using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Analytics.Queries.GetTopProducts;

public record GetTopProductsQuery(int Days = 30) : IRequest<List<TopProductDto>>;

public class TopProductDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal TotalQuantitySold { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class GetTopProductsQueryHandler : IRequestHandler<GetTopProductsQuery, List<TopProductDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTopProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TopProductDto>> Handle(GetTopProductsQuery request, CancellationToken cancellationToken)
    {
        // Querying the live tables. In production, this would hit the mv_product_sales_stats View.
        var fromDate = DateTime.UtcNow.Date.AddDays(-request.Days);

        return await _context.InvoiceItems
            .Where(ii => ii.Invoice.BusinessDate >= fromDate && ii.Invoice.Status == "COMPLETED")
            .GroupBy(ii => new { ii.ProductId, ii.ProductName })
            .Select(g => new TopProductDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                TotalQuantitySold = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.TotalRevenue)
            .Take(10)
            .ToListAsync(cancellationToken);
    }
}
