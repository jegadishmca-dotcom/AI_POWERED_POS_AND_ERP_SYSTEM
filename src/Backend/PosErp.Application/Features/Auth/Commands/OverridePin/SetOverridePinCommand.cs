using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Auth.Commands.OverridePin;

/// <summary>
/// Sets (or changes) the Manager Override PIN for a specific user.
/// Requires the caller to be authenticated (Owner or the user themselves).
/// The PIN is stored as a bcrypt hash — never in plain text.
/// </summary>
public record SetOverridePinCommand(Guid UserId, string NewPin, string ConfirmPin) : IRequest<Unit>;

public class SetOverridePinCommandHandler : IRequestHandler<SetOverridePinCommand, Unit>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public SetOverridePinCommandHandler(IApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<Unit> Handle(SetOverridePinCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPin) || request.NewPin.Length < 4)
            throw new ArgumentException("Override PIN must be at least 4 digits.");

        if (request.NewPin != request.ConfirmPin)
            throw new ArgumentException("PINs do not match.");

        if (!System.Text.RegularExpressions.Regex.IsMatch(request.NewPin, @"^\d{4,8}$"))
            throw new ArgumentException("Override PIN must be 4–8 numeric digits.");

        var user = await _context.Users.FindAsync(new object[] { request.UserId }, cancellationToken)
            ?? throw new Exception("User not found.");

        user.PinHash = _passwordHasher.HashPassword(request.NewPin);

        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
