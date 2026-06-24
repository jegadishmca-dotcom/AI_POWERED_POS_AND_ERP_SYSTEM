using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Offers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Services.Offers;

public class OffersImportExportService : IOfferExportService, IOfferImportService
{
    private readonly IApplicationDbContext _context;

    public OffersImportExportService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> ExportOffersToJsonAsync(CancellationToken cancellationToken)
    {
        var offers = await _context.Offers.ToListAsync(cancellationToken);
        return JsonSerializer.Serialize(offers, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> ExportOffersByStoreToJsonAsync(Guid storeId, CancellationToken cancellationToken)
    {
        var offers = await _context.Offers.Where(o => o.StoreId == storeId || o.StoreId == null).ToListAsync(cancellationToken);
        return JsonSerializer.Serialize(offers, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> ExportOffersToCsvAsync(CancellationToken cancellationToken)
    {
        var offers = await _context.Offers.ToListAsync(cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("Id,Name,Description,OfferType,Priority,IsStackable,IsExclusive,StartDate,EndDate,IsActive");
        foreach (var o in offers)
        {
            sb.AppendLine($"{o.Id},\"{o.Name}\",\"{o.Description}\",{o.OfferType},{o.Priority},{o.IsStackable},{o.IsExclusive},{o.StartDate:O},{o.EndDate:O},{o.IsActive}");
        }
        return sb.ToString();
    }

    public async Task<List<Offer>> ImportOffersFromJsonAsync(string jsonContent, Guid currentUserId, CancellationToken cancellationToken)
    {
        var offersToImport = JsonSerializer.Deserialize<List<Offer>>(jsonContent);
        if (offersToImport == null) return new List<Offer>();

        var imported = new List<Offer>();

        foreach (var offer in offersToImport)
        {
            offer.Id = Guid.NewGuid(); // Always generate new ID for imported offer
            offer.CreatedAt = DateTime.UtcNow;
            offer.CreatedBy = currentUserId;
            offer.IsActive = false; // Always disabled by default on import
            offer.Name = $"{offer.Name} (Imported)";
            
            _context.Offers.Add(offer);
            
            // Generate initial version record
            _context.OfferVersions.Add(new OfferVersion
            {
                Id = Guid.NewGuid(),
                OfferId = offer.Id,
                VersionNumber = 1,
                ModifiedBy = currentUserId,
                ModifiedDate = DateTime.UtcNow,
                ChangeReason = "Imported Offer",
                PreviousConfiguration = JsonSerializer.Serialize(offer)
            });
            
            imported.Add(offer);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return imported;
    }

    public async Task<List<Offer>> ImportStoreSpecificOffersFromJsonAsync(string jsonContent, Guid storeId, Guid currentUserId, CancellationToken cancellationToken)
    {
        var offers = await ImportOffersFromJsonAsync(jsonContent, currentUserId, cancellationToken);
        foreach(var o in offers)
        {
            o.StoreId = storeId;
        }
        await _context.SaveChangesAsync(cancellationToken);
        return offers;
    }
}
