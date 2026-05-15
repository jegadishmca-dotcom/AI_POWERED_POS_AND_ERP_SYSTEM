using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Purchasing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace PosErp.Application.Features.Purchasing.Commands.CreatePurchaseOrder;

public record CreatePurchaseOrderCommand(
    Guid? StoreId,
    Guid SupplierId,
    DateTime ExpectedDeliveryDate,
    List<PurchaseOrderItemDto> Items,
    Guid? UserId
) : IRequest<Guid>;

public record PurchaseOrderItemDto(
    Guid ProductId,
    decimal OrderedQuantity,
    decimal UnitCost
);

public class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreatePurchaseOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = new PurchaseOrderHeader
        {
            StoreId = request.StoreId,
            SupplierId = request.SupplierId,
            PoNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
            PoDate = DateTime.UtcNow,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            Status = "DRAFT",
            CreatedBy = request.UserId
        };

        foreach (var itemDto in request.Items)
        {
            var itemTotal = itemDto.OrderedQuantity * itemDto.UnitCost;
            po.Items.Add(new PurchaseOrderItem
            {
                ProductId = itemDto.ProductId,
                OrderedQuantity = itemDto.OrderedQuantity,
                ReceivedQuantity = 0,
                UnitCost = itemDto.UnitCost,
                TotalCost = itemTotal
            });
            po.TotalAmount += itemTotal;
        }

        _context.PurchaseOrders.Add(po);
        await _context.SaveChangesAsync(cancellationToken);

        return po.Id;
    }
}
