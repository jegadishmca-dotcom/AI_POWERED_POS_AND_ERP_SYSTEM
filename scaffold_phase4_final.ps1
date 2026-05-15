$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Finance\Services"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Pos\Commands\GenerateEInvoice"

# 1. Update Invoice Entity
$invoicePath = "$backendDir\PosErp.Domain\Entities\Pos\PosEntities.cs"
$invoiceContent = Get-Content -Path $invoicePath -Raw
if (-not ($invoiceContent -match "Irn")) {
    $invoiceContent = $invoiceContent -replace "public DateTime CreatedAt \{ get; set; \} = DateTime.UtcNow;", "public string? Irn { get; set; } // E-Invoice Reference Number`n    public string? AckNo { get; set; }`n    public DateTime? AckDate { get; set; }`n    public string? QrCodeUrl { get; set; }`n    public string? EwayBillNo { get; set; }`n    `n    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;"
    $invoiceContent | Out-File -FilePath $invoicePath -Encoding utf8
}

# 2. Schema Migration for E-Invoice and COA
@"
-- ==============================================================================
-- PHASE 4: E-INVOICING AND REPORTS
-- ==============================================================================
ALTER TABLE invoices 
ADD COLUMN irn VARCHAR(64) UNIQUE,
ADD COLUMN ack_no VARCHAR(50),
ADD COLUMN ack_date TIMESTAMP WITH TIME ZONE,
ADD COLUMN qr_code_url TEXT,
ADD COLUMN eway_bill_no VARCHAR(50);
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Persistence\Migrations\11_EInvoiceAndReportsSchema.sql" -Encoding utf8

# 3. Seed Comprehensive Retail COA
@"
-- ==============================================================================
-- PHASE 4: RETAIL CHART OF ACCOUNTS SEED
-- ==============================================================================
-- Assets
INSERT INTO accounts (account_code, name, account_type) VALUES ('10000', 'Current Assets', 'ASSET') ON CONFLICT (account_code) DO NOTHING;
INSERT INTO accounts (account_code, name, account_type, parent_account_id) VALUES 
('10100', 'Main Cash Register', 'ASSET', (SELECT id FROM accounts WHERE account_code = '10000')),
('10200', 'HDFC Current A/C', 'ASSET', (SELECT id FROM accounts WHERE account_code = '10000')),
('10300', 'Inventory Asset', 'ASSET', (SELECT id FROM accounts WHERE account_code = '10000'))
ON CONFLICT (account_code) DO NOTHING;

-- Liabilities
INSERT INTO accounts (account_code, name, account_type) VALUES ('20000', 'Current Liabilities', 'LIABILITY') ON CONFLICT (account_code) DO NOTHING;
INSERT INTO accounts (account_code, name, account_type, parent_account_id) VALUES 
('20100', 'Accounts Payable - Vendors', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '20000')),
('20200', 'Customer Wallet Liabilities', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '20000')),
('22000', 'GST Payable', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '20000'))
ON CONFLICT (account_code) DO NOTHING;
INSERT INTO accounts (account_code, name, account_type, parent_account_id) VALUES 
('22010', 'Output CGST', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '22000')),
('22020', 'Output SGST', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '22000')),
('22030', 'Input CGST', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '22000')),
('22040', 'Input SGST', 'LIABILITY', (SELECT id FROM accounts WHERE account_code = '22000'))
ON CONFLICT (account_code) DO NOTHING;

-- Equity
INSERT INTO accounts (account_code, name, account_type) VALUES ('30000', 'Owner Equity', 'EQUITY') ON CONFLICT (account_code) DO NOTHING;

-- Revenue
INSERT INTO accounts (account_code, name, account_type) VALUES ('40000', 'Operating Revenue', 'REVENUE') ON CONFLICT (account_code) DO NOTHING;
INSERT INTO accounts (account_code, name, account_type, parent_account_id) VALUES 
('40100', 'Sales - FMCG', 'REVENUE', (SELECT id FROM accounts WHERE account_code = '40000')),
('40200', 'Sales - Grocery', 'REVENUE', (SELECT id FROM accounts WHERE account_code = '40000'))
ON CONFLICT (account_code) DO NOTHING;

-- Expenses
INSERT INTO accounts (account_code, name, account_type) VALUES ('50000', 'Operating Expenses', 'EXPENSE') ON CONFLICT (account_code) DO NOTHING;
INSERT INTO accounts (account_code, name, account_type, parent_account_id) VALUES 
('50100', 'Cost of Goods Sold (COGS)', 'EXPENSE', (SELECT id FROM accounts WHERE account_code = '50000')),
('50200', 'Staff Salary', 'EXPENSE', (SELECT id FROM accounts WHERE account_code = '50000')),
('50300', 'Electricity & Utilities', 'EXPENSE', (SELECT id FROM accounts WHERE account_code = '50000'))
ON CONFLICT (account_code) DO NOTHING;
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Persistence\Migrations\12_SeedChartOfAccounts.sql" -Encoding utf8

# 4. E-Invoice Service Stub
@"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Services;

public interface IEInvoiceService
{
    Task<EInvoiceResult> GenerateInvoiceIrnAsync(Guid invoiceId, CancellationToken cancellationToken);
}

public class EInvoiceResult
{
    public bool Success { get; set; }
    public string? Irn { get; set; }
    public string? AckNo { get; set; }
    public DateTime? AckDate { get; set; }
    public string? SignedQrCodeUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

public class EInvoiceService : IEInvoiceService
{
    public async Task<EInvoiceResult> GenerateInvoiceIrnAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        // In a real implementation, this would format the payload according to the Indian NIC e-Invoice schema
        // and send an HTTP request to a GSP (GST Suvidha Provider) like ClearTax or directly to NIC.
        
        await Task.Delay(500, cancellationToken); // Simulate API Call

        // Mock Success Response
        return new EInvoiceResult
        {
            Success = true,
            Irn = Guid.NewGuid().ToString().Replace("-", "") + Guid.NewGuid().ToString().Replace("-", ""),
            AckNo = new Random().Next(10000000, 99999999).ToString(),
            AckDate = DateTime.UtcNow,
            SignedQrCodeUrl = $"https://einvoice.gst.gov.in/qr/{invoiceId}"
        };
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Finance\Services\EInvoiceService.cs" -Encoding utf8

# 5. Generate E-Invoice Command
@"
using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Finance.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands.GenerateEInvoice;

public record GenerateEInvoiceCommand(Guid InvoiceId) : IRequest<bool>;

public class GenerateEInvoiceCommandHandler : IRequestHandler<GenerateEInvoiceCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IEInvoiceService _eInvoiceService;

    public GenerateEInvoiceCommandHandler(IApplicationDbContext context, IEInvoiceService eInvoiceService)
    {
        _context = context;
        _eInvoiceService = eInvoiceService;
    }

    public async Task<bool> Handle(GenerateEInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices.FindAsync(new object[] { request.InvoiceId }, cancellationToken);
        if (invoice == null) throw new Exception("Invoice not found");
        if (!string.IsNullOrEmpty(invoice.Irn)) return true; // Already generated

        var result = await _eInvoiceService.GenerateInvoiceIrnAsync(invoice.Id, cancellationToken);

        if (result.Success)
        {
            invoice.Irn = result.Irn;
            invoice.AckNo = result.AckNo;
            invoice.AckDate = result.AckDate;
            invoice.QrCodeUrl = result.SignedQrCodeUrl;
            invoice.Status = "E-INVOICED";

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        throw new Exception($"E-Invoice generation failed: {result.ErrorMessage}");
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Pos\Commands\GenerateEInvoice\GenerateEInvoiceCommand.cs" -Encoding utf8

# 6. Financial Reporting Service
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

public interface IFinancialReportingService
{
    Task<List<AccountBalanceDto>> GetTrialBalanceAsync(DateTime asOfDate, CancellationToken cancellationToken);
    Task<ProfitAndLossDto> GetProfitAndLossAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
}

public class AccountBalanceDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public decimal DebitBalance { get; set; }
    public decimal CreditBalance { get; set; }
}

public class ProfitAndLossDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCOGS { get; set; }
    public decimal GrossProfit => TotalRevenue - TotalCOGS;
    public decimal TotalOperatingExpenses { get; set; }
    public decimal NetProfit => GrossProfit - TotalOperatingExpenses;
    
    public List<AccountBalanceDto> RevenueAccounts { get; set; } = new();
    public List<AccountBalanceDto> ExpenseAccounts { get; set; } = new();
}

public class FinancialReportingService : IFinancialReportingService
{
    private readonly IApplicationDbContext _context;

    public FinancialReportingService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AccountBalanceDto>> GetTrialBalanceAsync(DateTime asOfDate, CancellationToken cancellationToken)
    {
        // In a high-volume ERP, we would aggregate this via a Postgres Materialized View 
        // running daily, instead of computing from millions of journal lines on the fly.
        
        var balances = await _context.JournalEntryLines
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.EntryDate <= asOfDate.Date)
            .GroupBy(l => new { l.Account.AccountCode, l.Account.Name, l.Account.AccountType })
            .Select(g => new AccountBalanceDto
            {
                AccountCode = g.Key.AccountCode,
                AccountName = g.Key.Name,
                AccountType = g.Key.AccountType,
                DebitBalance = g.Sum(x => x.DebitAmount),
                CreditBalance = g.Sum(x => x.CreditAmount)
            })
            .ToListAsync(cancellationToken);

        // Calculate Net Balances based on normal account balance rules
        foreach (var bal in balances)
        {
            if (bal.AccountType == "ASSET" || bal.AccountType == "EXPENSE")
            {
                bal.DebitBalance = bal.DebitBalance - bal.CreditBalance;
                bal.CreditBalance = 0;
            }
            else
            {
                bal.CreditBalance = bal.CreditBalance - bal.DebitBalance;
                bal.DebitBalance = 0;
            }
        }

        return balances.Where(b => b.DebitBalance != 0 || b.CreditBalance != 0).OrderBy(b => b.AccountCode).ToList();
    }

    public async Task<ProfitAndLossDto> GetProfitAndLossAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        var balances = await _context.JournalEntryLines
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.EntryDate >= startDate.Date && l.JournalEntry.EntryDate <= endDate.Date)
            .Where(l => l.Account.AccountType == "REVENUE" || l.Account.AccountType == "EXPENSE")
            .GroupBy(l => new { l.Account.AccountCode, l.Account.Name, l.Account.AccountType })
            .Select(g => new AccountBalanceDto
            {
                AccountCode = g.Key.AccountCode,
                AccountName = g.Key.Name,
                AccountType = g.Key.AccountType,
                DebitBalance = g.Sum(x => x.DebitAmount),
                CreditBalance = g.Sum(x => x.CreditAmount)
            })
            .ToListAsync(cancellationToken);

        var report = new ProfitAndLossDto();

        foreach (var b in balances)
        {
            if (b.AccountType == "REVENUE")
            {
                decimal netRevenue = b.CreditBalance - b.DebitBalance;
                b.CreditBalance = netRevenue; b.DebitBalance = 0;
                report.RevenueAccounts.Add(b);
                report.TotalRevenue += netRevenue;
            }
            else if (b.AccountType == "EXPENSE")
            {
                decimal netExpense = b.DebitBalance - b.CreditBalance;
                b.DebitBalance = netExpense; b.CreditBalance = 0;
                report.ExpenseAccounts.Add(b);
                
                if (b.AccountName.Contains("COGS") || b.AccountName.Contains("Cost of Goods"))
                    report.TotalCOGS += netExpense;
                else
                    report.TotalOperatingExpenses += netExpense;
            }
        }

        return report;
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Finance\Services\FinancialReportingService.cs" -Encoding utf8

Write-Host "Phase 4 Final Scaffolding Complete"
