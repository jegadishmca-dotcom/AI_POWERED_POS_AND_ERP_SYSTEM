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

public record StockPositionDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string CategoryName,
    decimal CurrentStock,
    decimal LastUnitCost,
    decimal TotalValue
);

public class GetStockPositionQueryHandler : IRequestHandler<GetStockPositionQuery, List<StockPositionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStockPositionQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<StockPositionDto>> Handle(GetStockPositionQuery request, CancellationToken cancellationToken)
    {
        // In a real implementation, we would query the mv_current_stock via Dapper or EF Core Raw SQL
        // since EF Core doesn't natively map materialized views without a defined entity.
        // For scaffold purposes, we simulate the query structure.

        var sql = @"
            SELECT 
                p.id as ProductId,
                p.product_code as ProductCode,
                p.name as ProductName,
                c.name as CategoryName,
                COALESCE(mv.current_stock, 0) as CurrentStock,
                COALESCE(mv.last_unit_cost, 0) as LastUnitCost
            FROM products p
            LEFT JOIN categories c ON p.category_id = c.id
            LEFT JOIN mv_current_stock mv ON p.id = mv.product_id AND (mv.store_id = @p0 OR @p0 IS NULL)
            WHERE (@p1 IS NULL OR p.category_id = @p1)
              AND (@p2 IS NULL OR p.name ILIKE '%' || @p2 || '%' OR p.product_code ILIKE '%' || @p2 || '%')
            ORDER BY p.name";

        // Execution logic omitted for scaffold...
        
        return new List<StockPositionDto>();
    }
}
