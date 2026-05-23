using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Auth.Commands.OverridePin;

/// <summary>
/// Verifies a Manager Override PIN against any active Owner/Manager/Cashier
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

        // Fetch all active users who have an override PIN set (Owner / Manager roles)
        var usersWithPin = await _context.Users
            .Where(u => u.IsActive && !u.IsDeleted && u.PinHash != null)
            .ToListAsync(cancellationToken);

        foreach (var user in usersWithPin)
        {
            if (_passwordHasher.VerifyPassword(request.Pin, user.PinHash!))
                return true;
        }
        return false;
    }
}
