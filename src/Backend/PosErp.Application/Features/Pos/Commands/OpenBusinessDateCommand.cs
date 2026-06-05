using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands;

public record OpenBusinessDateCommand(DateTime BusinessDate, Guid? StoreId = null, Guid? OpenedBy = null) : IRequest<bool>;

public class OpenBusinessDateCommandHandler : IRequestHandler<OpenBusinessDateCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public OpenBusinessDateCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(OpenBusinessDateCommand request, CancellationToken cancellationToken)
    {
        var targetStoreId = request.StoreId ?? Guid.Empty;

        // Check if there is already an active business date
        var activeDate = await _context.StoreBusinessDates
            .Where(d => d.StoreId == targetStoreId && d.Status == "OPEN")
            .AnyAsync(cancellationToken);

        if (activeDate)
        {
            throw new InvalidOperationException("Cannot open a new business date. An active business date is already open. Please perform End-of-Day (EOD) first.");
        }

        var targetDate = request.BusinessDate.Date;

        // Also make sure we don't open a date that has already been closed.
        // It's a standard practice in retail that business dates are unique and sequential.
        var dateExists = await _context.StoreBusinessDates
            .Where(d => d.StoreId == targetStoreId && d.BusinessDate == targetDate)
            .AnyAsync(cancellationToken);

        if (dateExists)
        {
            throw new InvalidOperationException($"The business date {targetDate:yyyy-MM-dd} was already used or closed. Please select a later date.");
        }

        var newBusinessDate = new StoreBusinessDate
        {
            StoreId = targetStoreId,
            BusinessDate = targetDate,
            Status = "OPEN",
            OpenedAt = DateTime.UtcNow,
            OpenedBy = request.OpenedBy
        };

        _context.StoreBusinessDates.Add(newBusinessDate);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
