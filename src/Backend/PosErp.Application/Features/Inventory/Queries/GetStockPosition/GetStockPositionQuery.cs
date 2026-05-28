using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Queries.GetStockPosition;

public record GetStockPositionQuery(Guid? StoreId, Guid? CategoryId, string? SearchTerm) : IRequest<List<StockPositionDto>>;

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

public class GetStockPositionQueryHandler : IRequestHandler<GetStockPositionQuery, List<StockPositionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStockPositionQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<StockPositionDto>> Handle(GetStockPositionQuery request, CancellationToken cancellationToken)
    {
        var db = (DbContext)_context;

        var searchToken = string.IsNullOrEmpty(request.SearchTerm) ? null : request.SearchTerm;

        var result = await db.Database.SqlQuery<StockPositionDto>(@$"
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
            LEFT JOIN mv_current_stock mv ON p.id = mv.product_id AND (mv.store_id = {request.StoreId} OR cast({request.StoreId} as uuid) IS NULL)
            WHERE (cast({request.CategoryId} as uuid) IS NULL OR p.category_id = {request.CategoryId})
              AND (cast({searchToken} as text) IS NULL OR p.name ILIKE '%' || {searchToken} || '%' OR p.product_code ILIKE '%' || {searchToken} || '%')
            ORDER BY p.name").ToListAsync(cancellationToken);

        return result;
    }
}
