$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"

# Directories
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Domain\Entities\Auth"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Auth\Commands\Login"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Auth\Commands\Refresh"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Interfaces"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Infrastructure\Authentication"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Api\Controllers"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Api\Extensions"

# 1. Domain Entities
@"
using System;

namespace PosErp.Domain.Entities.Auth;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PinHash { get; set; }
    public Guid RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public bool IsDeleted { get; set; }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\Entities\Auth\User.cs" -Encoding utf8

@"
using System;

namespace PosErp.Domain.Entities.Auth;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string TokenFamily { get; set; } = string.Empty; // For invalidation
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\Entities\Auth\RefreshToken.cs" -Encoding utf8

# 2. Application Interfaces
@"
using System.Threading.Tasks;
using PosErp.Domain.Entities.Auth;

namespace PosErp.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user, string roleName);
    string GenerateRefreshToken();
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Interfaces\IJwtTokenGenerator.cs" -Encoding utf8

# 3. Application Commands
@"
using FluentValidation;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Auth.Commands.Login;

public record LoginCommand(string Username, string Password, string DeviceId) : IRequest<LoginResponse>;

public record LoginResponse(string AccessToken, string RefreshToken, UserDto User);

public record UserDto(Guid Id, string Username, string FullName, string Role);

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.DeviceId).NotEmpty();
    }
}

// Handler stub (assuming EF Core injected later)
public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IJwtTokenGenerator _jwtGenerator;
    // DbContext omitted for pure scaffolding purposes

    public LoginCommandHandler(IJwtTokenGenerator jwtGenerator)
    {
        _jwtGenerator = jwtGenerator;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // TODO: Validate user against DB, verify password hash.
        // For now, return stub data
        var user = new Domain.Entities.Auth.User { Username = request.Username, FullName = "Admin User" };
        var accessToken = _jwtGenerator.GenerateToken(user, "Admin");
        var refreshToken = _jwtGenerator.GenerateRefreshToken();
        
        return new LoginResponse(accessToken, refreshToken, new UserDto(user.Id, user.Username, user.FullName, "Admin"));
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Auth\Commands\Login\LoginCommand.cs" -Encoding utf8

@"
using FluentValidation;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Auth.Commands.Refresh;

public record RefreshTokenCommand(string RefreshToken, string DeviceId) : IRequest<RefreshResponse>;

public record RefreshResponse(string AccessToken, string RefreshToken);

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshResponse>
{
    private readonly IJwtTokenGenerator _jwtGenerator;

    public RefreshTokenCommandHandler(IJwtTokenGenerator jwtGenerator)
    {
        _jwtGenerator = jwtGenerator;
    }

    public async Task<RefreshResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // TODO: Validate against DB, handle token family invalidation if IsRevoked == true
        return new RefreshResponse("new_access_token", "new_refresh_token");
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Auth\Commands\Refresh\RefreshTokenCommand.cs" -Encoding utf8

# 4. Infrastructure Authentication
@"
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Auth;

namespace PosErp.Infrastructure.Authentication;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    // Placeholder Secret
    private const string Secret = "SuperSecretKeyForDevelopmentPurposesOnlyReplaceInProd";
    private const string Issuer = "PosErp";
    private const string Audience = "PosErpClient";

    public string GenerateToken(User user, string roleName)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, roleName),
            new Claim("store_id", user.StoreId?.ToString() ?? string.Empty)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Authentication\JwtTokenGenerator.cs" -Encoding utf8

# 5. Api Controllers & Extensions
@"
using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using PosErp.Application.Features.Auth.Commands.Login;
using PosErp.Application.Features.Auth.Commands.Refresh;
using Microsoft.AspNetCore.Http;
using System;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);
        
        SetRefreshTokenCookie(result.RefreshToken);
        
        return Ok(new { result.AccessToken, result.User });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken)) return Unauthorized();

        // In a real scenario, extract device ID properly
        var command = new RefreshTokenCommand(refreshToken, "device-123");
        var result = await _mediator.Send(command);
        
        SetRefreshTokenCookie(result.RefreshToken);
        
        return Ok(new { result.AccessToken });
    }

    private void SetRefreshTokenCookie(string token)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Must be true in production
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        };
        Response.Cookies.Append("refreshToken", token, cookieOptions);
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Api\Controllers\AuthController.cs" -Encoding utf8

Write-Host "Backend Auth Scaffolded"
