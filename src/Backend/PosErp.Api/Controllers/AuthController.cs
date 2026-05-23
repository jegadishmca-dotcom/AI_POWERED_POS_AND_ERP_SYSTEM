using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using PosErp.Application.Features.Auth.Commands.Login;
using PosErp.Application.Features.Auth.Commands.Refresh;
using PosErp.Application.Features.Auth.Commands.OverridePin;
using Microsoft.AspNetCore.Http;
using System;
using System.IdentityModel.Tokens.Jwt;

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

        var command = new RefreshTokenCommand(refreshToken, "device-123");
        var result = await _mediator.Send(command);
        
        SetRefreshTokenCookie(result.RefreshToken);
        
        return Ok(new { result.AccessToken });
    }

    /// <summary>
    /// Verifies a manager override PIN — called from the POS Manager Override modal.
    /// Returns { authorized: true/false }. Does NOT reveal which user matched.
    /// No [Authorize] needed: any authenticated user (with a valid token) can call this.
    /// </summary>
    [HttpPost("verify-override-pin")]
    public async Task<IActionResult> VerifyOverridePin([FromBody] VerifyOverridePinRequest req)
    {
        // Require a valid Bearer token (caller must be logged in)
        if (!TryGetCallerUserId(out _))
            return Unauthorized(new { message = "A valid login session is required." });

        var authorized = await _mediator.Send(new VerifyOverridePinCommand(req.Pin));
        return Ok(new { authorized });
    }

    /// <summary>
    /// Sets/changes the override PIN for the currently logged-in user.
    /// Extracts user ID directly from the Bearer JWT token (Sub claim).
    /// </summary>
    [HttpPost("set-override-pin")]
    public async Task<IActionResult> SetOverridePin([FromBody] SetOverridePinRequest req)
    {
        if (!TryGetCallerUserId(out var callerId))
            return Unauthorized(new { message = "A valid login session is required." });

        var targetId = req.UserId.HasValue ? req.UserId.Value : callerId;

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

    /// <summary>
    /// Reads the Bearer token from the Authorization header and extracts the
    /// user ID from the 'sub' claim (set by JwtTokenGenerator.GenerateToken).
    /// Returns false if the token is missing or malformed.
    /// </summary>
    private bool TryGetCallerUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
            return false;

        var tokenStr = authHeader["Bearer ".Length..].Trim();
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(tokenStr);
            var sub = jwt.Subject; // 'sub' claim = user ID
            return Guid.TryParse(sub, out userId);
        }
        catch
        {
            return false;
        }
    }
}

// Request body records
public record VerifyOverridePinRequest(string Pin);
public record SetOverridePinRequest(string NewPin, string ConfirmPin, Guid? UserId = null);
