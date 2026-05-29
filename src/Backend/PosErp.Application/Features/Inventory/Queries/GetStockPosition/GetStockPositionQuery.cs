using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Queries.GetStockPosition;

public class StockPositionSummaryDto
{
    public int TotalCount { get; set; }
    public decimal TotalValue { get; set; }
}

public record StockPositionListDto(
    List<StockPositionDto> Items,
    int TotalCount,
    decimal TotalValue
);

public record GetStockPositionQuery(
    Guid? StoreId,
    Guid? CategoryId,
    string? SearchTerm,
    int Page = 1,
    int PageSize = 50
) : IRequest<StockPositionListDto>;

public class StockPositionDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal LastUnitCost { get; set; }
    public decimal TotalValue { get; set; }
}

public class GetStockPositionQueryHandler : IRequestHandler<GetStockPositionQuery, StockPositionListDto>
{
    private readonly IApplicationDbContext _context;

    public GetStockPositionQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StockPositionListDto> Handle(GetStockPositionQuery request, CancellationToken cancellationToken)
    {
        var db = (DbContext)_context;

        var searchToken = string.IsNullOrEmpty(request.SearchTerm) ? null : request.SearchTerm;

        // Get summary information (total count and total valuation)
        var summary = await db.Database.SqlQuery<StockPositionSummaryDto>(@$"
            SELECT 
                CAST(COUNT(*) AS integer) as ""TotalCount"",
                COALESCE(SUM(COALESCE(mv.current_stock, 0) * COALESCE(mv.last_unit_cost, p.purchase_price, 0)), 0)::numeric as ""TotalValue""
            FROM products p
            LEFT JOIN mv_current_stock mv ON p.id = mv.product_id AND (mv.store_id = {request.StoreId} OR cast({request.StoreId} as uuid) IS NULL)
            WHERE (cast({request.CategoryId} as uuid) IS NULL OR p.category_id = {request.CategoryId})
              AND (cast({searchToken} as text) IS NULL OR p.name ILIKE '%' || {searchToken} || '%' OR p.product_code ILIKE '%' || {searchToken} || '%')
        ").FirstOrDefaultAsync(cancellationToken);

        var sql = @$"
            SELECT 
                p.id as ""ProductId"",
                p.product_code as ""ProductCode"",
                p.name as ""ProductName"",
                COALESCE(c.name, 'General') as ""CategoryName"",
                COALESCE(mv.current_stock, 0) as ""CurrentStock"",
                COALESCE(mv.last_unit_cost, p.purchase_price, 0) as ""LastUnitCost"",
                (COALESCE(mv.current_stock, 0) * COALESCE(mv.last_unit_cost, p.purchase_price, 0)) as ""TotalValue""
            FROM products p
            LEFT JOIN categories c ON p.category_id = c.id
            LEFT JOIN mv_current_stock mv ON p.id = mv.product_id AND (mv.store_id = {{0}} OR cast({{0}} as uuid) IS NULL)
            WHERE (cast({{1}} as uuid) IS NULL OR p.category_id = {{1}})
              AND (cast({{2}} as text) IS NULL OR p.name ILIKE '%' || {{2}} || '%' OR p.product_code ILIKE '%' || {{2}} || '%')
            ORDER BY p.name";

        if (request.PageSize > 0)
        {
            var offset = (request.Page - 1) * request.PageSize;
            sql += $" LIMIT {request.PageSize} OFFSET {offset}";
        }

        var items = await db.Database.SqlQueryRaw<StockPositionDto>(sql, request.StoreId, request.CategoryId, searchToken)
            .ToListAsync(cancellationToken);

        return new StockPositionListDto(items, summary?.TotalCount ?? 0, summary?.TotalValue ?? 0);
    }
}
