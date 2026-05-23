using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using PosErp.Application.Features.Auth.Commands.Login;
using PosErp.Application.Features.Auth.Commands.Refresh;
using PosErp.Application.Features.Auth.Commands.OverridePin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;

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

    /// <summary>
    /// Verifies a manager override PIN — called from the POS Manager Override modal.
    /// Returns { authorized: true/false }. Does NOT reveal which user matched.
    /// </summary>
    [HttpPost("verify-override-pin")]
    [Authorize]
    public async Task<IActionResult> VerifyOverridePin([FromBody] VerifyOverridePinRequest req)
    {
        var authorized = await _mediator.Send(new VerifyOverridePinCommand(req.Pin));
        return Ok(new { authorized });
    }

    /// <summary>
    /// Sets/changes the override PIN for the currently logged-in user (or a specified userId for Owner).
    /// </summary>
    [HttpPost("set-override-pin")]
    [Authorize]
    public async Task<IActionResult> SetOverridePin([FromBody] SetOverridePinRequest req)
    {
        var callerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Guid.Empty.ToString());
        var targetId = req.UserId.HasValue ? req.UserId.Value : callerId;

        await _mediator.Send(new SetOverridePinCommand(targetId, req.NewPin, req.ConfirmPin));
        return Ok(new { message = "Override PIN updated successfully." });
    }
}

// Request body records
public record VerifyOverridePinRequest(string Pin);
public record SetOverridePinRequest(string NewPin, string ConfirmPin, Guid? UserId = null);
