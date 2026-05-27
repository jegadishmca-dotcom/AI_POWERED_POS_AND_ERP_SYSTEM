using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Purchasing;
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;

namespace PosErp.Application.Features.Purchasing.Commands.CreateSupplier;

public class CreateSupplierCommand : IRequest<Guid>
{
    public string Name { get; set; } = string.Empty;
    public string? Gstin { get; set; }
    public string? Phone { get; set; }
    public string PaymentTerms { get; set; } = "NET30";
}

public class CreateSupplierCommandValidator : AbstractValidator<CreateSupplierCommand>
{
    public CreateSupplierCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Supplier name is required.");
        RuleFor(x => x.Phone).NotEmpty().WithMessage("Supplier phone is required.");
    }
}

public class CreateSupplierCommandHandler : IRequestHandler<CreateSupplierCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateSupplierCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Gstin = request.Gstin,
            Phone = request.Phone,
            PaymentTerms = string.IsNullOrEmpty(request.PaymentTerms) ? "NET30" : request.PaymentTerms,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        return supplier.Id;
    }
}
