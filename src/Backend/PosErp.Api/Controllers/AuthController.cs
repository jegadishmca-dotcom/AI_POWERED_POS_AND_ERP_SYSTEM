using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using PosErp.Application.Features.Auth.Commands.Login;
using PosErp.Application.Features.Auth.Commands.Refresh;
using PosErp.Application.Features.Auth.Commands.OverridePin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Security.Claims;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _context;

    public AuthController(IMediator mediator, IApplicationDbContext context)
    {
        _mediator = mediator;
        _context = context;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);
        
        SetRefreshTokenCookie(result.RefreshToken);
        
        return Ok(new { result.AccessToken, result.User, result.TerminalId });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken)) return Unauthorized();

        // SEC-03 FIX: Use a real device identifier so stolen token detection is meaningful.
        // The frontend should send X-Device-Id with a stable device fingerprint (e.g., terminal code).
        // Fall back to a hash of User-Agent + IP if the header is not present.
        var deviceId = Request.Headers["X-Device-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            var ua = Request.Headers["User-Agent"].FirstOrDefault() ?? "unknown";
            var ip = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            deviceId = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes($"{ua}|{ip}")
                )
            ).Substring(0, 16);
        }

        var command = new RefreshTokenCommand(refreshToken, deviceId);
        var result = await _mediator.Send(command);
        
        SetRefreshTokenCookie(result.RefreshToken);
        
        return Ok(new { result.AccessToken });
    }

    /// <summary>
    /// H5 FIX: Logout — revokes the current refresh token so stolen tokens cannot be reused.
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.Token == refreshToken);
            if (token != null)
            {
                token.IsRevoked = true;
                await _context.SaveChangesAsync(default);
            }
        }

        // Clear the cookie regardless
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict
        });

        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Verifies a manager override PIN. Caller must be authenticated.
    /// </summary>
    [HttpPost("verify-override-pin")]
    [Authorize]
    public async Task<IActionResult> VerifyOverridePin([FromBody] VerifyOverridePinRequest req)
    {
        var authorized = await _mediator.Send(new VerifyOverridePinCommand(req.Pin));
        return Ok(new { authorized });
    }

    /// <summary>
    /// S5 FIX: Sets/changes override PIN.
    /// - A user can always change their own PIN.
    /// - Only Manager/Owner can change another user's PIN.
    /// H3 FIX: Uses HttpContext.User (signature-validated Claims principal) instead of
    ///          manually decoding the JWT without signature verification.
    /// </summary>
    [HttpPost("set-override-pin")]
    [Authorize]
    public async Task<IActionResult> SetOverridePin([FromBody] SetOverridePinRequest req)
    {
        // H3 FIX: Use validated Claims principal — NOT ReadJwtToken() which skips validation
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(callerIdStr, out var callerId))
            return Unauthorized(new { message = "A valid login session is required." });

        var targetId = req.UserId.HasValue ? req.UserId.Value : callerId;

        // S5 FIX: Only Manager/Owner can set another user's PIN
        if (targetId != callerId)
        {
            var callerRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (!callerRole.Equals("Manager", StringComparison.OrdinalIgnoreCase) &&
                !callerRole.Equals("Owner", StringComparison.OrdinalIgnoreCase) &&
                !callerRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid(); // 403: only Manager/Owner can change other users' PINs
            }
        }

        await _mediator.Send(new SetOverridePinCommand(targetId, req.NewPin, req.ConfirmPin));
        return Ok(new { message = "Override PIN updated successfully." });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void SetRefreshTokenCookie(string token)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        };
        Response.Cookies.Append("refreshToken", token, cookieOptions);
    }
}

// Request body records
public record VerifyOverridePinRequest(string Pin);
public record SetOverridePinRequest(string NewPin, string ConfirmPin, Guid? UserId = null);
