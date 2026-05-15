using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Inventory;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Commands.CreateStockAdjustment;

public record CreateStockAdjustmentCommand(
    Guid? StoreId,
    string Reason, // DAMAGE, THEFT, FOUND
    List<StockAdjustmentItemDto> Items,
    Guid? UserId
) : IRequest<Guid>;

public record StockAdjustmentItemDto(
    Guid ProductId,
    Guid? BatchId,
    decimal AdjustedQuantity, // Can be negative (damage) or positive (found)
    decimal UnitCost
);

public class CreateStockAdjustmentCommandHandler : IRequestHandler<CreateStockAdjustmentCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateStockAdjustmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateStockAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var adjustment = new StockAdjustment
        {
            StoreId = request.StoreId,
            AdjustmentNumber = $"ADJ-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0,4).ToUpper()}",
            Reason = request.Reason,
            Status = "PENDING"
        };

        foreach (var dto in request.Items)
        {
            adjustment.Items.Add(new StockAdjustmentItem
            {
                ProductId = dto.ProductId,
                BatchId = dto.BatchId,
                AdjustedQuantity = dto.AdjustedQuantity,
                UnitCost = dto.UnitCost
            });
        }

        _context.StockAdjustments.Add(adjustment);
        await _context.SaveChangesAsync(cancellationToken);

        return adjustment.Id;
    }
}
