using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Finance;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Services;

public interface IDocumentSequenceService
{
    Task<string> GenerateNextNumberAsync(Guid storeId, string documentType, CancellationToken cancellationToken);
}

public class DocumentSequenceService : IDocumentSequenceService
{
    private readonly IApplicationDbContext _context;

    public DocumentSequenceService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextNumberAsync(Guid storeId, string documentType, CancellationToken cancellationToken)
    {
        var sequence = await _context.DocumentSequences
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.DocumentType == documentType, cancellationToken);

        if (sequence == null)
        {
            string defaultPrefix = documentType switch
            {
                "INVOICE" => "INV",
                "PURCHASE_BILL" => "PB",
                "SUPPLIER_PAYMENT" => "SP",
                "CUSTOMER_RECEIPT" => "CR",
                "PETTY_CASH" => "PCV",
                "JOURNAL_ENTRY" => "JE",
                "INTER_STORE_TRANSFER" => "IST",
                "PURCHASE_RETURN" => "PR",
                "SALES_RETURN" => "SR",
                _ => "DOC"
            };

            sequence = new DocumentSequence
            {
                StoreId = storeId,
                DocumentType = documentType,
                Prefix = defaultPrefix,
                CurrentNumber = 0,
                Padding = 6
            };
            _context.DocumentSequences.Add(sequence);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
                // Concurrency fallback: if another thread inserted it first, detach ours and reload the existing row
                ((DbContext)_context).Entry(sequence).State = EntityState.Detached;
                sequence = await _context.DocumentSequences
                    .FirstOrDefaultAsync(s => s.StoreId == storeId && s.DocumentType == documentType, cancellationToken);
                
                if (sequence == null) throw;
            }
        }

        // Acquire PostgreSQL row-level lock (FOR UPDATE) to serialize sequence updates cross-process
        await ((DbContext)_context).Database.ExecuteSqlRawAsync(
            "SELECT 1 FROM document_sequences WHERE store_id = {0} AND document_type = {1} FOR UPDATE",
            new object[] { storeId, documentType },
            cancellationToken);

        // Reload the entity state to get the latest committed CurrentNumber from the database
        await ((DbContext)_context).Entry(sequence).ReloadAsync(cancellationToken);

        sequence.CurrentNumber++;
        await _context.SaveChangesAsync(cancellationToken);

        string paddedNumber = sequence.CurrentNumber.ToString().PadLeft(sequence.Padding, '0');
        string suffixStr = string.IsNullOrWhiteSpace(sequence.Suffix) ? "" : $"-{sequence.Suffix}";

        string storeSuffix = "";
        if (storeId != Guid.Empty && storeId != Guid.Parse("00000000-0000-0000-0000-000000000000"))
        {
            var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == storeId, cancellationToken);
            if (store != null && !string.IsNullOrWhiteSpace(store.StoreCode))
            {
                storeSuffix = $"-{store.StoreCode}";
            }
            else
            {
                storeSuffix = $"-{storeId.ToString().Substring(0, 4)}";
            }
        }

        return $"{sequence.Prefix}{storeSuffix}-{paddedNumber}{suffixStr}";
    }
}
