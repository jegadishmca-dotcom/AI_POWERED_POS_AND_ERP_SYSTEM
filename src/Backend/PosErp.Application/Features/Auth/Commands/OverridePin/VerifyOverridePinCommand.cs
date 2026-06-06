using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Auth.Commands.OverridePin;

/// <summary>
/// Verifies a Manager Override PIN against any active Owner/Manager/Supervisor
/// who has a PinHash set. Returns true if the pin matches any authorised user.
/// Called from the POS terminal's Manager Override modal.
/// </summary>
public record VerifyOverridePinCommand(string Pin) : IRequest<bool>;

public class VerifyOverridePinCommandHandler : IRequestHandler<VerifyOverridePinCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public VerifyOverridePinCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> Handle(VerifyOverridePinCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Pin)) return false;

        // BUG-04 FIX: "Admin" role does not exist in the seeded role table.
        // Seeded roles are: "Owner", "Manager", "Supervisor", "Cashier".
        // Override-authorized roles are Owner, Manager, and Supervisor.
        var usersWithPin = await _context.Users
            .Join(_context.Roles,
                u => u.RoleId,
                r => r.Id,
                (u, r) => new { User = u, Role = r })
            .Where(x => x.User.IsActive && !x.User.IsDeleted && x.User.PinHash != null &&
                (x.Role.Name == "Supervisor" || x.Role.Name == "Manager" || x.Role.Name == "Owner"))
            .Select(x => x.User)
            .ToListAsync(cancellationToken);

        foreach (var user in usersWithPin)
        {
            if (_passwordHasher.VerifyPassword(request.Pin, user.PinHash!))
                return true;
        }
        return false;
    }
}
