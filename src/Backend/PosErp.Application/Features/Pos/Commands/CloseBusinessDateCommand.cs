using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands;

public record CloseBusinessDateCommand(Guid? StoreId = null, Guid? ClosedBy = null) : IRequest<DateTime>;

public class CloseBusinessDateCommandHandler : IRequestHandler<CloseBusinessDateCommand, DateTime>
{
    private readonly IApplicationDbContext _context;

    public CloseBusinessDateCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DateTime> Handle(CloseBusinessDateCommand request, CancellationToken cancellationToken)
    {
        var targetStoreId = request.StoreId ?? Guid.Empty;

        // Retrieve the current active business date
        var activeDate = await _context.StoreBusinessDates
            .Where(d => d.StoreId == targetStoreId && d.Status == "OPEN")
            .FirstOrDefaultAsync(cancellationToken);

        if (activeDate == null)
        {
            throw new InvalidOperationException("No active business date is open for this store.");
        }

        // Verify if any POS sessions (cashier shifts) are still open.
        // EOD requires all cash registers/drawers to be closed.
        var hasOpenSessions = await _context.PosSessions
            .Where(s => s.Status == "OPEN")
            .AnyAsync(cancellationToken);

        if (hasOpenSessions)
        {
            throw new InvalidOperationException("Cannot perform End-of-Day. There are still active terminal shifts/sessions open. Please close all cashier shifts first.");
        }

        // Close the business date
        activeDate.Status = "CLOSED";
        activeDate.ClosedAt = DateTime.UtcNow;
        activeDate.ClosedBy = request.ClosedBy;

        await _context.SaveChangesAsync(cancellationToken);

        return activeDate.BusinessDate;
    }
}
