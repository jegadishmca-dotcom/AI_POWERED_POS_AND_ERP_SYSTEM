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

        // 1. Determine items to delete (those in DB but NOT in the request)
        var incomingProductIds = request.Items.Select(i => i.ProductId).ToList();
        var itemsToRemove = po.Items.Where(i => !incomingProductIds.Contains(i.ProductId)).ToList();
        foreach (var item in itemsToRemove)
        {
            _context.PurchaseOrderItems.Remove(item);
        }

        // 2. Add or Update items
        po.TotalAmount = 0m;
        foreach (var itemDto in request.Items)
        {
            var existingItem = po.Items.FirstOrDefault(i => i.ProductId == itemDto.ProductId);
            var itemTotal = itemDto.OrderedQuantity * itemDto.UnitCost;

            if (existingItem != null)
            {
                // Update existing item in place
                existingItem.OrderedQuantity = itemDto.OrderedQuantity;
                existingItem.UnitCost = itemDto.UnitCost;
                existingItem.TotalCost = itemTotal;
            }
            else
            {
                // Add new item
                po.Items.Add(new PurchaseOrderItem
                {
                    ProductId = itemDto.ProductId,
                    OrderedQuantity = itemDto.OrderedQuantity,
                    ReceivedQuantity = 0,
                    UnitCost = itemDto.UnitCost,
                    TotalCost = itemTotal
                });
            }
            po.TotalAmount += itemTotal;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
