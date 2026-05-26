using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Purchasing.Queries.GetPurchaseOrders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Purchasing.Queries.GetPurchaseOrderById;

public class GetPurchaseOrderByIdQuery : IRequest<PurchaseOrderDetailsDto?>
{
    public Guid Id { get; set; }
}

public class PurchaseOrderDetailsDto : PurchaseOrderDto
{
    public List<PurchaseOrderItemDetailDto> Items { get; set; } = new();
}

public class PurchaseOrderItemDetailDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public bool HasExpiry { get; set; }
}

public class GetPurchaseOrderByIdQueryHandler : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDetailsDto?>
{
    private readonly IApplicationDbContext _context;

    public GetPurchaseOrderByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PurchaseOrderDetailsDto?> Handle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (po == null) return null;

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.Id == po.SupplierId, cancellationToken);

        var dto = new PurchaseOrderDetailsDto
        {
            Id = po.Id,
            PoNumber = po.PoNumber,
            PoDate = po.PoDate,
            ExpectedDeliveryDate = po.ExpectedDeliveryDate,
            TotalAmount = po.TotalAmount,
            Status = po.Status,
            SupplierId = po.SupplierId,
            SupplierName = supplier?.Name ?? "Unknown",
            Items = po.Items.Select(i => new PurchaseOrderItemDetailDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = _context.Products.FirstOrDefault(p => p.Id == i.ProductId)?.Name ?? "Unknown Product",
                OrderedQuantity = i.OrderedQuantity,
                ReceivedQuantity = i.ReceivedQuantity,
                UnitCost = i.UnitCost,
                TotalCost = i.TotalCost,
                HasExpiry = _context.Products.Where(p => p.Id == i.ProductId).Select(p => p.HasExpiry).FirstOrDefault()
            }).ToList()
        };

        return dto;
    }
}
