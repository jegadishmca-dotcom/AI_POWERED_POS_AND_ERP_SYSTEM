using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Inventory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Queries.GetStockLedger;

public record StockLedgerDto(
    Guid Id,
    DateTime Date,
    string ProductName,
    string MovementType,
    string ReferenceDocument,
    decimal DeltaQty,
    decimal RunningBalance,
    string? BatchNumber,
    DateTime? ExpiryDate,
    Guid ReferenceDocumentId
);

public record StockLedgerListDto(
    List<StockLedgerDto> Items,
    int TotalCount
);

public record GetStockLedgerQuery(
    Guid? StoreId,
    string? SearchToken,
    string? MovementType,
    int Page = 1,
    int PageSize = 50
) : IRequest<StockLedgerListDto>;

public class GetStockLedgerQueryHandler : IRequestHandler<GetStockLedgerQuery, StockLedgerListDto>
{
    private readonly IApplicationDbContext _context;

    public GetStockLedgerQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StockLedgerListDto> Handle(GetStockLedgerQuery request, CancellationToken cancellationToken)
    {
        var query = from sl in _context.StockLedger
                    join p in _context.Products on sl.ProductId equals p.Id
                    join b in _context.ProductBatches on sl.BatchId equals b.Id into bj
                    from b in bj.DefaultIfEmpty()
                    select new { sl, p, BatchNumber = b != null ? b.BatchNumber : null };

        if (request.StoreId.HasValue)
        {
            query = query.Where(x => x.sl.StoreId == request.StoreId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.MovementType))
        {
            query = query.Where(x => x.sl.MovementType == request.MovementType);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchToken))
        {
            var search = request.SearchToken.ToLower();
            query = query.Where(x => x.p.Name.ToLower().Contains(search) 
                                   || x.sl.ReferenceNumber.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var queryOrdered = query.OrderByDescending(x => x.sl.CreatedAt);
        var queryPaginated = request.PageSize > 0
            ? queryOrdered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            : queryOrdered;

        var results = await queryPaginated
            .Select(x => new StockLedgerDto(
                x.sl.Id,
                x.sl.CreatedAt,
                x.p.Name,
                x.sl.MovementType,
                x.sl.ReferenceNumber,
                x.sl.Quantity,
                x.sl.RunningBalance,
                x.BatchNumber,
                x.sl.ExpiryDate,
                x.sl.ReferenceDocumentId
            ))
            .ToListAsync(cancellationToken);

        return new StockLedgerListDto(results, totalCount);
    }
}
