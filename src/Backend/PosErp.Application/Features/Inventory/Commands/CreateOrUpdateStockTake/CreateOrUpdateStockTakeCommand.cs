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
        var utcScheduledDate = DateTime.SpecifyKind(request.ScheduledDate, DateTimeKind.Utc);

        if (request.Id.HasValue)
        {
            // Load existing draft
            take = await _context.StockTakeHeaders
                .Include(t => t.Items)
                .FirstOrDefaultAsync(t => t.Id == request.Id.Value, cancellationToken);

            if (take == null) throw new Exception("Stock Take not found.");
            if (take.Status != "DRAFT") throw new Exception("Only DRAFT Stock Takes can be updated.");

            take.StoreId = request.StoreId;
            take.ScheduledDate = utcScheduledDate;
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
                ScheduledDate = utcScheduledDate,
                Status = request.Status,
                CreatedAt = DateTime.UtcNow
            };
            _context.StockTakeHeaders.Add(take);
        }

        foreach (var item in request.Items)
        {
            // Compute live system stock for this product/batch
            decimal systemQty = 0;
            if (item.BatchId.HasValue && item.BatchId.Value != Guid.Empty)
            {
                var batch = await _context.ProductBatches
                    .FirstOrDefaultAsync(b => b.Id == item.BatchId.Value, cancellationToken);
                if (batch != null)
                {
                    systemQty = batch.AvailableQuantity;
                }
                else
                {
                    systemQty = await _context.StockLedger
                        .Where(sl => sl.ProductId == item.ProductId && sl.BatchId == item.BatchId)
                        .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;
                }
            }
            else
            {
                systemQty = await _context.StockLedger
                    .Where(sl => sl.ProductId == item.ProductId)
                    .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;
            }

            var newItem = new StockTakeItem
            {
                Id = Guid.NewGuid(),
                StockTakeHeaderId = take.Id,
                ProductId = item.ProductId,
                BatchId = (item.BatchId.HasValue && item.BatchId.Value != Guid.Empty) ? item.BatchId : null,
                SystemQuantity = systemQty,
                PhysicalQuantity = item.PhysicalQuantity
            };
            take.Items.Add(newItem);

            if (request.Id.HasValue && _context is DbContext dbContext)
            {
                dbContext.Entry(newItem).State = EntityState.Added;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return take.Id;
    }
}
