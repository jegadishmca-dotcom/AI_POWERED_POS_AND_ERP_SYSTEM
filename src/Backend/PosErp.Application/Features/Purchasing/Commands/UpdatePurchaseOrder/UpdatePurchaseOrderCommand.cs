using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Purchasing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace PosErp.Application.Features.Purchasing.Commands.UpdatePurchaseOrder;

public record UpdatePurchaseOrderCommand(
    Guid PurchaseOrderId,
    Guid SupplierId,
    DateTime ExpectedDeliveryDate,
    List<UpdatePurchaseOrderItemDto> Items
) : IRequest<bool>;

public record UpdatePurchaseOrderItemDto(
    Guid ProductId,
    decimal OrderedQuantity,
    decimal UnitCost
);

public class UpdatePurchaseOrderCommandHandler : IRequestHandler<UpdatePurchaseOrderCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdatePurchaseOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId, cancellationToken);

        if (po == null) throw new Exception("Purchase Order not found.");
        if (po.Status != "DRAFT") throw new Exception("Only DRAFT Purchase Orders can be updated.");

        po.SupplierId = request.SupplierId;
        po.ExpectedDeliveryDate = request.ExpectedDeliveryDate;
        po.TotalAmount = 0m;

        // Clear existing items and re-add
        po.Items.Clear();

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

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
