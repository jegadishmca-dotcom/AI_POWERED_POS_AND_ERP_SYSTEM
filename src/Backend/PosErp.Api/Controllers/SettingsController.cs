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

    [HttpGet("email")]
    public IActionResult GetEmailSettings()
    {
        var settings = PosErp.Application.Features.Inventory.Services.EmailSettingsManager.GetSettings();
        var displaySettings = new 
        {
            settings.SmtpServer,
            settings.SmtpPort,
            settings.SenderEmail,
            SenderPassword = string.IsNullOrEmpty(settings.SenderPassword) ? "" : "••••••••",
            settings.RecipientEmail,
            settings.EnableSsl
        };
        return Ok(displaySettings);
    }

    [HttpPost("email")]
    public IActionResult UpdateEmailSettings([FromBody] PosErp.Application.Features.Inventory.Services.EmailSettings settings)
    {
        if (settings == null)
        {
            return BadRequest("Settings payload is empty.");
        }

        if (settings.SenderPassword == "••••••••")
        {
            var existing = PosErp.Application.Features.Inventory.Services.EmailSettingsManager.GetSettings();
            settings.SenderPassword = existing.SenderPassword;
        }

        PosErp.Application.Features.Inventory.Services.EmailSettingsManager.SaveSettings(settings);
        return Ok(new { success = true });
    }

    [HttpPost("email/test")]
    public async Task<IActionResult> TestEmailSettings([FromBody] PosErp.Application.Features.Inventory.Services.EmailSettings settings)
    {
        if (settings == null)
        {
            return BadRequest("Settings payload is empty.");
        }

        if (settings.SenderPassword == "••••••••")
        {
            var existing = PosErp.Application.Features.Inventory.Services.EmailSettingsManager.GetSettings();
            settings.SenderPassword = existing.SenderPassword;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(settings.SenderEmail) || string.IsNullOrWhiteSpace(settings.SenderPassword))
            {
                return BadRequest(new { success = false, message = "Sender email and password are required for the test." });
            }

            var to = !string.IsNullOrWhiteSpace(settings.RecipientEmail) ? settings.RecipientEmail : "jegadishmca@gmail.com";
            var subject = "🍎 Apple Supermarket POS - SMTP Connection Test";
            var htmlBody = $@"
                <div style='font-family: sans-serif; padding: 20px; border: 1px solid #e5e7eb; border-radius: 8px;'>
                    <h2 style='color: #4f46e5;'>SMTP Setup Connection Test</h2>
                    <p>Congratulations! Your SMTP settings configuration is correct.</p>
                    <hr style='border: none; border-top: 1px solid #f3f4f6; margin: 20px 0;' />
                    <p style='font-size: 12px; color: #9ca3af;'>Sent at: {DateTime.UtcNow.AddHours(5.5):dd MMM yyyy HH:mm:ss} IST</p>
                </div>";

            using var mailMessage = new System.Net.Mail.MailMessage();
            mailMessage.From = new System.Net.Mail.MailAddress(settings.SenderEmail, "Apple Supermarket ERP");
            mailMessage.To.Add(to);
            mailMessage.Subject = subject;
            mailMessage.Body = htmlBody;
            mailMessage.IsBodyHtml = true;

            using var smtpClient = new System.Net.Mail.SmtpClient(settings.SmtpServer, settings.SmtpPort);
            smtpClient.EnableSsl = settings.EnableSsl;
            smtpClient.UseDefaultCredentials = false;
            smtpClient.Credentials = new System.Net.NetworkCredential(settings.SenderEmail, settings.SenderPassword);

            await smtpClient.SendMailAsync(mailMessage);

            return Ok(new { success = true, message = $"Test email sent successfully to {to}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = $"SMTP connection test failed: {ex.Message}" });
        }
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
