using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Purchasing.Queries.GetSuppliers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Purchasing.Queries.GetSupplierById;

public class GetSupplierByIdQuery : IRequest<SupplierDto?>
{
    public Guid Id { get; set; }
}

public class GetSupplierByIdQueryHandler : IRequestHandler<GetSupplierByIdQuery, SupplierDto?>
{
    private readonly IApplicationDbContext _context;

    public GetSupplierByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SupplierDto?> Handle(GetSupplierByIdQuery request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (supplier == null) return null;

        return new SupplierDto
        {
            Id = supplier.Id,
            Name = supplier.Name,
            Gstin = supplier.Gstin,
            Phone = supplier.Phone,
            PaymentTerms = supplier.PaymentTerms,
            IsActive = supplier.IsActive
        };
    }
}
