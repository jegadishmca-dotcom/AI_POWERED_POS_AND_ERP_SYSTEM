using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Finance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Services;

public interface IFinancialPostingService
{
    Task<Guid> PostJournalEntryAsync(Guid? storeId, DateTime date, string description, string refDoc, List<JournalLineDto> lines, CancellationToken cancellationToken);
    Task RecordGstTransactionAsync(Guid? storeId, string type, string docNumber, DateTime date, decimal taxable, decimal cgst, decimal sgst, string? gstin, CancellationToken cancellationToken);
}

public class JournalLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class FinancialPostingService : IFinancialPostingService
{
    private readonly IApplicationDbContext _context;

    public FinancialPostingService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> PostJournalEntryAsync(Guid? storeId, DateTime date, string description, string refDoc, List<JournalLineDto> lines, CancellationToken cancellationToken)
    {
        // Enforce Double-Entry Rule
        decimal totalDebit = lines.Sum(l => l.Debit);
        decimal totalCredit = lines.Sum(l => l.Credit);

        if (totalDebit != totalCredit)
            throw new Exception($"Journal is unbalanced. Debit: {totalDebit}, Credit: {totalCredit}");

        var entry = new JournalEntry
        {
            StoreId = storeId,
            EntryNumber = $"JE-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
            EntryDate = date,
            Description = description,
            ReferenceDocument = refDoc,
            IsPosted = true
        };

        foreach(var dto in lines)
        {
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountCode == dto.AccountCode, cancellationToken);
            if (account == null) throw new Exception($"Account code {dto.AccountCode} not found in COA.");

            entry.Lines.Add(new JournalEntryLine
            {
                AccountId = account.Id,
                Description = dto.Description,
                DebitAmount = dto.Debit,
                CreditAmount = dto.Credit
            });
        }

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
        return entry.Id;
    }

    public async Task RecordGstTransactionAsync(Guid? storeId, string type, string docNumber, DateTime date, decimal taxable, decimal cgst, decimal sgst, string? gstin, CancellationToken cancellationToken)
    {
        var tax = new TaxTransaction
        {
            StoreId = storeId,
            TransactionType = type,
            DocumentNumber = docNumber,
            TransactionDate = date,
            TaxableAmount = taxable,
            CgstAmount = cgst,
            SgstAmount = sgst,
            Gstin = gstin
        };

        _context.TaxTransactions.Add(tax);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
