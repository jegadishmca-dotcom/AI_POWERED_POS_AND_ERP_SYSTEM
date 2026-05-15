using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Crm.Services;

public interface ICustomerTierService
{
    Task EvaluateCustomerTierAsync(Guid customerId, CancellationToken cancellationToken);
}

public class CustomerTierService : ICustomerTierService
{
    private readonly IApplicationDbContext _context;

    public CustomerTierService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task EvaluateCustomerTierAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers.FindAsync(new object[] { customerId }, cancellationToken);
        if (customer == null) return;

        var last12Months = DateTime.UtcNow.AddMonths(-12);
        
        // In reality, sum completed invoices from the Invoices table
        decimal rollingSpend = await _context.Invoices
            .Where(i => i.CustomerId == customerId && i.CreatedAt >= last12Months && i.Status == "COMPLETED")
            .SumAsync(i => i.TotalAmount, cancellationToken);

        var eligibleTier = await _context.CustomerTiers
            .Where(t => rollingSpend >= t.MinimumSpend)
            .OrderByDescending(t => t.MinimumSpend)
            .FirstOrDefaultAsync(cancellationToken);

        if (eligibleTier != null && customer.CustomerTierId != eligibleTier.Id)
        {
            customer.CustomerTierId = eligibleTier.Id;
            // Audit log the tier change here in production
        }
    }
}
