using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Audit.Services;
using PosErp.Domain.Entities.Auth;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Auth.Commands.Login;

public record LoginCommand(string Username, string Password, string TerminalCode) : IRequest<LoginResponse>;

public record LoginResponse(string AccessToken, string RefreshToken, UserDto User);
public record UserDto(Guid Id, string Username, string FullName, string Role, Guid? StoreId);

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MinimumLength(3);
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required.");
        RuleFor(x => x.TerminalCode).MaximumLength(50).WithMessage("Terminal Code must be under 50 characters.");
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenGenerator _jwtGenerator;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLoggingService _auditLoggingService;

    public LoginCommandHandler(IApplicationDbContext context, IJwtTokenGenerator jwtGenerator, IPasswordHasher passwordHasher, IAuditLoggingService auditLoggingService)
    {
        _context = context;
        _jwtGenerator = jwtGenerator;
        _passwordHasher = passwordHasher;
        _auditLoggingService = auditLoggingService;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username && !u.IsDeleted, cancellationToken);
        
        if (user == null || !user.IsActive || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            // GAP-01 FIX: Log failed login attempt to the audit trail.
            // This enables detection of brute-force attacks and unauthorized access investigation.
            await _auditLoggingService.LogActionAsync(
                userId: null,
                action: "LOGIN_FAILED",
                entityName: "User",
                entityId: request.Username,
                oldValues: null,
                newValues: new { Username = request.Username, TerminalCode = request.TerminalCode },
                ipAddress: "unknown",
                cancellationToken: cancellationToken);

            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        // GAP-02 FIX: Validate the terminal code exists and is active before issuing a token.
        // Previously, any terminal code (even fictional ones) was accepted silently.
        // We bypass this check for "BACK-OFFICE" which represents ERP Back-Office login rather than a POS counter.
        if (!string.IsNullOrEmpty(request.TerminalCode) && request.TerminalCode.Trim().ToUpper() != "BACK-OFFICE")
        {
            var terminal = await _context.Terminals
                .FirstOrDefaultAsync(t => t.TerminalCode == request.TerminalCode.Trim().ToUpper(), cancellationToken);

            if (terminal == null || !terminal.IsActive)
            {
                throw new UnauthorizedAccessException($"Terminal code '{request.TerminalCode}' is not recognized or is inactive. Please contact your administrator.");
            }
        }

        // Retrieve actual Role name
        var roleName = await _context.Roles
            .Where(r => r.Id == user.RoleId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Staff";
        
        var accessToken = _jwtGenerator.GenerateToken(user, roleName);
        var refreshTokenStr = _jwtGenerator.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenStr,
            TokenFamily = Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceId = string.IsNullOrEmpty(request.TerminalCode) ? "BACK-OFFICE" : request.TerminalCode,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        
        // GAP-01 FIX: Log successful login to the audit trail.
        await _auditLoggingService.LogActionAsync(
            userId: user.Id,
            action: "LOGIN_SUCCESS",
            entityName: "User",
            entityId: user.Id.ToString(),
            oldValues: null,
            newValues: new { Username = user.Username, TerminalCode = request.TerminalCode, Role = roleName },
            ipAddress: "unknown",
            cancellationToken: cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new LoginResponse(
            accessToken, 
            refreshTokenStr, 
            new UserDto(user.Id, user.Username, user.FullName, roleName, user.StoreId));
    }
}
