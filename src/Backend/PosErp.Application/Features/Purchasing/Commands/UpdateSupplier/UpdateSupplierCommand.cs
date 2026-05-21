using MediatR;
using PosErp.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace PosErp.Application.Features.Purchasing.Commands.UpdateSupplier;

public class UpdateSupplierCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Gstin { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class UpdateSupplierCommandValidator : AbstractValidator<UpdateSupplierCommand>
{
    public UpdateSupplierCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Phone).NotEmpty();
    }
}

public class UpdateSupplierCommandHandler : IRequestHandler<UpdateSupplierCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateSupplierCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        
        if (supplier == null)
            return false;

        supplier.Name = request.Name;
        supplier.Gstin = request.Gstin;
        supplier.Phone = request.Phone;
        supplier.PaymentTerms = string.IsNullOrEmpty(request.PaymentTerms) ? "NET30" : request.PaymentTerms;
        supplier.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
