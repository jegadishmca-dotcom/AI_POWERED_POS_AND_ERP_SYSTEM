using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands;

public record OpenBusinessDateCommand(DateTime BusinessDate, Guid? StoreId = null, Guid? OpenedBy = null, string? ManagerOverridePin = null) : IRequest<bool>;

public class OpenBusinessDateCommandHandler : IRequestHandler<OpenBusinessDateCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public OpenBusinessDateCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> Handle(OpenBusinessDateCommand request, CancellationToken cancellationToken)
    {
        // 1. Authorize user or Manager Override PIN
        if (request.OpenedBy.HasValue)
        {
            var openedByUser = await _context.Users
                .Join(_context.Roles,
                    u => u.RoleId,
                    r => r.Id,
                    (u, r) => new { User = u, Role = r })
                .FirstOrDefaultAsync(x => x.User.Id == request.OpenedBy.Value, cancellationToken);

            bool isAuthorizedUser = openedByUser != null && 
                (openedByUser.Role.Name == "Manager" || openedByUser.Role.Name == "Owner");

            if (!isAuthorizedUser)
            {
                if (string.IsNullOrWhiteSpace(request.ManagerOverridePin))
                {
                    throw new UnauthorizedAccessException("Manager or Owner authorization is required to open the business date.");
                }

                // Verify manager PIN
                var usersWithPin = await _context.Users
                    .Join(_context.Roles,
                        u => u.RoleId,
                        r => r.Id,
                        (u, r) => new { User = u, Role = r })
                    .Where(x => x.User.IsActive && !x.User.IsDeleted && x.User.PinHash != null &&
                        (x.Role.Name == "Supervisor" || x.Role.Name == "Manager" || x.Role.Name == "Owner"))
                    .Select(x => x.User)
                    .ToListAsync(cancellationToken);

                bool pinVerified = false;
                foreach (var user in usersWithPin)
                {
                    if (_passwordHasher.VerifyPassword(request.ManagerOverridePin, user.PinHash!))
                    {
                        pinVerified = true;
                        break;
                    }
                }

                if (!pinVerified)
                {
                    throw new UnauthorizedAccessException("Invalid Manager Override PIN. Access denied.");
                }
            }
        }
        else
        {
            throw new UnauthorizedAccessException("OpenedBy user must be specified.");
        }

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
