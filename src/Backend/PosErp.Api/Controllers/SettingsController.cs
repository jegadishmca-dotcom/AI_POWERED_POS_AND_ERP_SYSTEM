using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
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
    private readonly PosErp.Application.Features.Audit.Services.IAuditLoggingService _auditLoggingService;
    private readonly PosErp.Application.Features.Inventory.Services.IEmailSettingsManager _emailSettingsManager;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public SettingsController(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        PosErp.Application.Features.Audit.Services.IAuditLoggingService auditLoggingService,
        PosErp.Application.Features.Inventory.Services.IEmailSettingsManager emailSettingsManager,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _auditLoggingService = auditLoggingService;
        _emailSettingsManager = emailSettingsManager;
        _emailService = emailService;
        _configuration = configuration;
    }

    // ── User Management Endpoints ─────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var currentUsername = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var isDemoUser = string.Equals(currentUsername, "demo@supermarket.com", StringComparison.OrdinalIgnoreCase);

        var query = _context.Users.Where(u => !u.IsDeleted);

        if (isDemoUser)
        {
            query = query.Where(u => !u.Username.ToLower().EndsWith("@supermarket.local"));
        }

        var users = await query
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

        var currentUsername = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var isDemoUser = string.Equals(currentUsername, "demo@supermarket.com", StringComparison.OrdinalIgnoreCase);

        if (isDemoUser && user.Username.ToLower().EndsWith("@supermarket.local"))
        {
            return StatusCode(403, new { message = "Demo Sandbox User cannot modify system accounts." });
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new { message = "Username is required." });
        }

        var usernameNormalized = request.Username.Trim().ToLower();
        if (isDemoUser && usernameNormalized.EndsWith("@supermarket.local") && !user.Username.ToLower().EndsWith("@supermarket.local"))
        {
            return StatusCode(403, new { message = "Demo Sandbox User cannot assign system usernames ending with @supermarket.local." });
        }

        var exists = await _context.Users.AnyAsync(u => u.Username.ToLower() == usernameNormalized && u.Id != id && !u.IsDeleted);
        if (exists)
        {
            return BadRequest(new { message = "Username is already taken by another account." });
        }

        var roleExists = await _context.Roles.AnyAsync(r => r.Id == request.RoleId);
        if (!roleExists)
        {
            return BadRequest(new { message = "Invalid Role ID selected." });
        }

        user.Username = request.Username.Trim();
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
            return BadRequest(new { message = "New password must be at least 8 characters long." });
        }

        // SEC-06 FIX: Require the requester's current password to authorize a password change.
        // Without this check, any Manager/Owner can change any user's password without knowing it,
        // enabling privilege abuse (e.g., a terminated manager locking out accounts).
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            return BadRequest(new { message = "Current password is required to authorize this change." });
        }

        // Find the calling user (the one making the request) to verify their own password
        var currentUsername = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var callerUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == currentUsername && !u.IsDeleted);
        if (callerUser == null || !_passwordHasher.VerifyPassword(request.CurrentPassword, callerUser.PasswordHash))
        {
            return StatusCode(403, new { message = "Your current password is incorrect. Password change denied." });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        var isDemoUser = string.Equals(currentUsername, "demo@supermarket.com", StringComparison.OrdinalIgnoreCase);
        if (isDemoUser && user.Username.ToLower().EndsWith("@supermarket.local"))
        {
            return StatusCode(403, new { message = "Demo Sandbox User cannot modify system accounts." });
        }

        user.PasswordHash = _passwordHasher.HashPassword(request.Password);

        // Audit the password change
        var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        Guid? userId = Guid.TryParse(userIdClaim, out var guid) ? guid : null;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _auditLoggingService.LogActionAsync(
            userId,
            "CHANGE_PASSWORD",
            "User",
            id.ToString(),
            null,
            new { TargetUsername = user.Username },
            ipAddress,
            default);

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
    public async Task<IActionResult> UpdateInventoryRules([FromBody] InventoryRules rules)
    {
        if (rules == null)
        {
            return BadRequest("Rules payload is empty.");
        }

        var oldRules = InventoryRulesManager.GetRules();
        InventoryRulesManager.SaveRules(rules);

        // Get user details for auditing
        var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        Guid? userId = Guid.TryParse(userIdClaim, out var guid) ? guid : null;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        await _auditLoggingService.LogActionAsync(
            userId,
            "UPDATE_INVENTORY_RULES",
            "InventoryRules",
            "system",
            oldRules,
            rules,
            ipAddress,
            default);

        return Ok(rules);
    }

    [HttpGet("email")]
    public IActionResult GetEmailSettings()
    {
        var settings = _emailSettingsManager.GetSettings();
        
        var currentUsername = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var isSuperAdmin = string.Equals(currentUsername, "admin@supermarket.local", StringComparison.OrdinalIgnoreCase);

        var displaySettings = new 
        {
            settings.SmtpServer,
            settings.SmtpPort,
            settings.SenderEmail,
            SenderPassword = isSuperAdmin 
                ? settings.SenderPassword 
                : (string.IsNullOrEmpty(settings.SenderPassword) ? "" : "••••••••"),
            settings.RecipientEmail,
            settings.EnableSsl,
            settings.TriggerIntervalMinutes,
            settings.DeliveryMethod,
            settings.MailgunDomain,
            MailgunApiKey = isSuperAdmin 
                ? settings.MailgunApiKey 
                : (string.IsNullOrEmpty(settings.MailgunApiKey) ? "" : "••••••••"),
            PostmarkToken = isSuperAdmin 
                ? settings.PostmarkToken 
                : (string.IsNullOrEmpty(settings.PostmarkToken) ? "" : "••••••••"),
            ResendApiKey = isSuperAdmin 
                ? settings.ResendApiKey 
                : (string.IsNullOrEmpty(settings.ResendApiKey) ? "" : "••••••••")
        };
        return Ok(displaySettings);
    }

    [HttpPost("email")]
    public async Task<IActionResult> UpdateEmailSettings([FromBody] PosErp.Application.Features.Inventory.Services.EmailSettings settings)
    {
        if (settings == null)
        {
            return BadRequest("Settings payload is empty.");
        }

        var oldSettings = _emailSettingsManager.GetSettings();

        var currentUsername = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var isSuperAdmin = string.Equals(currentUsername, "admin@supermarket.local", StringComparison.OrdinalIgnoreCase);

        if (!isSuperAdmin)
        {
            // Non-superadmins cannot modify the password or sender email or keys
            settings.SenderPassword = oldSettings.SenderPassword;
            settings.SenderEmail = oldSettings.SenderEmail;
            settings.MailgunApiKey = oldSettings.MailgunApiKey;
            settings.MailgunDomain = oldSettings.MailgunDomain;
            settings.PostmarkToken = oldSettings.PostmarkToken;
            settings.ResendApiKey = oldSettings.ResendApiKey;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(settings.SenderPassword) || settings.SenderPassword == "••••••••")
            {
                settings.SenderPassword = oldSettings.SenderPassword;
            }
            if (string.IsNullOrWhiteSpace(settings.MailgunApiKey) || settings.MailgunApiKey == "••••••••")
            {
                settings.MailgunApiKey = oldSettings.MailgunApiKey;
            }
            if (string.IsNullOrWhiteSpace(settings.PostmarkToken) || settings.PostmarkToken == "••••••••")
            {
                settings.PostmarkToken = oldSettings.PostmarkToken;
            }
            if (string.IsNullOrWhiteSpace(settings.ResendApiKey) || settings.ResendApiKey == "••••••••")
            {
                settings.ResendApiKey = oldSettings.ResendApiKey;
            }
        }

        _emailSettingsManager.SaveSettings(settings);

        // Get user details for auditing
        var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        Guid? userId = Guid.TryParse(userIdClaim, out var guid) ? guid : null;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var oldSettingsMasked = new { oldSettings.SmtpServer, oldSettings.SmtpPort, oldSettings.SenderEmail, oldSettings.RecipientEmail, oldSettings.EnableSsl, oldSettings.TriggerIntervalMinutes, oldSettings.DeliveryMethod, oldSettings.MailgunDomain };
        var newSettingsMasked = new { settings.SmtpServer, settings.SmtpPort, settings.SenderEmail, settings.RecipientEmail, settings.EnableSsl, settings.TriggerIntervalMinutes, settings.DeliveryMethod, settings.MailgunDomain };

        await _auditLoggingService.LogActionAsync(
            userId,
            "UPDATE_EMAIL_SETTINGS",
            "EmailSettings",
            "system",
            oldSettingsMasked,
            newSettingsMasked,
            ipAddress,
            default);

        return Ok(new { success = true });
    }

    [HttpPost("email/test")]
    public async Task<IActionResult> TestEmailSettings([FromBody] PosErp.Application.Features.Inventory.Services.EmailSettings settings)
    {
        if (settings == null)
        {
            return BadRequest("Settings payload is empty.");
        }

        var oldSettings = _emailSettingsManager.GetSettings();

        var currentUsername = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var isSuperAdmin = string.Equals(currentUsername, "admin@supermarket.local", StringComparison.OrdinalIgnoreCase);

        if (!isSuperAdmin)
        {
            // If the requester is not admin@supermarket.local, enforce original credentials
            settings.SenderPassword = oldSettings.SenderPassword;
            settings.SenderEmail = oldSettings.SenderEmail;
            settings.MailgunApiKey = oldSettings.MailgunApiKey;
            settings.MailgunDomain = oldSettings.MailgunDomain;
            settings.PostmarkToken = oldSettings.PostmarkToken;
            settings.ResendApiKey = oldSettings.ResendApiKey;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(settings.SenderPassword) || settings.SenderPassword == "••••••••")
            {
                settings.SenderPassword = oldSettings.SenderPassword;
            }
            if (string.IsNullOrWhiteSpace(settings.MailgunApiKey) || settings.MailgunApiKey == "••••••••")
            {
                settings.MailgunApiKey = oldSettings.MailgunApiKey;
            }
            if (string.IsNullOrWhiteSpace(settings.PostmarkToken) || settings.PostmarkToken == "••••••••")
            {
                settings.PostmarkToken = oldSettings.PostmarkToken;
            }
            if (string.IsNullOrWhiteSpace(settings.ResendApiKey) || settings.ResendApiKey == "••••••••")
            {
                settings.ResendApiKey = oldSettings.ResendApiKey;
            }
        }

        // Make sure we fallback to saved email if it's sent empty
        if (string.IsNullOrWhiteSpace(settings.SenderEmail))
        {
            settings.SenderEmail = oldSettings.SenderEmail;
        }

        // Save current test settings to database so the SendEmailAsync uses it
        _emailSettingsManager.SaveSettings(settings);

        try
        {
            var to = !string.IsNullOrWhiteSpace(settings.RecipientEmail) ? settings.RecipientEmail : "jegadishmca@gmail.com";
            var subject = "🍎 Apple Supermarket POS - Connection Test";
            var htmlBody = $@"
                <div style='font-family: sans-serif; padding: 20px; border: 1px solid #e5e7eb; border-radius: 8px;'>
                    <h2 style='color: #4f46e5;'>Email Setup Connection Test</h2>
                    <p>Congratulations! Your email settings configuration is correct.</p>
                    <hr style='border: none; border-top: 1px solid #f3f4f6; margin: 20px 0;' />
                    <p style='font-size: 12px; color: #9ca3af;'>Sent at: {DateTime.UtcNow.AddHours(5.5):dd MMM yyyy HH:mm:ss} IST</p>
                </div>";

            await _emailService.SendEmailAsync(to, subject, htmlBody);

            return Ok(new { success = true, message = $"Test email sent successfully to {to}" });
        }
        catch (Exception ex)
        {
            var detail = ex.InnerException != null ? $"{ex.Message} (Inner: {ex.InnerException.Message})" : ex.Message;
            return StatusCode(500, new { success = false, message = $"Email connection test failed: {detail}" });
        }
    }

    // ── GST Compliance Feature Toggles ───────────────────────────────────────
    // Reads from and writes to database_metadata so the settings survive Docker
    // image rebuilds (appsettings.json is baked into the image and reverts on rebuild).

    [HttpGet("features/compliance")]
    [Authorize(Roles = "Owner,Developer")] // Owner/Developer only — gates statutory GST e-invoicing integration
    public async Task<IActionResult> GetComplianceFeatures()
    {
        var connStr = _configuration.GetConnectionString("DefaultConnection");
        try
        {
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT einvoice_enabled, ewaybill_enabled FROM database_metadata LIMIT 1";
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Ok(new ComplianceFeaturesDto(
                    EInvoiceEnabled: reader.GetBoolean(0),
                    EWayBillEnabled: reader.GetBoolean(1)
                ));
            }
            return Ok(new ComplianceFeaturesDto(false, false)); // no metadata row yet
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Failed to read compliance flags: {ex.Message}" });
        }
    }

    [HttpPost("features/compliance")]
    [Authorize(Roles = "Owner,Developer")] // Owner/Developer only — gates statutory GST e-invoicing integration
    public async Task<IActionResult> UpdateComplianceFeatures([FromBody] ComplianceFeaturesDto request)
    {
        var connStr = _configuration.GetConnectionString("DefaultConnection");
        try
        {
            await using var conn = new NpgsqlConnection(connStr);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            // Updates only the compliance columns; all other database_metadata columns are untouched.
            cmd.CommandText = @"
                UPDATE database_metadata
                SET einvoice_enabled = @einvoice,
                    ewaybill_enabled  = @ewaybill,
                    updated_at        = NOW()";
            cmd.Parameters.AddWithValue("einvoice", request.EInvoiceEnabled);
            cmd.Parameters.AddWithValue("ewaybill",  request.EWayBillEnabled);
            var affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 0)
                return StatusCode(500, new { message = "database_metadata row not found. Ensure the database is initialised." });

            return Ok(new
            {
                success = true,
                // Settings take effect immediately (read from DB on each request) — no restart needed.
                message = "Compliance feature settings saved to database. Changes take effect immediately (no restart required).",
                eInvoiceEnabled = request.EInvoiceEnabled,
                eWayBillEnabled = request.EWayBillEnabled
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Failed to update compliance flags: {ex.Message}" });
        }
    }

    [HttpPost("email/alert")]
    [AllowAnonymous] // Authenticated via custom X-System-Alert-Key header
    public async Task<IActionResult> DispatchSystemAlert(
        [FromBody] SystemAlertRequest request,
        [FromHeader(Name = "X-System-Alert-Key")] string alertKey)
    {
        var expectedKey = _configuration["EmailSettings:SystemAlertApiKey"];
        if (string.IsNullOrEmpty(expectedKey) || alertKey != expectedKey)
        {
            return Unauthorized("Invalid alert API key.");
        }

        var settings = _emailSettingsManager.GetSettings();
        var to = !string.IsNullOrWhiteSpace(settings.DeveloperAlertEmail) 
            ? settings.DeveloperAlertEmail 
            : (!string.IsNullOrWhiteSpace(settings.RecipientEmail) ? settings.RecipientEmail : "jegadishmca@gmail.com");

        // HTML-encode inputs to prevent layout breakage or tag injection
        var safeSource = System.Net.WebUtility.HtmlEncode(request.AlertSource);
        var safeMessage = System.Net.WebUtility.HtmlEncode(request.Message);

        var subject = $"🚨 Apple Supermarket POS - {safeSource} FAILURE";
        var htmlBody = $@"
            <div style='font-family: sans-serif; padding: 20px; border: 1px solid #ef4444; border-radius: 8px;'>
                <h2 style='color: #dc2626;'>System Backup Alert</h2>
                <p><strong>Source:</strong> {safeSource}</p>
                <p><strong>Message:</strong> {safeMessage}</p>
                <p><strong>Time:</strong> {DateTime.UtcNow.AddHours(5.5):dd MMM yyyy HH:mm:ss} IST</p>
            </div>";

        await _emailService.SendEmailAsync(to, subject, htmlBody);
        return Ok(new { success = true });
    }

    // ── POS Permissions Feature Flags ─────────────────────────────────────────

    /// <summary>
    /// Feature 2: Returns POS-level permission flags (e.g. cashier delete toggle).
    /// GET /api/settings/features/pos-permissions
    ///
    /// AUTHORIZATION: Open to any authenticated role (including Cashier) because
    /// PosTerminal.tsx calls this on startup to load the cashierCanDeleteLineItem flag.
    /// The class-level [Authorize(Roles = "Manager,Owner")] is intentionally overridden
    /// here. Only the POST (write) endpoint remains Manager/Owner-only.
    /// </summary>
    [HttpGet("features/pos-permissions")]
    [Authorize] // Any authenticated user — Cashier must be able to read this flag at POS startup
    public IActionResult GetPosPermissions()
    {
        var permissions = PosPermissionsManager.GetPermissions();
        return Ok(new
        {
            cashierCanDeleteLineItem = permissions.CashierCanDeleteLineItem,
            receiptProductLanguage = permissions.ReceiptProductLanguage ?? "secondary",
            enableCatalogAutoTranslation = permissions.EnableCatalogAutoTranslation,
            catalogTargetLanguage = permissions.CatalogTargetLanguage ?? "ta"
        });
    }

    /// <summary>
    /// Feature 2: Updates POS-level permission & multi-language settings.
    /// POST /api/settings/features/pos-permissions
    /// Changes are audit-logged for traceability.
    /// </summary>
    [HttpPost("features/pos-permissions")]
    public async Task<IActionResult> UpdatePosPermissions([FromBody] PosPermissionsRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Request payload is required." });

        var oldPermissions = PosPermissionsManager.GetPermissions();
        var newPermissions = new PosPermissions
        {
            CashierCanDeleteLineItem = request.CashierCanDeleteLineItem,
            ReceiptProductLanguage = request.ReceiptProductLanguage ?? oldPermissions.ReceiptProductLanguage ?? "secondary",
            EnableCatalogAutoTranslation = request.EnableCatalogAutoTranslation ?? oldPermissions.EnableCatalogAutoTranslation,
            CatalogTargetLanguage = request.CatalogTargetLanguage ?? oldPermissions.CatalogTargetLanguage ?? "ta"
        };
        PosPermissionsManager.SavePermissions(newPermissions);

        var userIdClaim = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        Guid? userId = Guid.TryParse(userIdClaim, out var guid) ? guid : null;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        await _auditLoggingService.LogActionAsync(
            userId,
            "UPDATE_POS_PERMISSIONS",
            "PosPermissions",
            "cashierCanDeleteLineItem",
            oldPermissions,
            newPermissions,
            ipAddress,
            default);

        return Ok(new
        {
            success = true,
            message = "POS permission & multi-language settings saved. Changes take effect immediately.",
            cashierCanDeleteLineItem = newPermissions.CashierCanDeleteLineItem,
            receiptProductLanguage = newPermissions.ReceiptProductLanguage,
            enableCatalogAutoTranslation = newPermissions.EnableCatalogAutoTranslation,
            catalogTargetLanguage = newPermissions.CatalogTargetLanguage
        });
    }
}

public record SystemAlertRequest(string AlertSource, string Message);

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
    string Username,
    string FullName,
    Guid RoleId,
    bool IsActive,
    Guid? StoreId);

public record ChangePasswordRequest(string Password, string CurrentPassword);

public record TerminalRequest(
    string TerminalCode,
    string Name,
    bool IsActive);

public record ComplianceFeaturesDto(
    bool EInvoiceEnabled,
    bool EWayBillEnabled);

public record PosPermissionsRequest(
    bool CashierCanDeleteLineItem,
    string? ReceiptProductLanguage = null,
    bool? EnableCatalogAutoTranslation = null,
    string? CatalogTargetLanguage = null);
