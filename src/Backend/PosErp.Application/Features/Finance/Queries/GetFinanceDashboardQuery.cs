using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Queries;

public record GetFinanceDashboardQuery(Guid StoreId) : IRequest<FinanceDashboardDto>;

public class FinanceDashboardDto
{
    public decimal CashBalance { get; set; }
    public decimal BankBalance { get; set; }
    public decimal InventoryValue { get; set; }
    public decimal AccountsReceivable { get; set; }
    public decimal AccountsPayable { get; set; }
    public decimal GstInput { get; set; }
    public decimal GstOutput { get; set; }
    public decimal GstPayable { get; set; }
    public decimal WorkingCapital { get; set; }
    public decimal Profit { get; set; }
    public decimal SalesToday { get; set; }
    public decimal PurchasesToday { get; set; }
}

public class GetFinanceDashboardQueryHandler : IRequestHandler<GetFinanceDashboardQuery, FinanceDashboardDto>
{
    private readonly IApplicationDbContext _context;

    public GetFinanceDashboardQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<FinanceDashboardDto> Handle(GetFinanceDashboardQuery request, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;

        // Query all accounts and sum their line debits and credits from posted journal entries
        var rawBalances = await (from l in _context.JournalEntryLines
                                 join je in _context.JournalEntries on l.JournalEntryId equals je.Id
                                 join a in _context.Accounts on l.AccountId equals a.Id
                                 where je.Status == "POSTED" && je.StoreId == request.StoreId
                                 group l by new { a.AccountCode, a.AccountType } into g
                                 select new
                                 {
                                     g.Key.AccountCode,
                                     g.Key.AccountType,
                                     TotalDebit = g.Sum(x => x.DebitAmount),
                                     TotalCredit = g.Sum(x => x.CreditAmount)
                                 })
                                 .ToListAsync(cancellationToken);

        // Classify balances dynamically
        decimal cashBalance = 0;
        decimal bankBalance = 0;
        decimal inventoryValue = 0;
        decimal accountsReceivable = 0;
        decimal accountsPayable = 0;
        decimal gstInput = 0;
        decimal gstOutput = 0;
        decimal totalAssets = 0;
        decimal totalLiabilities = 0;
        decimal totalRevenue = 0;
        decimal totalExpense = 0;

        foreach (var b in rawBalances)
        {
            decimal netDebit = b.TotalDebit - b.TotalCredit;
            decimal netCredit = b.TotalCredit - b.TotalDebit;

            // Type based aggregates for working capital and profit
            if (b.AccountType == "ASSET")
            {
                totalAssets += netDebit;
            }
            else if (b.AccountType == "LIABILITY")
            {
                totalLiabilities += netCredit;
            }
            else if (b.AccountType == "REVENUE")
            {
                totalRevenue += netCredit;
            }
            else if (b.AccountType == "EXPENSE")
            {
                totalExpense += netDebit;
            }

            // Specific account mappings using Chart of Accounts code ranges
            if (b.AccountCode.StartsWith("101"))
            {
                cashBalance += netDebit;
            }
            else if (b.AccountCode.StartsWith("102"))
            {
                bankBalance += netDebit;
            }
            else if (b.AccountCode == "10300")
            {
                inventoryValue += netDebit;
            }
            else if (b.AccountCode == "20100")
            {
                accountsPayable += netCredit;
            }
            else if (b.AccountCode == "20200")
            {
                // Customer Wallet/AR
                accountsReceivable += netDebit;
            }
            else if (b.AccountCode == "22010" || b.AccountCode == "22020")
            {
                gstOutput += netCredit;
            }
            else if (b.AccountCode == "22030" || b.AccountCode == "22040")
            {
                gstInput += netDebit;
            }
        }

        // Today's Sales
        decimal salesToday = await _context.Invoices
            .Where(i => i.StoreId == request.StoreId && i.BusinessDate == today && i.Status == "COMPLETED")
            .SumAsync(i => i.NetPayable, cancellationToken);

        // Today's Purchases
        decimal purchasesToday = await _context.PurchaseBills
            .Where(b => b.StoreId == request.StoreId && b.BillDate == today)
            .SumAsync(b => b.TotalAmount, cancellationToken);

        return new FinanceDashboardDto
        {
            CashBalance = cashBalance,
            BankBalance = bankBalance,
            InventoryValue = inventoryValue,
            AccountsReceivable = accountsReceivable,
            AccountsPayable = accountsPayable,
            GstInput = gstInput,
            GstOutput = gstOutput,
            GstPayable = gstOutput - gstInput,
            WorkingCapital = totalAssets - totalLiabilities,
            Profit = totalRevenue - totalExpense,
            SalesToday = salesToday,
            PurchasesToday = purchasesToday
        };
    }
}
