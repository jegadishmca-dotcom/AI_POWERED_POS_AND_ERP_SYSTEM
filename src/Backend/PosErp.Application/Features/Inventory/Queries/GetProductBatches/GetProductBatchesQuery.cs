using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Queries.GetProductBatches;

public record GetProductBatchesQuery(Guid ProductId) : IRequest<List<ProductBatchDto>>;

public class ProductBatchDto
{
    public Guid Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public decimal CurrentStock { get; set; }
}

public class GetProductBatchesQueryHandler : IRequestHandler<GetProductBatchesQuery, List<ProductBatchDto>>
{
    private readonly IApplicationDbContext _context;

    public GetProductBatchesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductBatchDto>> Handle(GetProductBatchesQuery request, CancellationToken cancellationToken)
    {
        // Fetch all active batches for the given ProductId
        var batches = await _context.ProductBatches
            .Where(b => b.ProductId == request.ProductId && b.IsActive)
            .ToListAsync(cancellationToken);

        var result = new List<ProductBatchDto>();

        foreach (var b in batches)
        {
            // Sum ledger quantities to get current stock of this specific batch
            var currentStock = await _context.StockLedger
                .Where(sl => sl.ProductId == request.ProductId && sl.BatchId == b.Id)
                .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;

            result.Add(new ProductBatchDto
            {
                Id = b.Id,
                BatchNumber = b.BatchNumber,
                ExpiryDate = b.ExpiryDate,
                CurrentStock = currentStock
            });
        }

        // Sort by FEFO: nearest expiry first. Null expiries (non-perishables or incomplete records) are put last.
        return result
            .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
            .ThenBy(b => b.ExpiryDate)
            .ThenBy(b => b.BatchNumber)
            .ToList();
    }
}
