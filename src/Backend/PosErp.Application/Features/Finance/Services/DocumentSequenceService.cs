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
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public DocumentSequenceService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextNumberAsync(Guid storeId, string documentType, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
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
            }

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
        finally
        {
            _semaphore.Release();
        }
    }
}
