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
