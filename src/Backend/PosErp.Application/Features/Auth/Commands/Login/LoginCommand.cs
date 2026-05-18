using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
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
        RuleFor(x => x.Password).NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
        RuleFor(x => x.TerminalCode).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenGenerator _jwtGenerator;
    private readonly IPasswordHasher _passwordHasher;

    public LoginCommandHandler(IApplicationDbContext context, IJwtTokenGenerator jwtGenerator, IPasswordHasher passwordHasher)
    {
        _context = context;
        _jwtGenerator = jwtGenerator;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username && !u.IsDeleted, cancellationToken);
        
        if (user == null || !user.IsActive || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            // TODO: Log failed attempt to AuditLog
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        // Retrieve actual Role name
        var roleName = await _context.Roles
            .Where(r => r.Id == user.RoleId)
            .Select(r => r.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "Staff";

        // TODO: Validate TerminalCode exists and is active
        
        var accessToken = _jwtGenerator.GenerateToken(user, roleName);
        var refreshTokenStr = _jwtGenerator.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenStr,
            TokenFamily = Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeviceId = request.TerminalCode,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        
        // TODO: Log successful login to AuditLog

        await _context.SaveChangesAsync(cancellationToken);

        return new LoginResponse(
            accessToken, 
            refreshTokenStr, 
            new UserDto(user.Id, user.Username, user.FullName, roleName, user.StoreId));
    }
}
