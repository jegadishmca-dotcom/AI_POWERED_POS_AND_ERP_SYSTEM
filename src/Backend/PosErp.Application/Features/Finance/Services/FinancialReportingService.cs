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
