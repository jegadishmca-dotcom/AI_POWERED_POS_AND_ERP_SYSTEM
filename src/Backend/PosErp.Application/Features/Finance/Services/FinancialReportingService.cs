using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Finance;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Pos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Services;

public interface IFinancialReportingService
{
    // Financial Reports
    Task<List<AccountBalanceDto>> GetTrialBalanceAsync(Guid? storeId, DateTime asOfDate, CancellationToken cancellationToken);
    Task<ProfitAndLossDto> GetProfitAndLossAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    Task<BalanceSheetDto> GetBalanceSheetAsync(Guid? storeId, DateTime asOfDate, CancellationToken cancellationToken);
    Task<CashFlowStatementDto> GetCashFlowStatementAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    Task<GeneralLedgerReportDto> GetGeneralLedgerReportAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    Task<AccountLedgerReportDto> GetAccountLedgerReportAsync(Guid? storeId, string accountCode, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    Task<DailySalesSummaryDto> GetDailySalesSummaryAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);

    // Inventory Reports
    Task<List<InventoryValuationDto>> GetInventoryValuationAsync(Guid? storeId, DateTime asOfDate, CancellationToken cancellationToken);
    Task<List<StockAgingDto>> GetStockAgingAsync(Guid? storeId, DateTime asOfDate, CancellationToken cancellationToken);
    Task<List<ExpiryAnalysisDto>> GetExpiryAnalysisAsync(Guid? storeId, DateTime asOfDate, CancellationToken cancellationToken);
    Task<InventoryMovementLedgerDto> GetInventoryMovementLedgerAsync(Guid? storeId, Guid? productId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    Task<List<InventoryShrinkageDto>> GetInventoryShrinkageAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    Task<List<TopSellingProductDto>> GetTopSellingProductsAsync(Guid? storeId, DateTime startDate, DateTime endDate, int limit, CancellationToken cancellationToken);
    Task<List<SlowMovingInventoryDto>> GetSlowMovingInventoryAsync(Guid? storeId, int thresholdDays, CancellationToken cancellationToken);

    // GST Reports
    Task<GstSummaryDto> GetGstSummaryAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    Task<List<GstRegisterEntryDto>> GetSalesRegisterAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    Task<List<GstRegisterEntryDto>> GetPurchaseRegisterAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    Task<Gstr1ReportDto> GetGstr1ReportAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    Task<Gstr3BReportDto> GetGstr3BReportAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
}

#region Financial DTOs

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

public class BalanceSheetDto
{
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalEquity { get; set; }
    public List<AccountBalanceDto> AssetAccounts { get; set; } = new();
    public List<AccountBalanceDto> LiabilityAccounts { get; set; } = new();
    public List<AccountBalanceDto> EquityAccounts { get; set; } = new();
    public decimal CurrentPeriodNetProfit { get; set; }
}

public class CashFlowStatementDto
{
    public decimal BeginningCashBalance { get; set; }
    public decimal OperatingCashInflows { get; set; }
    public decimal OperatingCashOutflows { get; set; }
    public decimal NetCashFromOperatingActivities => OperatingCashInflows - OperatingCashOutflows;
    
    public decimal InvestingCashInflows { get; set; }
    public decimal InvestingCashOutflows { get; set; }
    public decimal NetCashFromInvestingActivities => InvestingCashInflows - InvestingCashOutflows;
    
    public decimal FinancingCashInflows { get; set; }
    public decimal FinancingCashOutflows { get; set; }
    public decimal NetCashFromFinancingActivities => FinancingCashInflows - FinancingCashOutflows;
    
    public decimal NetIncreaseInCash => NetCashFromOperatingActivities + NetCashFromInvestingActivities + NetCashFromFinancingActivities;
    public decimal EndingCashBalance { get; set; }
}

public class GeneralLedgerReportDto
{
    public List<GeneralLedgerEntryDto> Entries { get; set; } = new();
}

public class GeneralLedgerEntryDto
{
    public Guid Id { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ReferenceDocument { get; set; } = string.Empty;
    public string? SourceModule { get; set; }
    public string? SourceDocumentType { get; set; }
    public Guid? StoreId { get; set; }
    public string StoreCode { get; set; } = string.Empty;
    public List<GeneralLedgerLineDto> Lines { get; set; } = new();
}

public class GeneralLedgerLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
}

public class AccountLedgerReportDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public List<AccountLedgerLineDto> Lines { get; set; } = new();
    public decimal ClosingBalance { get; set; }
}

public class AccountLedgerLineDto
{
    public DateTime EntryDate { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RunningBalance { get; set; }
}

public class DailySalesSummaryDto
{
    public List<DailySalesSummaryItemDto> Days { get; set; } = new();
}

public class DailySalesSummaryItemDto
{
    public DateTime Date { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Discounts { get; set; }
    public decimal GstTax { get; set; }
    public decimal NetSales { get; set; }
    public int InvoiceCount { get; set; }
}

#endregion

#region Inventory DTOs

public class InventoryValuationDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public Guid BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CostPrice { get; set; }
    public decimal TotalValuation { get; set; }
}

public class StockAgingDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    
    public decimal Qty0_30 { get; set; }
    public decimal Value0_30 { get; set; }
    
    public decimal Qty31_60 { get; set; }
    public decimal Value31_60 { get; set; }
    
    public decimal Qty61_90 { get; set; }
    public decimal Value61_90 { get; set; }
    
    public decimal Qty90Plus { get; set; }
    public decimal Value90Plus { get; set; }
}

public class ExpiryAnalysisDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CostPrice { get; set; }
    public decimal TotalValue => Quantity * CostPrice;
    public DateTime? ExpiryDate { get; set; }
    public string ExpiryStatus { get; set; } = string.Empty; // Expired, Expiring 0-30 Days, Expiring 31-60 Days, Expiring 61-90 Days, Safe
}

public class InventoryMovementLedgerDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal OpeningQuantity { get; set; }
    public List<InventoryMovementLineDto> Lines { get; set; } = new();
    public decimal ClosingQuantity { get; set; }
}

public class InventoryMovementLineDto
{
    public DateTime Date { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal QuantityChange { get; set; }
    public decimal UnitCost { get; set; }
    public decimal RunningQuantity { get; set; }
}

public class InventoryShrinkageDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal QuantityLost { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalLossValue => QuantityLost * UnitCost;
    public string ReferenceNumber { get; set; } = string.Empty;
}

public class TopSellingProductDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}

public class SlowMovingInventoryDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public decimal AvailableQuantity { get; set; }
    public decimal CostPrice { get; set; }
    public decimal TotalValue => AvailableQuantity * CostPrice;
    public DateTime CreatedAt { get; set; }
    public int DaysSinceLastMovement { get; set; }
}

#endregion

#region GST DTOs

public class GstSummaryDto
{
    public decimal OutwardTaxableAmount { get; set; }
    public decimal OutwardCgst { get; set; }
    public decimal OutwardSgst { get; set; }
    public decimal OutwardIgst { get; set; }
    public decimal OutwardTotalTax => OutwardCgst + OutwardSgst + OutwardIgst;
    
    public decimal InwardTaxableAmount { get; set; }
    public decimal InwardCgst { get; set; }
    public decimal InwardSgst { get; set; }
    public decimal InwardIgst { get; set; }
    public decimal InwardTotalTax => InwardCgst + InwardSgst + InwardIgst;
    
    public decimal NetCgstPayable => OutwardCgst - InwardCgst;
    public decimal NetSgstPayable => OutwardSgst - InwardSgst;
    public decimal NetIgstPayable => OutwardIgst - InwardIgst;
    public decimal NetTotalTaxPayable => NetCgstPayable + NetSgstPayable + NetIgstPayable;
}

public class GstRegisterEntryDto
{
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string? Gstin { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal CessAmount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class Gstr1ReportDto
{
    public List<Gstr1B2BEntryDto> B2BSupplies { get; set; } = new();
    public List<Gstr1B2CEntryDto> B2CSupplies { get; set; } = new();
    public List<Gstr1HsnSummaryDto> HsnSummary { get; set; } = new();
}

public class Gstr1B2BEntryDto
{
    public string CustomerName { get; set; } = string.Empty;
    public string Gstin { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal TotalInvoiceValue { get; set; }
}

public class Gstr1B2CEntryDto
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal TotalInvoiceValue { get; set; }
}

public class Gstr1HsnSummaryDto
{
    public string HsnCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Uom { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
}

public class Gstr3BReportDto
{
    public decimal OutwardTaxableValue { get; set; }
    public decimal OutwardCgst { get; set; }
    public decimal OutwardSgst { get; set; }
    public decimal OutwardIgst { get; set; }
    
    public decimal ItcTaxableValue { get; set; }
    public decimal ItcCgst { get; set; }
    public decimal ItcSgst { get; set; }
    public decimal ItcIgst { get; set; }
    
    public decimal NetCgstLiability => OutwardCgst - ItcCgst;
    public decimal NetSgstLiability => OutwardSgst - ItcSgst;
    public decimal NetIgstLiability => OutwardIgst - ItcIgst;
}

#endregion

public class FinancialReportingService : IFinancialReportingService
{
    private readonly IApplicationDbContext _context;

    public FinancialReportingService(IApplicationDbContext context)
    {
        _context = context;
    }

    #region Helper Method

    private async Task<List<AccountBalanceDto>> GetRawBalancesAsync(Guid? storeId, DateTime endDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var query = _context.JournalEntryLines
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.EntryDate <= endDate.Date);

        if (storeId.HasValue)
        {
            query = query.Where(l => l.StoreId == checkStoreId || l.JournalEntry.StoreId == checkStoreId);
        }

        var raw = await query
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

        return raw;
    }

    #endregion

    #region Financial Reports implementation

    public async Task<List<AccountBalanceDto>> GetTrialBalanceAsync(Guid? storeId, DateTime asOfDate, CancellationToken cancellationToken)
    {
        var balances = await GetRawBalancesAsync(storeId, asOfDate, cancellationToken);

        // Consolidated reporting eliminates Inter-Store Clearing balance (Account 10900)
        if (!storeId.HasValue)
        {
            balances = balances.Where(b => b.AccountCode != "10900").ToList();
        }

        // Apply normal account balance rules
        foreach (var bal in balances)
        {
            if (bal.AccountType == "ASSET" || bal.AccountType == "EXPENSE")
            {
                decimal net = bal.DebitBalance - bal.CreditBalance;
                if (net >= 0) { bal.DebitBalance = net; bal.CreditBalance = 0; }
                else { bal.DebitBalance = 0; bal.CreditBalance = -net; }
            }
            else
            {
                decimal net = bal.CreditBalance - bal.DebitBalance;
                if (net >= 0) { bal.CreditBalance = net; bal.DebitBalance = 0; }
                else { bal.DebitBalance = 0; bal.CreditBalance = -net; }
            }
        }

        return balances.Where(b => b.DebitBalance != 0 || b.CreditBalance != 0).OrderBy(b => b.AccountCode).ToList();
    }

    public async Task<ProfitAndLossDto> GetProfitAndLossAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var query = _context.JournalEntryLines
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.EntryDate >= startDate.Date && l.JournalEntry.EntryDate <= endDate.Date)
            .Where(l => l.Account.AccountType == "REVENUE" || l.Account.AccountType == "EXPENSE");

        if (storeId.HasValue)
        {
            query = query.Where(l => l.StoreId == checkStoreId || l.JournalEntry.StoreId == checkStoreId);
        }

        var balances = await query
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

        if (!storeId.HasValue)
        {
            balances = balances.Where(b => b.AccountCode != "10900").ToList();
        }

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
                
                if (b.AccountCode == "5000" || b.AccountCode == "50100" || b.AccountName.Contains("COGS") || b.AccountName.Contains("Cost of Goods"))
                    report.TotalCOGS += netExpense;
                else
                    report.TotalOperatingExpenses += netExpense;
            }
        }

        return report;
    }

    public async Task<BalanceSheetDto> GetBalanceSheetAsync(Guid? storeId, DateTime asOfDate, CancellationToken cancellationToken)
    {
        var raw = await GetRawBalancesAsync(storeId, asOfDate, cancellationToken);

        // 1. Calculate Retained Earnings / Current Period Net Profit on the fly from revenue/expenses
        decimal totalRevenue = 0;
        decimal totalExpenses = 0;

        foreach (var r in raw)
        {
            if (r.AccountType == "REVENUE")
            {
                totalRevenue += (r.CreditBalance - r.DebitBalance);
            }
            else if (r.AccountType == "EXPENSE")
            {
                totalExpenses += (r.DebitBalance - r.CreditBalance);
            }
        }
        decimal currentPeriodNetProfit = totalRevenue - totalExpenses;

        // 2. Classify Assets, Liabilities, and Equity
        var assetList = new List<AccountBalanceDto>();
        var liabilityList = new List<AccountBalanceDto>();
        var equityList = new List<AccountBalanceDto>();

        decimal totalAssets = 0;
        decimal totalLiabilities = 0;
        decimal totalEquity = 0;

        foreach (var b in raw)
        {
            if (b.AccountCode == "10900" && !storeId.HasValue) continue; // Eliminate clearing account

            if (b.AccountType == "ASSET")
            {
                decimal net = b.DebitBalance - b.CreditBalance;
                b.DebitBalance = net; b.CreditBalance = 0;
                assetList.Add(b);
                totalAssets += net;
            }
            else if (b.AccountType == "LIABILITY")
            {
                decimal net = b.CreditBalance - b.DebitBalance;
                b.CreditBalance = net; b.DebitBalance = 0;
                liabilityList.Add(b);
                totalLiabilities += net;
            }
            else if (b.AccountType == "EQUITY")
            {
                decimal net = b.CreditBalance - b.DebitBalance;
                b.CreditBalance = net; b.DebitBalance = 0;
                equityList.Add(b);
                totalEquity += net;
            }
        }

        // Add Current Period Net Profit line
        equityList.Add(new AccountBalanceDto
        {
            AccountCode = "3999",
            AccountName = "Current Period Net Profit",
            AccountType = "EQUITY",
            CreditBalance = currentPeriodNetProfit,
            DebitBalance = 0
        });
        totalEquity += currentPeriodNetProfit;

        return new BalanceSheetDto
        {
            TotalAssets = totalAssets,
            TotalLiabilities = totalLiabilities,
            TotalEquity = totalEquity,
            AssetAccounts = assetList.Where(a => a.DebitBalance != 0).OrderBy(a => a.AccountCode).ToList(),
            LiabilityAccounts = liabilityList.Where(l => l.CreditBalance != 0).OrderBy(l => l.AccountCode).ToList(),
            EquityAccounts = equityList.Where(e => e.CreditBalance != 0).OrderBy(e => e.AccountCode).ToList(),
            CurrentPeriodNetProfit = currentPeriodNetProfit
        };
    }

    public async Task<CashFlowStatementDto> GetCashFlowStatementAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;

        // Cash & Bank account codes: 1000, 1100, 10100, 10200
        var cashBankCodes = new[] { "1000", "1100", "10100", "10200" };
        var cashBankAccounts = await _context.Accounts
            .Where(a => cashBankCodes.Contains(a.AccountCode))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // 1. Beginning Cash Balance
        var openingQuery = _context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.EntryDate < startDate.Date && cashBankAccounts.Contains(l.AccountId));

        if (storeId.HasValue)
        {
            openingQuery = openingQuery.Where(l => l.StoreId == checkStoreId || l.JournalEntry.StoreId == checkStoreId);
        }
        decimal beginningCashBalance = await openingQuery.SumAsync(l => l.DebitAmount - l.CreditAmount, cancellationToken);

        // 2. Ending Cash Balance
        var endingQuery = _context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.EntryDate <= endDate.Date && cashBankAccounts.Contains(l.AccountId));

        if (storeId.HasValue)
        {
            endingQuery = endingQuery.Where(l => l.StoreId == checkStoreId || l.JournalEntry.StoreId == checkStoreId);
        }
        decimal endingCashBalance = await endingQuery.SumAsync(l => l.DebitAmount - l.CreditAmount, cancellationToken);

        // 3. Transactions inside range
        var activeLinesQuery = _context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .ThenInclude(e => e.Lines)
            .ThenInclude(ln => ln.Account)
            .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.EntryDate >= startDate.Date && l.JournalEntry.EntryDate <= endDate.Date && cashBankAccounts.Contains(l.AccountId));

        if (storeId.HasValue)
        {
            activeLinesQuery = activeLinesQuery.Where(l => l.StoreId == checkStoreId || l.JournalEntry.StoreId == checkStoreId);
        }

        var lines = await activeLinesQuery.ToListAsync(cancellationToken);
        var journalEntryIds = lines.Select(l => l.JournalEntryId).Distinct().ToList();

        // Load all lines for these journal entries to analyze counter-parties
        var allJeLines = await _context.JournalEntryLines
            .Include(l => l.Account)
            .Where(l => journalEntryIds.Contains(l.JournalEntryId))
            .ToListAsync(cancellationToken);

        var linesByJe = allJeLines.ToLookup(l => l.JournalEntryId);

        decimal opIn = 0, opOut = 0;
        decimal invIn = 0, invOut = 0;
        decimal finIn = 0, finOut = 0;

        foreach (var jeId in journalEntryIds)
        {
            var jeLines = linesByJe[jeId].ToList();
            
            // Net cash change in this journal entry
            decimal netCash = jeLines
                .Where(l => cashBankAccounts.Contains(l.AccountId) && (!storeId.HasValue || l.StoreId == checkStoreId))
                .Sum(l => l.DebitAmount - l.CreditAmount);

            if (netCash == 0) continue;

            // Analyze counter-party accounts
            var otherLines = jeLines.Where(l => !cashBankAccounts.Contains(l.AccountId)).ToList();
            
            bool isInvesting = otherLines.Any(l => l.Account.AccountCode.StartsWith("15") || l.Account.AccountType == "ASSET" && (l.Account.Name.Contains("Asset") || l.Account.Name.Contains("Equipment")));
            bool isFinancing = otherLines.Any(l => l.Account.AccountType == "EQUITY" || l.Account.AccountCode.StartsWith("3"));

            if (netCash > 0)
            {
                if (isInvesting) invIn += netCash;
                else if (isFinancing) finIn += netCash;
                else opIn += netCash;
            }
            else
            {
                decimal absCash = -netCash;
                if (isInvesting) invOut += absCash;
                else if (isFinancing) finOut += absCash;
                else opOut += absCash;
            }
        }

        return new CashFlowStatementDto
        {
            BeginningCashBalance = beginningCashBalance,
            EndingCashBalance = endingCashBalance,
            OperatingCashInflows = opIn,
            OperatingCashOutflows = opOut,
            InvestingCashInflows = invIn,
            InvestingCashOutflows = invOut,
            FinancingCashInflows = finIn,
            FinancingCashOutflows = finOut
        };
    }

    public async Task<GeneralLedgerReportDto> GetGeneralLedgerReportAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var query = _context.JournalEntries
            .Include(e => e.Lines)
                .ThenInclude(l => l.Account)
            .Where(e => e.IsPosted && e.EntryDate >= startDate.Date && e.EntryDate <= endDate.Date);

        if (storeId.HasValue)
        {
            query = query.Where(e => e.StoreId == checkStoreId || e.Lines.Any(l => l.StoreId == checkStoreId));
        }

        var entries = await query.OrderBy(e => e.EntryDate).ThenBy(e => e.EntryNumber).ToListAsync(cancellationToken);
        
        var storesDict = await _context.Stores.ToDictionaryAsync(s => s.Id, s => s.StoreCode, cancellationToken);

        var report = new GeneralLedgerReportDto();
        foreach (var e in entries)
        {
            var storeCode = e.StoreId.HasValue && storesDict.TryGetValue(e.StoreId.Value, out var sc) ? sc : "HQ";
            var entryDto = new GeneralLedgerEntryDto
            {
                Id = e.Id,
                EntryNumber = e.EntryNumber,
                EntryDate = e.EntryDate,
                Description = e.Description,
                ReferenceDocument = e.ReferenceDocument,
                SourceModule = e.SourceModule,
                SourceDocumentType = e.SourceDocumentType,
                StoreId = e.StoreId,
                StoreCode = storeCode
            };

            foreach (var l in e.Lines)
            {
                entryDto.Lines.Add(new GeneralLedgerLineDto
                {
                    AccountCode = l.Account.AccountCode,
                    AccountName = l.Account.Name,
                    Description = l.Description,
                    DebitAmount = l.DebitAmount,
                    CreditAmount = l.CreditAmount
                });
            }

            report.Entries.Add(entryDto);
        }

        return report;
    }

    public async Task<AccountLedgerReportDto> GetAccountLedgerReportAsync(Guid? storeId, string accountCode, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var account = await _context.Accounts.FirstOrDefaultAsync(a => a.AccountCode == accountCode, cancellationToken);
        if (account == null)
        {
            throw new InvalidOperationException($"Account code {accountCode} not found.");
        }

        // Calculate opening balance
        var opQuery = _context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.EntryDate < startDate.Date && l.AccountId == account.Id);

        if (storeId.HasValue)
        {
            opQuery = opQuery.Where(l => l.StoreId == checkStoreId || l.JournalEntry.StoreId == checkStoreId);
        }

        decimal openingBalance = 0;
        var opDebits = await opQuery.SumAsync(l => l.DebitAmount, cancellationToken);
        var opCredits = await opQuery.SumAsync(l => l.CreditAmount, cancellationToken);

        if (account.AccountType == "ASSET" || account.AccountType == "EXPENSE")
            openingBalance = opDebits - opCredits;
        else
            openingBalance = opCredits - opDebits;

        // Fetch lines in range
        var linesQuery = _context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.EntryDate >= startDate.Date && l.JournalEntry.EntryDate <= endDate.Date && l.AccountId == account.Id);

        if (storeId.HasValue)
        {
            linesQuery = linesQuery.Where(l => l.StoreId == checkStoreId || l.JournalEntry.StoreId == checkStoreId);
        }

        var lines = await linesQuery.OrderBy(l => l.JournalEntry.EntryDate).ThenBy(l => l.JournalEntry.EntryNumber).ToListAsync(cancellationToken);

        var report = new AccountLedgerReportDto
        {
            AccountCode = account.AccountCode,
            AccountName = account.Name,
            OpeningBalance = openingBalance
        };

        decimal currentBalance = openingBalance;
        foreach (var l in lines)
        {
            if (account.AccountType == "ASSET" || account.AccountType == "EXPENSE")
                currentBalance += (l.DebitAmount - l.CreditAmount);
            else
                currentBalance += (l.CreditAmount - l.DebitAmount);

            report.Lines.Add(new AccountLedgerLineDto
            {
                EntryDate = l.JournalEntry.EntryDate,
                EntryNumber = l.JournalEntry.EntryNumber,
                Description = l.Description,
                DebitAmount = l.DebitAmount,
                CreditAmount = l.CreditAmount,
                RunningBalance = currentBalance
            });
        }
        report.ClosingBalance = currentBalance;

        return report;
    }

    public async Task<DailySalesSummaryDto> GetDailySalesSummaryAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var query = _context.Invoices
            .Include(i => i.Items)
            .Where(i => i.BusinessDate >= startDate.Date && i.BusinessDate <= endDate.Date && !i.IsDeleted && i.Status != "CANCELLED");

        if (storeId.HasValue)
        {
            query = query.Where(i => i.StoreId == checkStoreId);
        }

        var invoices = await query.ToListAsync(cancellationToken);

        var dailySummary = invoices
            .GroupBy(i => i.BusinessDate.Date)
            .Select(g => new DailySalesSummaryItemDto
            {
                Date = g.Key,
                GrossSales = g.Sum(i => i.Items.Sum(item => item.Quantity * item.UnitPrice)),
                Discounts = g.Sum(i => i.DiscountAmount),
                GstTax = g.Sum(i => i.TaxAmount),
                NetSales = g.Sum(i => i.NetPayable),
                InvoiceCount = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        return new DailySalesSummaryDto { Days = dailySummary };
    }

    #endregion

    #region Inventory Reports implementation

    public async Task<List<InventoryValuationDto>> GetInventoryValuationAsync(Guid? storeId, DateTime asOfDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;

        if (asOfDate.Date >= DateTime.Today)
        {
            var batchesQuery = _context.ProductBatches
                .Include(b => b.Product)
                .Where(b => b.AvailableQuantity > 0 && b.IsActive);

            if (storeId.HasValue)
            {
                batchesQuery = batchesQuery.Where(b => b.StoreId == checkStoreId);
            }

            var batches = await batchesQuery.ToListAsync(cancellationToken);

            return batches.Select(b => new InventoryValuationDto
            {
                ProductId = b.ProductId,
                ProductCode = b.Product.ProductCode,
                ProductName = b.Product.Name,
                BatchId = b.Id,
                BatchNumber = b.BatchNumber,
                Quantity = b.AvailableQuantity,
                CostPrice = b.CostPrice,
                TotalValuation = b.AvailableQuantity * b.CostPrice
            }).ToList();
        }
        else
        {
            // Reconstruct batch balance as of past date
            var ledgerQuery = _context.StockLedger
                .Where(l => l.BusinessDate <= asOfDate.Date);

            if (storeId.HasValue)
            {
                ledgerQuery = ledgerQuery.Where(l => l.StoreId == checkStoreId);
            }

            var reconstructedQuantities = await ledgerQuery
                .GroupBy(l => new { l.ProductId, l.BatchId })
                .Select(g => new
                {
                    g.Key.ProductId,
                    g.Key.BatchId,
                    Quantity = g.Sum(x => x.Quantity)
                })
                .Where(x => x.Quantity > 0)
                .ToListAsync(cancellationToken);

            var batchIds = reconstructedQuantities.Select(x => x.BatchId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
            var batches = await _context.ProductBatches
                .Include(b => b.Product)
                .Where(b => batchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, cancellationToken);

            var result = new List<InventoryValuationDto>();
            foreach (var rq in reconstructedQuantities)
            {
                if (rq.BatchId.HasValue && batches.TryGetValue(rq.BatchId.Value, out var batch))
                {
                    result.Add(new InventoryValuationDto
                    {
                        ProductId = rq.ProductId,
                        ProductCode = batch.Product.ProductCode,
                        ProductName = batch.Product.Name,
                        BatchId = batch.Id,
                        BatchNumber = batch.BatchNumber,
                        Quantity = rq.Quantity,
                        CostPrice = batch.CostPrice,
                        TotalValuation = rq.Quantity * batch.CostPrice
                    });
                }
            }
            return result;
        }
    }

    public async Task<List<StockAgingDto>> GetStockAgingAsync(Guid? storeId, DateTime asOfDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var query = _context.ProductBatches
            .Include(b => b.Product)
            .Where(b => b.AvailableQuantity > 0 && b.IsActive);

        if (storeId.HasValue)
        {
            query = query.Where(b => b.StoreId == checkStoreId);
        }

        var batches = await query.ToListAsync(cancellationToken);

        var reportList = new List<StockAgingDto>();
        foreach (var b in batches)
        {
            int ageDays = (asOfDate.Date - b.CreatedAt.Date).Days;
            if (ageDays < 0) ageDays = 0;

            decimal totalQty = b.AvailableQuantity;
            decimal totalVal = b.AvailableQuantity * b.CostPrice;

            var dto = new StockAgingDto
            {
                ProductId = b.ProductId,
                ProductCode = b.Product.ProductCode,
                ProductName = b.Product.Name,
                BatchNumber = b.BatchNumber,
                CostPrice = b.CostPrice,
                TotalQuantity = totalQty,
                TotalValue = totalVal
            };

            if (ageDays <= 30)
            {
                dto.Qty0_30 = totalQty;
                dto.Value0_30 = totalVal;
            }
            else if (ageDays <= 60)
            {
                dto.Qty31_60 = totalQty;
                dto.Value31_60 = totalVal;
            }
            else if (ageDays <= 90)
            {
                dto.Qty61_90 = totalQty;
                dto.Value61_90 = totalVal;
            }
            else
            {
                dto.Qty90Plus = totalQty;
                dto.Value90Plus = totalVal;
            }

            reportList.Add(dto);
        }

        return reportList;
    }

    public async Task<List<ExpiryAnalysisDto>> GetExpiryAnalysisAsync(Guid? storeId, DateTime asOfDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var query = _context.ProductBatches
            .Include(b => b.Product)
            .Where(b => b.AvailableQuantity > 0 && b.IsActive && b.ExpiryDate != null);

        if (storeId.HasValue)
        {
            query = query.Where(b => b.StoreId == checkStoreId);
        }

        var batches = await query.ToListAsync(cancellationToken);

        var report = new List<ExpiryAnalysisDto>();
        foreach (var b in batches)
        {
            var expiry = b.ExpiryDate!.Value.Date;
            var daysRemaining = (expiry - asOfDate.Date).Days;
            
            string status;
            if (daysRemaining < 0)
                status = "Expired";
            else if (daysRemaining <= 30)
                status = "Expiring 0-30 Days";
            else if (daysRemaining <= 60)
                status = "Expiring 31-60 Days";
            else if (daysRemaining <= 90)
                status = "Expiring 61-90 Days";
            else
                status = "Safe";

            report.Add(new ExpiryAnalysisDto
            {
                ProductId = b.ProductId,
                ProductCode = b.Product?.ProductCode ?? string.Empty,
                ProductName = b.Product?.Name ?? "Unknown Product",
                BatchNumber = b.BatchNumber,
                Quantity = b.AvailableQuantity,
                CostPrice = b.CostPrice,
                ExpiryDate = b.ExpiryDate,
                ExpiryStatus = status
            });
        }

        return report.OrderBy(r => r.ExpiryDate).ToList();
    }

    public async Task<InventoryMovementLedgerDto> GetInventoryMovementLedgerAsync(Guid? storeId, Guid? productId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        if (!productId.HasValue)
        {
            throw new ArgumentException("Product ID is required for Inventory Movement Ledger.");
        }

        var product = await _context.Products.FindAsync(new object[] { productId.Value }, cancellationToken);
        if (product == null)
        {
            throw new InvalidOperationException($"Product with ID {productId.Value} not found.");
        }

        // Opening Quantity
        var opQuery = _context.StockLedger.Where(l => l.ProductId == productId.Value && l.BusinessDate < startDate.Date);
        if (storeId.HasValue)
        {
            opQuery = opQuery.Where(l => l.StoreId == checkStoreId);
        }
        decimal openingQuantity = await opQuery.SumAsync(l => l.Quantity, cancellationToken);

        // Movements in range
        var movementsQuery = _context.StockLedger
            .Where(l => l.ProductId == productId.Value && l.BusinessDate >= startDate.Date && l.BusinessDate <= endDate.Date);
        
        if (storeId.HasValue)
        {
            movementsQuery = movementsQuery.Where(l => l.StoreId == checkStoreId);
        }

        var entries = await movementsQuery.OrderBy(l => l.BusinessDate).ThenBy(l => l.CreatedAt).ToListAsync(cancellationToken);

        var report = new InventoryMovementLedgerDto
        {
            ProductId = product.Id,
            ProductCode = product.ProductCode,
            ProductName = product.Name,
            OpeningQuantity = openingQuantity
        };

        decimal currentQty = openingQuantity;
        foreach (var e in entries)
        {
            currentQty += e.Quantity;
            report.Lines.Add(new InventoryMovementLineDto
            {
                Date = e.BusinessDate,
                MovementType = e.MovementType,
                ReferenceNumber = e.ReferenceNumber,
                QuantityChange = e.Quantity,
                UnitCost = e.UnitCost,
                RunningQuantity = currentQty
            });
        }
        report.ClosingQuantity = currentQty;

        return report;
    }

    public async Task<List<InventoryShrinkageDto>> GetInventoryShrinkageAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var query = _context.StockLedger
            .Where(l => l.BusinessDate >= startDate.Date && l.BusinessDate <= endDate.Date && l.Quantity < 0)
            .Where(l => l.MovementType == "SHRINKAGE" || l.MovementType == "WASTE" || l.MovementType == "ADJUSTMENT");

        if (storeId.HasValue)
        {
            query = query.Where(l => l.StoreId == checkStoreId);
        }

        var lines = await query.ToListAsync(cancellationToken);

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _context.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        var batchIds = lines.Select(l => l.BatchId).Where(b => b.HasValue).Select(b => b!.Value).Distinct().ToList();
        var batches = await _context.ProductBatches.Where(b => batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, cancellationToken);

        return lines.Select(l => new InventoryShrinkageDto
        {
            ProductId = l.ProductId,
            ProductCode = products.TryGetValue(l.ProductId, out var prod) ? prod.ProductCode : string.Empty,
            ProductName = prod?.Name ?? "Unknown",
            BatchNumber = l.BatchId.HasValue && batches.TryGetValue(l.BatchId.Value, out var bat) ? bat.BatchNumber : "N/A",
            Date = l.BusinessDate,
            QuantityLost = Math.Abs(l.Quantity),
            UnitCost = l.UnitCost,
            ReferenceNumber = l.ReferenceNumber
        }).ToList();
    }

    public async Task<List<TopSellingProductDto>> GetTopSellingProductsAsync(Guid? storeId, DateTime startDate, DateTime endDate, int limit, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var query = _context.InvoiceItems
            .Include(i => i.Invoice)
            .Where(i => !i.IsDeleted && i.Invoice.Status != "CANCELLED" && i.BusinessDate >= startDate.Date && i.BusinessDate <= endDate.Date);

        if (storeId.HasValue)
        {
            query = query.Where(i => i.StoreId == checkStoreId);
        }

        var grouped = await query
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.TotalAmount)
            })
            .OrderByDescending(x => x.QuantitySold)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var productIds = grouped.Select(x => x.ProductId).ToList();
        var products = await _context.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        return grouped.Select(x => new TopSellingProductDto
        {
            ProductId = x.ProductId,
            ProductCode = products.TryGetValue(x.ProductId, out var prod) ? prod.ProductCode : string.Empty,
            ProductName = x.ProductName,
            QuantitySold = x.QuantitySold,
            Revenue = x.Revenue
        }).ToList();
    }

    public async Task<List<SlowMovingInventoryDto>> GetSlowMovingInventoryAsync(Guid? storeId, int thresholdDays, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var thresholdDate = DateTime.Today.AddDays(-thresholdDays);

        // Get active batches with stock
        var batchesQuery = _context.ProductBatches
            .Include(b => b.Product)
            .Where(b => b.AvailableQuantity > 0 && b.IsActive);

        if (storeId.HasValue)
        {
            batchesQuery = batchesQuery.Where(b => b.StoreId == checkStoreId);
        }

        var batches = await batchesQuery.ToListAsync(cancellationToken);
        var batchIds = batches.Select(b => b.Id).ToList();

        // Find batches that had sale movements in the threshold period
        var saleMovements = await _context.StockLedger
            .Where(l => l.BatchId != null && batchIds.Contains(l.BatchId.Value) && l.BusinessDate >= thresholdDate.Date)
            .Where(l => l.MovementType.StartsWith("SALE") || l.MovementType.StartsWith("POS"))
            .Select(l => l.BatchId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Slow moving are those with NO sale movements
        var slowBatches = batches.Where(b => !saleMovements.Contains(b.Id)).ToList();

        var result = new List<SlowMovingInventoryDto>();
        foreach (var b in slowBatches)
        {
            // Find last sale date
            var lastSale = await _context.StockLedger
                .Where(l => l.BatchId == b.Id && (l.MovementType.StartsWith("SALE") || l.MovementType.StartsWith("POS")))
                .OrderByDescending(l => l.BusinessDate)
                .Select(l => (DateTime?)l.BusinessDate)
                .FirstOrDefaultAsync(cancellationToken);

            int daysSince = lastSale.HasValue ? (DateTime.Today - lastSale.Value.Date).Days : (DateTime.Today - b.CreatedAt.Date).Days;
            if (daysSince < 0) daysSince = 0;

            result.Add(new SlowMovingInventoryDto
            {
                ProductId = b.ProductId,
                ProductCode = b.Product.ProductCode,
                ProductName = b.Product.Name,
                BatchNumber = b.BatchNumber,
                AvailableQuantity = b.AvailableQuantity,
                CostPrice = b.CostPrice,
                CreatedAt = b.CreatedAt,
                DaysSinceLastMovement = daysSince
            });
        }

        return result.OrderByDescending(x => x.DaysSinceLastMovement).ToList();
    }

    #endregion

    #region GST Reports implementation

    public async Task<GstSummaryDto> GetGstSummaryAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var query = _context.TaxTransactions.Where(t => t.TransactionDate >= startDate.Date && t.TransactionDate <= endDate.Date);

        if (storeId.HasValue)
        {
            query = query.Where(t => t.StoreId == checkStoreId);
        }

        var txs = await query.ToListAsync(cancellationToken);

        var summary = new GstSummaryDto();

        foreach (var t in txs)
        {
            if (t.TransactionType == "SALE")
            {
                summary.OutwardTaxableAmount += t.TaxableAmount;
                summary.OutwardCgst += t.CgstAmount;
                summary.OutwardSgst += t.SgstAmount;
                summary.OutwardIgst += t.IgstAmount;
            }
            else if (t.TransactionType == "RETURN")
            {
                // Sales returns reduce outward liabilities
                summary.OutwardTaxableAmount -= t.TaxableAmount;
                summary.OutwardCgst -= t.CgstAmount;
                summary.OutwardSgst -= t.SgstAmount;
                summary.OutwardIgst -= t.IgstAmount;
            }
            else if (t.TransactionType == "PURCHASE")
            {
                summary.InwardTaxableAmount += t.TaxableAmount;
                summary.InwardCgst += t.CgstAmount;
                summary.InwardSgst += t.SgstAmount;
                summary.InwardIgst += t.IgstAmount;
            }
        }

        return summary;
    }

    public async Task<List<GstRegisterEntryDto>> GetSalesRegisterAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var query = _context.TaxTransactions
            .Where(t => t.TransactionDate >= startDate.Date && t.TransactionDate <= endDate.Date)
            .Where(t => t.TransactionType == "SALE" || t.TransactionType == "RETURN");

        if (storeId.HasValue)
        {
            query = query.Where(t => t.StoreId == checkStoreId);
        }

        var txs = await query.OrderBy(t => t.TransactionDate).ThenBy(t => t.DocumentNumber).ToListAsync(cancellationToken);

        return txs.Select(t =>
        {
            decimal mult = t.TransactionType == "RETURN" ? -1m : 1m;
            decimal taxes = t.CgstAmount + t.SgstAmount + t.IgstAmount + t.CessAmount;
            return new GstRegisterEntryDto
            {
                DocumentNumber = t.DocumentNumber,
                TransactionDate = t.TransactionDate,
                Gstin = t.Gstin,
                TaxableAmount = t.TaxableAmount * mult,
                CgstAmount = t.CgstAmount * mult,
                SgstAmount = t.SgstAmount * mult,
                IgstAmount = t.IgstAmount * mult,
                CessAmount = t.CessAmount * mult,
                TotalAmount = (t.TaxableAmount + taxes) * mult
            };
        }).ToList();
    }

    public async Task<List<GstRegisterEntryDto>> GetPurchaseRegisterAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var query = _context.TaxTransactions
            .Where(t => t.TransactionDate >= startDate.Date && t.TransactionDate <= endDate.Date)
            .Where(t => t.TransactionType == "PURCHASE");

        if (storeId.HasValue)
        {
            query = query.Where(t => t.StoreId == checkStoreId);
        }

        var txs = await query.OrderBy(t => t.TransactionDate).ThenBy(t => t.DocumentNumber).ToListAsync(cancellationToken);

        return txs.Select(t =>
        {
            decimal taxes = t.CgstAmount + t.SgstAmount + t.IgstAmount + t.CessAmount;
            return new GstRegisterEntryDto
            {
                DocumentNumber = t.DocumentNumber,
                TransactionDate = t.TransactionDate,
                Gstin = t.Gstin,
                TaxableAmount = t.TaxableAmount,
                CgstAmount = t.CgstAmount,
                SgstAmount = t.SgstAmount,
                IgstAmount = t.IgstAmount,
                CessAmount = t.CessAmount,
                TotalAmount = t.TaxableAmount + taxes
            };
        }).ToList();
    }

    public async Task<Gstr1ReportDto> GetGstr1ReportAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        
        var txsQuery = _context.TaxTransactions
            .Where(t => t.TransactionDate >= startDate.Date && t.TransactionDate <= endDate.Date && t.TransactionType == "SALE");

        if (storeId.HasValue)
        {
            txsQuery = txsQuery.Where(t => t.StoreId == checkStoreId);
        }

        var txs = await txsQuery.ToListAsync(cancellationToken);

        var docNumbers = txs.Select(t => t.DocumentNumber).ToList();
        var invoices = await _context.Invoices
            .Where(i => docNumbers.Contains(i.InvoiceNumber))
            .ToListAsync(cancellationToken);

        var customerIds = invoices.Select(i => i.CustomerId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var customers = await _context.Customers
            .Where(c => customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var invDict = invoices.ToDictionary(i => i.InvoiceNumber);

        var report = new Gstr1ReportDto();

        foreach (var t in txs)
        {
            var isB2B = !string.IsNullOrWhiteSpace(t.Gstin);
            var customerName = "Walk-in Customer";
            if (invDict.TryGetValue(t.DocumentNumber, out var inv) && inv.CustomerId.HasValue && customers.TryGetValue(inv.CustomerId.Value, out var cust))
            {
                customerName = cust.Name;
            }

            decimal cgst = t.CgstAmount;
            decimal sgst = t.SgstAmount;
            decimal igst = t.IgstAmount;
            decimal total = t.TaxableAmount + cgst + sgst + igst + t.CessAmount;

            if (isB2B)
            {
                report.B2BSupplies.Add(new Gstr1B2BEntryDto
                {
                    CustomerName = customerName,
                    Gstin = t.Gstin!,
                    InvoiceNumber = t.DocumentNumber,
                    InvoiceDate = t.TransactionDate,
                    TaxableValue = t.TaxableAmount,
                    CgstAmount = cgst,
                    SgstAmount = sgst,
                    IgstAmount = igst,
                    TotalInvoiceValue = total
                });
            }
            else
            {
                report.B2CSupplies.Add(new Gstr1B2CEntryDto
                {
                    InvoiceNumber = t.DocumentNumber,
                    InvoiceDate = t.TransactionDate,
                    TaxableValue = t.TaxableAmount,
                    CgstAmount = cgst,
                    SgstAmount = sgst,
                    IgstAmount = igst,
                    TotalInvoiceValue = total
                });
            }
        }

        var itemsQuery = from item in _context.InvoiceItems
                         join prod in _context.Products on item.ProductId equals prod.Id into prodGroup
                         from prod in prodGroup.DefaultIfEmpty()
                         where !item.IsDeleted && item.BusinessDate >= startDate.Date && item.BusinessDate <= endDate.Date
                         select new { item, prod };

        if (storeId.HasValue)
        {
            itemsQuery = itemsQuery.Where(x => x.item.StoreId == checkStoreId);
        }

        var itemsList = await itemsQuery.ToListAsync(cancellationToken);

        var hsnGroups = itemsList
            .GroupBy(x => new { Hsn = x.prod?.HsnCode ?? "N/A", Description = x.prod?.Name ?? x.item.ProductName })
            .Select(g => new Gstr1HsnSummaryDto
            {
                HsnCode = g.Key.Hsn,
                Description = g.Key.Description,
                Uom = "PCS",
                TotalQuantity = g.Sum(x => x.item.Quantity),
                TotalValue = g.Sum(x => x.item.FinalTotal),
                TaxableValue = g.Sum(x => x.item.FinalTotal - (x.item.CgstAmount + x.item.SgstAmount + x.item.CessAmount)),
                CgstAmount = g.Sum(x => x.item.CgstAmount),
                SgstAmount = g.Sum(x => x.item.SgstAmount),
                IgstAmount = 0
            })
            .ToList();

        report.HsnSummary = hsnGroups;

        return report;
    }

    public async Task<Gstr3BReportDto> GetGstr3BReportAsync(Guid? storeId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        Guid checkStoreId = storeId ?? Guid.Empty;
        var query = _context.TaxTransactions.Where(t => t.TransactionDate >= startDate.Date && t.TransactionDate <= endDate.Date);

        if (storeId.HasValue)
        {
            query = query.Where(t => t.StoreId == checkStoreId);
        }

        var txs = await query.ToListAsync(cancellationToken);

        var report = new Gstr3BReportDto();

        foreach (var t in txs)
        {
            if (t.TransactionType == "SALE")
            {
                report.OutwardTaxableValue += t.TaxableAmount;
                report.OutwardCgst += t.CgstAmount;
                report.OutwardSgst += t.SgstAmount;
                report.OutwardIgst += t.IgstAmount;
            }
            else if (t.TransactionType == "RETURN")
            {
                // Sales returns reverse outward liabilities
                report.OutwardTaxableValue -= t.TaxableAmount;
                report.OutwardCgst -= t.CgstAmount;
                report.OutwardSgst -= t.SgstAmount;
                report.OutwardIgst -= t.IgstAmount;
            }
            else if (t.TransactionType == "PURCHASE")
            {
                report.ItcTaxableValue += t.TaxableAmount;
                report.ItcCgst += t.CgstAmount;
                report.ItcSgst += t.SgstAmount;
                report.ItcIgst += t.IgstAmount;
            }
        }

        return report;
    }

    #endregion
}
