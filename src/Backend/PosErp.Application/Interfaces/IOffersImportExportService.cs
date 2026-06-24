using PosErp.Domain.Entities.Offers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Interfaces;

public interface IOfferExportService
{
    // Level 1: CSV / JSON
    Task<string> ExportOffersToJsonAsync(CancellationToken cancellationToken);
    Task<string> ExportOffersToCsvAsync(CancellationToken cancellationToken);
    
    // Level 2 architecture stub (Store specific)
    Task<string> ExportOffersByStoreToJsonAsync(Guid storeId, CancellationToken cancellationToken);
}

public interface IOfferImportService
{
    // Level 1: JSON
    Task<List<Offer>> ImportOffersFromJsonAsync(string jsonContent, Guid currentUserId, CancellationToken cancellationToken);
    
    // Level 2 architecture stub
    Task<List<Offer>> ImportStoreSpecificOffersFromJsonAsync(string jsonContent, Guid storeId, Guid currentUserId, CancellationToken cancellationToken);
}
