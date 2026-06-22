using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Queries;

public record GetAccountsQuery(bool OnlyActive, bool BuildTree) : IRequest<List<AccountDto>>;

public class AccountDto
{
    public Guid Id { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public Guid? ParentAccountId { get; set; }
    public bool IsActive { get; set; }
    public List<AccountDto> Children { get; set; } = new();
}

public class GetAccountsQueryHandler : IRequestHandler<GetAccountsQuery, List<AccountDto>>
{
    private readonly IApplicationDbContext _context;

    public GetAccountsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AccountDto>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Accounts.AsNoTracking();
        if (request.OnlyActive)
        {
            query = query.Where(a => a.IsActive);
        }

        var accounts = await query
            .OrderBy(a => a.AccountCode)
            .Select(a => new AccountDto
            {
                Id = a.Id,
                AccountCode = a.AccountCode,
                Name = a.Name,
                AccountType = a.AccountType,
                ParentAccountId = a.ParentAccountId,
                IsActive = a.IsActive
            })
            .ToListAsync(cancellationToken);

        if (!request.BuildTree)
        {
            return accounts;
        }

        // Build Tree structure recursively
        var accountMap = accounts.ToDictionary(a => a.Id);
        var roots = new List<AccountDto>();

        foreach (var account in accounts)
        {
            if (account.ParentAccountId.HasValue && accountMap.TryGetValue(account.ParentAccountId.Value, out var parent))
            {
                parent.Children.Add(account);
            }
            else
            {
                roots.Add(account);
            }
        }

        return roots;
    }
}
