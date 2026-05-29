using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Auth;
using PosErp.Application.Features.Inventory.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Manager,Owner")] // Strict Admin/Owner/Manager Role Authorization
public class SettingsController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;

    public SettingsController(IApplicationDbContext context, IPasswordHasher passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    // ── User Management Endpoints ─────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users
            .Where(u => !u.IsDeleted)
            .Join(_context.Roles,
                u => u.RoleId,
                r => r.Id,
                (u, r) => new UserSettingsDto(
                    u.Id,
                    u.Username,
                    u.FullName,
                    r.Id,
                    r.Name,
                    u.IsActive,
                    u.StoreId,
                    u.CreatedAt))
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username and password are required." });
        }

        var usernameNormalized = request.Username.Trim().ToLower();
        var exists = await _context.Users.AnyAsync(u => u.Username.ToLower() == usernameNormalized && !u.IsDeleted);
        if (exists)
        {
            return BadRequest(new { message = "Username is already taken." });
        }

        var roleExists = await _context.Roles.AnyAsync(r => r.Id == request.RoleId);
        if (!roleExists)
        {
            return BadRequest(new { message = "Invalid Role ID selected." });
        }

        var newUser = new User
        {
            Username = request.Username.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            FullName = request.FullName.Trim(),
            RoleId = request.RoleId,
            StoreId = request.StoreId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync(default);

        return Ok(new { id = newUser.Id, message = "User created successfully." });
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        var roleExists = await _context.Roles.AnyAsync(r => r.Id == request.RoleId);
        if (!roleExists)
        {
            return BadRequest(new { message = "Invalid Role ID selected." });
        }

        user.FullName = request.FullName.Trim();
        user.RoleId = request.RoleId;
        user.IsActive = request.IsActive;
        user.StoreId = request.StoreId;

        await _context.SaveChangesAsync(default);
        return Ok(new { message = "User details updated successfully." });
    }

    [HttpPut("users/{id}/change-password")]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            return BadRequest(new { message = "Password must be at least 8 characters long." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.Password);
        await _context.SaveChangesAsync(default);

        return Ok(new { message = "Password updated successfully." });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _context.Roles
            .Where(r => !r.IsDeleted)
            .Select(r => new { r.Id, r.Name, r.Description })
            .ToListAsync();

        return Ok(roles);
    }

    // ── Terminal Configuration Endpoints ─────────────────────────────────────

    [HttpGet("terminals")]
    public async Task<IActionResult> GetTerminals()
    {
        var terminals = await _context.Terminals
            .OrderBy(t => t.TerminalCode)
            .ToListAsync();

        return Ok(terminals);
    }

    [HttpPost("terminals")]
    public async Task<IActionResult> CreateTerminal([FromBody] TerminalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TerminalCode) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Terminal code and name are required." });
        }

        var codeNormalized = request.TerminalCode.Trim().ToUpper();
        var exists = await _context.Terminals.AnyAsync(t => t.TerminalCode.ToUpper() == codeNormalized);
        if (exists)
        {
            return BadRequest(new { message = "Terminal Code already exists." });
        }

        var terminal = new Terminal
        {
            TerminalCode = request.TerminalCode.Trim().ToUpper(),
            Name = request.Name.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Terminals.Add(terminal);
        await _context.SaveChangesAsync(default);

        return Ok(terminal);
    }

    [HttpPut("terminals/{id}")]
    public async Task<IActionResult> UpdateTerminal(Guid id, [FromBody] TerminalRequest request)
    {
        var terminal = await _context.Terminals.FirstOrDefaultAsync(t => t.Id == id);
        if (terminal == null)
        {
            return NotFound(new { message = "Terminal not found." });
        }

        if (string.IsNullOrWhiteSpace(request.TerminalCode) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Terminal code and name are required." });
        }

        var codeNormalized = request.TerminalCode.Trim().ToUpper();
        var exists = await _context.Terminals.AnyAsync(t => t.TerminalCode.ToUpper() == codeNormalized && t.Id != id);
        if (exists)
        {
            return BadRequest(new { message = "Terminal Code already exists on another counter." });
        }

        terminal.TerminalCode = request.TerminalCode.Trim().ToUpper();
        terminal.Name = request.Name.Trim();
        terminal.IsActive = request.IsActive;

        await _context.SaveChangesAsync(default);
        return Ok(terminal);
    }

    [HttpDelete("terminals/{id}")]
    public async Task<IActionResult> DeleteTerminal(Guid id)
    {
        var terminal = await _context.Terminals.FirstOrDefaultAsync(t => t.Id == id);
        if (terminal == null)
        {
            return NotFound(new { message = "Terminal not found." });
        }

        _context.Terminals.Remove(terminal);
        await _context.SaveChangesAsync(default);

        return Ok(new { message = "Terminal deleted successfully." });
    }

    [HttpGet("inventory-rules")]
    public IActionResult GetInventoryRules()
    {
        var rules = InventoryRulesManager.GetRules();
        return Ok(rules);
    }

    [HttpPost("inventory-rules")]
    public IActionResult UpdateInventoryRules([FromBody] InventoryRules rules)
    {
        if (rules == null)
        {
            return BadRequest("Rules payload is empty.");
        }
        InventoryRulesManager.SaveRules(rules);
        return Ok(rules);
    }
}

// ── Settings DTOs ─────────────────────────────────────────────────────────────

public record UserSettingsDto(
    Guid Id,
    string Username,
    string FullName,
    Guid RoleId,
    string RoleName,
    bool IsActive,
    Guid? StoreId,
    DateTime CreatedAt);

public record CreateUserRequest(
    string Username,
    string Password,
    string FullName,
    Guid RoleId,
    Guid? StoreId);

public record UpdateUserRequest(
    string FullName,
    Guid RoleId,
    bool IsActive,
    Guid? StoreId);

public record ChangePasswordRequest(string Password);

public record TerminalRequest(
    string TerminalCode,
    string Name,
    bool IsActive);
