using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Services;

public class AccountResolutionService : IAccountResolutionService
{
    private readonly IApplicationDbContext _context;

    public AccountResolutionService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> ResolveAccountCodeAsync(string accountType, string namePattern, string fallbackCode, CancellationToken cancellationToken)
    {
        var accounts = await _context.Accounts
            .Where(a => a.IsActive && a.AccountType == accountType &&
                        !IAccountResolutionService.LegacyExcludedCodes.Contains(a.AccountCode) &&
                        !_context.Accounts.Any(sub => sub.ParentAccountId == a.Id && sub.IsActive))
            .OrderByDescending(a => a.AccountCode.Length)
            .ThenBy(a => a.AccountCode)
            .ToListAsync(cancellationToken);

        var matched = accounts.FirstOrDefault(a => a.Name.Contains(namePattern, StringComparison.OrdinalIgnoreCase))
                   ?? accounts.FirstOrDefault(a => a.AccountCode == fallbackCode);

        return matched?.AccountCode ?? fallbackCode;
    }
}
