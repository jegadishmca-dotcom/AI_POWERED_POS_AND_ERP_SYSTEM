using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Purchasing.Queries.GetPurchaseOrders;

public class GetPurchaseOrdersQuery : IRequest<List<PurchaseOrderDto>>
{
}

public class PurchaseOrderDto
{
    public Guid Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public DateTime PoDate { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
}

public class GetPurchaseOrdersQueryHandler : IRequestHandler<GetPurchaseOrdersQuery, List<PurchaseOrderDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPurchaseOrdersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PurchaseOrderDto>> Handle(GetPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        var pos = await _context.PurchaseOrders
            .OrderByDescending(p => p.PoDate)
            .Select(p => new PurchaseOrderDto
            {
                Id = p.Id,
                PoNumber = p.PoNumber,
                PoDate = p.PoDate,
                ExpectedDeliveryDate = p.ExpectedDeliveryDate,
                TotalAmount = p.TotalAmount,
                Status = p.Status,
                SupplierId = p.SupplierId,
                SupplierName = _context.Suppliers.FirstOrDefault(s => s.Id == p.SupplierId) != null ? _context.Suppliers.First(s => s.Id == p.SupplierId).Name : "Unknown"
            })
            .ToListAsync(cancellationToken);

        return pos;
    }
}
