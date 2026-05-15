$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Domain\Entities\Finance"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Finance\Services"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Finance\Queries\GetZReport"

# 1. Finance Domain Entities
@"
using System;
using System.Collections.Generic;

namespace PosErp.Domain.Entities.Finance;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccountCode { get; set; } = string.Empty; // e.g., 1000, 2000
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty; // ASSET, LIABILITY, EQUITY, REVENUE, EXPENSE
    public Guid? ParentAccountId { get; set; } // For hierarchical reporting
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class JournalEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string EntryNumber { get; set; } = string.Empty; // e.g., JE-20260515-001
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = string.Empty; // e.g., "POS Invoice #12345"
    public string ReferenceDocument { get; set; } = string.Empty; // Source document ID/Number
    public bool IsPosted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }

    public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
}

public class JournalEntryLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; } = 0;
    public decimal CreditAmount { get; set; } = 0;

    public JournalEntry JournalEntry { get; set; } = null!;
    public Account Account { get; set; } = null!;
}

public class TaxTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string TransactionType { get; set; } = string.Empty; // SALE, PURCHASE, RETURN
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    
    public decimal TaxableAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessAmount { get; set; }
    
    public string? Gstin { get; set; } // B2B if present
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\Entities\Finance\FinanceEntities.cs" -Encoding utf8

# 2. Update DbContext
$dbContextPath = "$backendDir\PosErp.Application\Interfaces\IApplicationDbContext.cs"
$dbContextContent = Get-Content -Path $dbContextPath -Raw
if (-not ($dbContextContent -match "DbSet<JournalEntry>")) {
    $dbContextContent = $dbContextContent -replace "using PosErp.Domain.Entities.Offers;", "using PosErp.Domain.Entities.Offers;`nusing PosErp.Domain.Entities.Finance;"
    $dbContextContent = $dbContextContent -replace "DbSet<Offer> Offers \{ get; \}", "DbSet<Offer> Offers { get; }`n    `n    // Finance`n    DbSet<Account> Accounts { get; }`n    DbSet<JournalEntry> JournalEntries { get; }`n    DbSet<JournalEntryLine> JournalEntryLines { get; }`n    DbSet<TaxTransaction> TaxTransactions { get; }"
    $dbContextContent | Out-File -FilePath $dbContextPath -Encoding utf8
}

# 3. Schema Migration for Accounting
@"
-- ==============================================================================
-- PHASE 4: ACCOUNTING & GST SCHEMA
-- ==============================================================================

CREATE TABLE accounts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    account_code VARCHAR(20) UNIQUE NOT NULL,
    name VARCHAR(200) NOT NULL,
    account_type VARCHAR(50) NOT NULL,
    parent_account_id UUID REFERENCES accounts(id),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE journal_entries (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    entry_number VARCHAR(100) UNIQUE NOT NULL,
    entry_date DATE NOT NULL,
    description TEXT,
    reference_document VARCHAR(100),
    is_posted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);

CREATE TABLE journal_entry_lines (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    journal_entry_id UUID NOT NULL REFERENCES journal_entries(id) ON DELETE CASCADE,
    account_id UUID NOT NULL REFERENCES accounts(id),
    description TEXT,
    debit_amount DECIMAL(18,4) DEFAULT 0,
    credit_amount DECIMAL(18,4) DEFAULT 0
);

CREATE TABLE tax_transactions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    transaction_type VARCHAR(50) NOT NULL,
    document_number VARCHAR(100) NOT NULL,
    transaction_date DATE NOT NULL,
    taxable_amount DECIMAL(18,4) NOT NULL,
    cgst_amount DECIMAL(18,4) DEFAULT 0,
    sgst_amount DECIMAL(18,4) DEFAULT 0,
    igst_amount DECIMAL(18,4) DEFAULT 0,
    cess_amount DECIMAL(18,4) DEFAULT 0,
    gstin VARCHAR(15),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Basic Seed for COA
INSERT INTO accounts (account_code, name, account_type) VALUES
('1000', 'Cash on Hand', 'ASSET'),
('1100', 'Bank Account', 'ASSET'),
('2000', 'Accounts Payable', 'LIABILITY'),
('2100', 'Customer Wallet Deposits', 'LIABILITY'),
('2200', 'CGST Payable', 'LIABILITY'),
('2201', 'SGST Payable', 'LIABILITY'),
('4000', 'Sales Revenue', 'REVENUE'),
('5000', 'Cost of Goods Sold', 'EXPENSE')
ON CONFLICT (account_code) DO NOTHING;
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Persistence\Migrations\10_AccountingSchema.sql" -Encoding utf8

# 4. Financial Posting Service
@"
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
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Finance\Services\FinancialPostingService.cs" -Encoding utf8

# 5. Z-Report Query Placeholder
@"
using MediatR;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Queries.GetZReport;

public record GetZReportQuery(Guid TerminalId, DateTime BusinessDate) : IRequest<ZReportDto>;

public class ZReportDto
{
    public decimal GrossSales { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal NetSales { get; set; }
    public decimal TotalTax { get; set; }
    public decimal FinalCollections { get; set; }
    public Dictionary<string, decimal> TenderBreakdown { get; set; } = new();
}

public class GetZReportQueryHandler : IRequestHandler<GetZReportQuery, ZReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetZReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ZReportDto> Handle(GetZReportQuery request, CancellationToken cancellationToken)
    {
        // Placeholder for complex Z-Report aggregation logic
        // This will query Invoices and TaxTransactions to build the daily reconciliation report
        return new ZReportDto();
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Finance\Queries\GetZReport\GetZReportQuery.cs" -Encoding utf8

Write-Host "Finance Scaffold Complete"
