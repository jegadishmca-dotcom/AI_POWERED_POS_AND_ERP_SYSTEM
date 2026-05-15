using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Crm;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace PosErp.Application.Features.Crm.Commands.RegisterCustomer;

public record RegisterCustomerCommand(
    string Phone,
    string Name,
    string? TamilName,
    DateTime? Dob,
    bool MarketingConsent
) : IRequest<Guid>;

public class RegisterCustomerCommandHandler : IRequestHandler<RegisterCustomerCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public RegisterCustomerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(RegisterCustomerCommand request, CancellationToken cancellationToken)
    {
        var existing = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == request.Phone, cancellationToken);
        if (existing != null) throw new Exception("Customer with this phone already exists.");

        var customer = new Customer
        {
            Phone = request.Phone,
            Name = request.Name,
            TamilName = request.TamilName,
            Dob = request.Dob,
            MarketingConsent = request.MarketingConsent,
            ConsentRecordedAt = DateTime.UtcNow,
            MembershipCardNumber = $"MEM-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString().Substring(0,4).ToUpper()}"
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}
