using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Crm.Queries.SearchCustomers;

public record SearchCustomersQuery(string Query) : IRequest<List<CustomerDto>>;

public record CustomerDto(Guid Id, string Phone, string Name, decimal WalletBalance, decimal LoyaltyPoints, string TierName);

public class SearchCustomersQueryHandler : IRequestHandler<SearchCustomersQuery, List<CustomerDto>>
{
    private readonly IApplicationDbContext _context;

    public SearchCustomersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CustomerDto>> Handle(SearchCustomersQuery request, CancellationToken cancellationToken)
    {
        var q = _context.Customers.Include(c => c.Tier).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            q = q.Where(c => c.Phone.Contains(request.Query) || c.Name.ToLower().Contains(request.Query.ToLower()));
        }

        return await q
            .Select(c => new CustomerDto(
                c.Id, c.Phone, c.Name, c.RunningWalletBalance, c.RunningLoyaltyPoints, c.Tier != null ? c.Tier.Name : "Base"
            ))
            .Take(20)
            .ToListAsync(cancellationToken);
    }
}
