using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Inventory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Commands.CreateOrUpdateStockTake;

public record CreateOrUpdateStockTakeCommand(
    Guid? Id,
    Guid? StoreId,
    DateTime ScheduledDate,
    string Status, // DRAFT or REVIEW
    List<StockTakeItemInputDto> Items,
    Guid? UserId
) : IRequest<Guid>;

public record StockTakeItemInputDto(
    Guid ProductId,
    Guid? BatchId,
    decimal PhysicalQuantity
);

public class CreateOrUpdateStockTakeCommandHandler : IRequestHandler<CreateOrUpdateStockTakeCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateOrUpdateStockTakeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateOrUpdateStockTakeCommand request, CancellationToken cancellationToken)
    {
        StockTakeHeader take;

        if (request.Id.HasValue)
        {
            // Load existing draft
            take = await _context.StockTakeHeaders
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == request.Id.Value, cancellationToken);

            if (take == null) throw new Exception("Stock Take not found.");
            if (take.Status != "DRAFT") throw new Exception("Only DRAFT Stock Takes can be updated.");

            take.StoreId = request.StoreId;
            take.ScheduledDate = request.ScheduledDate;
            take.Status = request.Status;

            // Clear old items (EF Core will delete them)
            take.Items.Clear();
        }
        else
        {
            // Create new
            take = new StockTakeHeader
            {
                StoreId = request.StoreId,
                TakeNumber = $"STK-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                ScheduledDate = request.ScheduledDate,
                Status = request.Status,
                CreatedAt = DateTime.UtcNow
            };
            _context.StockTakeHeaders.Add(take);
        }

        foreach (var item in request.Items)
        {
            // Compute live system stock for this product/batch
            decimal systemQty = 0;
            if (item.BatchId.HasValue)
            {
                systemQty = await _context.StockLedger
                    .Where(sl => sl.ProductId == item.ProductId && sl.BatchId == item.BatchId)
                    .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;
            }
            else
            {
                systemQty = await _context.StockLedger
                    .Where(sl => sl.ProductId == item.ProductId && sl.BatchId == null)
                    .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;
            }

            take.Items.Add(new StockTakeItem
            {
                ProductId = item.ProductId,
                BatchId = item.BatchId,
                SystemQuantity = systemQty,
                PhysicalQuantity = item.PhysicalQuantity
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return take.Id;
    }
}
