using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Purchasing;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Purchasing.Queries.GetSuppliers;

public class GetSuppliersQuery : IRequest<List<SupplierDto>>
{
}

public class SupplierDto
{
    public System.Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Gstin { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, List<SupplierDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSuppliersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SupplierDto>> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        var suppliers = await _context.Suppliers
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto
            {
                Id = s.Id,
                Name = s.Name,
                Gstin = s.Gstin,
                Phone = s.Phone,
                PaymentTerms = s.PaymentTerms,
                IsActive = s.IsActive
            })
            .ToListAsync(cancellationToken);

        return suppliers;
    }
}
