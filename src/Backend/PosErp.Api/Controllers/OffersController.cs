using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Offers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/offers")]
[Authorize(Roles = "Owner,Manager")]
public class OffersController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly PosErp.Application.Features.Audit.Services.IAuditLoggingService _auditLogger;
    private readonly IOfferExportService _exportService;
    private readonly IOfferImportService _importService;

    public OffersController(
        IApplicationDbContext context, 
        PosErp.Application.Features.Audit.Services.IAuditLoggingService auditLogger,
        IOfferExportService exportService,
        IOfferImportService importService)
    {
        _context = context;
        _auditLogger = auditLogger;
        _exportService = exportService;
        _importService = importService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllOffers(CancellationToken cancellationToken)
    {
        var offers = await _context.Offers.ToListAsync(cancellationToken);
        return Ok(offers);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOffer(Guid id, CancellationToken cancellationToken)
    {
        var offer = await _context.Offers.FindAsync(new object[] { id }, cancellationToken);
        if (offer == null) return NotFound();
        return Ok(offer);
    }

    [HttpPost]
    public async Task<IActionResult> CreateOffer([FromBody] Offer offer, CancellationToken cancellationToken)
    {
        // Normalize DateTimes to UTC to satisfy PostgreSQL 'timestamp with time zone' requirement.
        // The frontend sends datetime-local values which have DateTimeKind=Unspecified.
        offer.StartDate = DateTime.SpecifyKind(offer.StartDate, DateTimeKind.Utc);
        offer.EndDate   = DateTime.SpecifyKind(offer.EndDate,   DateTimeKind.Utc);

        // Validation
        if (offer.EndDate < offer.StartDate) return BadRequest("End date cannot be before start date.");
        
        offer.Id = Guid.NewGuid();
        offer.CreatedAt = DateTime.UtcNow;
        offer.CreatedBy = GetUserId();

        _context.Offers.Add(offer);
        
        // Initial Version
        var version = new OfferVersion
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            VersionNumber = 1,
            ModifiedBy = offer.CreatedBy,
            ModifiedDate = DateTime.UtcNow,
            ChangeReason = "Initial Creation",
            PreviousConfiguration = System.Text.Json.JsonSerializer.Serialize(offer)
        };
        _context.OfferVersions.Add(version);
        
        await _context.SaveChangesAsync(cancellationToken);
        await _auditLogger.LogActionAsync(GetUserId(), "Offer Created", "Offer", offer.Id.ToString(), null, offer, GetIpAddress(), cancellationToken);

        return CreatedAtAction(nameof(GetOffer), new { id = offer.Id }, offer);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateOffer(Guid id, [FromBody] Offer updatedOffer, CancellationToken cancellationToken)
    {
        var offer = await _context.Offers.FindAsync(new object[] { id }, cancellationToken);
        if (offer == null) return NotFound();

        // Normalize DateTimes to UTC to satisfy PostgreSQL 'timestamp with time zone' requirement.
        updatedOffer.StartDate = DateTime.SpecifyKind(updatedOffer.StartDate, DateTimeKind.Utc);
        updatedOffer.EndDate   = DateTime.SpecifyKind(updatedOffer.EndDate,   DateTimeKind.Utc);

        // Validation
        if (updatedOffer.EndDate < updatedOffer.StartDate) return BadRequest("End date cannot be before start date.");

        offer.Name = updatedOffer.Name;
        offer.Description = updatedOffer.Description;
        offer.OfferType = updatedOffer.OfferType;
        offer.RulesJson = updatedOffer.RulesJson;
        offer.PromoCode = updatedOffer.PromoCode;
        offer.Priority = updatedOffer.Priority;
        offer.IsStackable = updatedOffer.IsStackable;
        offer.IsExclusive = updatedOffer.IsExclusive;
        offer.MaxUsagePerInvoice = updatedOffer.MaxUsagePerInvoice;
        offer.StartDate = updatedOffer.StartDate;
        offer.EndDate = updatedOffer.EndDate;
        offer.StoreId = updatedOffer.StoreId;

        // Activation / Deactivation Tracking
        if (!offer.IsActive && updatedOffer.IsActive)
        {
            offer.ActivatedBy = GetUserId();
        }
        else if (offer.IsActive && !updatedOffer.IsActive)
        {
            offer.DeactivatedBy = GetUserId();
        }

        offer.IsActive = updatedOffer.IsActive;
        offer.UpdatedAt = DateTime.UtcNow;
        offer.UpdatedBy = GetUserId();
        
        var oldConfig = System.Text.Json.JsonSerializer.Serialize(offer); // Previous config

        // Bump Version
        var latestVersion = await _context.OfferVersions.Where(v => v.OfferId == id).OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(cancellationToken);
        int nextVersion = (latestVersion?.VersionNumber ?? 0) + 1;
        
        var newVersion = new OfferVersion
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            VersionNumber = nextVersion,
            ModifiedBy = GetUserId(),
            ModifiedDate = DateTime.UtcNow,
            ChangeReason = "Updated via API",
            PreviousConfiguration = oldConfig
        };
        _context.OfferVersions.Add(newVersion);

        await _context.SaveChangesAsync(cancellationToken);
        await _auditLogger.LogActionAsync(GetUserId(), "Offer Modified", "Offer", offer.Id.ToString(), oldConfig, offer, GetIpAddress(), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOffer(Guid id, CancellationToken cancellationToken)
    {
        var offer = await _context.Offers.FindAsync(new object[] { id }, cancellationToken);
        if (offer == null) return NotFound();

        _context.Offers.Remove(offer);
        await _context.SaveChangesAsync(cancellationToken);
        await _auditLogger.LogActionAsync(GetUserId(), "Offer Deleted", "Offer", offer.Id.ToString(), offer, null, GetIpAddress(), cancellationToken);
        return NoContent();
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportOffers([FromQuery] string format, CancellationToken cancellationToken)
    {
        string data;
        if (format?.ToLower() == "csv")
        {
            data = await _exportService.ExportOffersToCsvAsync(cancellationToken);
            await _auditLogger.LogActionAsync(GetUserId(), "Offer Exported", "Offer", "ALL", null, new { format = "csv" }, GetIpAddress(), cancellationToken);
            return File(System.Text.Encoding.UTF8.GetBytes(data), "text/csv", $"offers_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }
        else
        {
            data = await _exportService.ExportOffersToJsonAsync(cancellationToken);
            await _auditLogger.LogActionAsync(GetUserId(), "Offer Exported", "Offer", "ALL", null, new { format = "json" }, GetIpAddress(), cancellationToken);
            return File(System.Text.Encoding.UTF8.GetBytes(data), "application/json", $"offers_export_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
        }
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportOffers([FromBody] System.Text.Json.JsonElement payload, CancellationToken cancellationToken)
    {
        try
        {
            var jsonString = payload.GetRawText();
            var imported = await _importService.ImportOffersFromJsonAsync(jsonString, GetUserId(), cancellationToken);
            await _auditLogger.LogActionAsync(GetUserId(), "Offer Imported", "Offer", "MULTIPLE", null, new { importedCount = imported.Count }, GetIpAddress(), cancellationToken);
            return Ok(imported);
        }
        catch (Exception ex)
        {
            return BadRequest($"Failed to import: {ex.Message}");
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst("id")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private string GetIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
    }
}
