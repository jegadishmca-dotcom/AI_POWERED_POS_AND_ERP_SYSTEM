using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Services;

public interface IPeriodLockService
{
    Task<bool> IsPeriodLockedAsync(Guid storeId, DateTime date, CancellationToken cancellationToken);
    Task CheckPeriodLockAsync(Guid storeId, DateTime date, CancellationToken cancellationToken);
}

public class PeriodLockService : IPeriodLockService
{
    private readonly IApplicationDbContext _context;

    public PeriodLockService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsPeriodLockedAsync(Guid storeId, DateTime date, CancellationToken cancellationToken)
    {
        var isLocked = await _context.FinancialPeriodLocks
            .AnyAsync(l => l.StoreId == storeId && l.IsLocked && date.Date >= l.StartDate.Date && date.Date <= l.EndDate.Date, cancellationToken);

        if (isLocked) return true;

        var isYearClosed = await _context.FinancialYears
            .AnyAsync(y => y.Status == "CLOSED" && date.Date >= y.StartDate.Date && date.Date <= y.EndDate.Date, cancellationToken);

        return isYearClosed;
    }

    public async Task CheckPeriodLockAsync(Guid storeId, DateTime date, CancellationToken cancellationToken)
    {
        if (await IsPeriodLockedAsync(storeId, date, cancellationToken))
        {
            throw new InvalidOperationException($"The financial period or year containing date {date:yyyy-MM-dd} is locked or closed. Postings are blocked.");
        }
    }
}
