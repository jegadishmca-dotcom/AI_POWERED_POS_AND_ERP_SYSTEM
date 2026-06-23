using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Api.Helpers;
using PosErp.Application.Features.Finance.Services;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Manager,Owner")]
public class FinancialReportsController : ControllerBase
{
    private readonly IFinancialReportingService _reportingService;
    private readonly IApplicationDbContext _context;

    public FinancialReportsController(IFinancialReportingService reportingService, IApplicationDbContext context)
    {
        _reportingService = reportingService;
        _context = context;
    }

    private async Task<string> GetStoreCodeAsync(Guid? storeId)
    {
        if (!storeId.HasValue) return "HQ";
        var store = await _context.Stores.FirstOrDefaultAsync(s => s.Id == storeId.Value);
        return store?.StoreCode ?? "STORE";
    }

    private IActionResult ExportData<T>(List<T> data, string reportName, string storeCode, string dateRange, string format, string fileName)
    {
        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var csvBytes = ReportExportHelper.ExportToCsv(data);
            return File(csvBytes, "text/csv", $"{fileName}.csv");
        }
        if (format.Equals("excel", StringComparison.OrdinalIgnoreCase) || format.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
        {
            var excelBytes = ReportExportHelper.ExportToExcel(reportName, storeCode, dateRange, data);
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileName}.xlsx");
        }
        if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
        {
            var pdfBytes = ReportExportHelper.ExportToPdf(reportName, storeCode, dateRange, data);
            return File(pdfBytes, "application/pdf", $"{fileName}.pdf");
        }
        return BadRequest("Invalid export format. Supported formats: json, csv, excel, pdf");
    }

    [HttpGet("trial-balance")]
    public async Task<IActionResult> GetTrialBalance([FromQuery] Guid? storeId, [FromQuery] DateTime? asOfDate, [FromQuery] string format = "json")
    {
        var targetDate = asOfDate ?? DateTime.Today;
        var data = await _reportingService.GetTrialBalanceAsync(storeId, targetDate, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = targetDate.ToString("yyyyMMdd");
        var fileName = $"TrialBalance_{storeCode}_{dateStr}";

        return ExportData(data, "Trial Balance", storeCode, dateStr, format, fileName);
    }

    [HttpGet("profit-and-loss")]
    public async Task<IActionResult> GetProfitAndLoss([FromQuery] Guid? storeId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string format = "json")
    {
        var start = startDate ?? DateTime.Today.AddDays(-30);
        var end = endDate ?? DateTime.Today;
        var data = await _reportingService.GetProfitAndLossAsync(storeId, start, end, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = $"{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fileName = $"ProfitAndLoss_{storeCode}_{dateStr}";

        var flattened = FlattenProfitAndLoss(data);
        return ExportData(flattened, "Profit & Loss Statement", storeCode, dateStr, format, fileName);
    }

    [HttpGet("balance-sheet")]
    public async Task<IActionResult> GetBalanceSheet([FromQuery] Guid? storeId, [FromQuery] DateTime? asOfDate, [FromQuery] string format = "json")
    {
        var targetDate = asOfDate ?? DateTime.Today;
        var data = await _reportingService.GetBalanceSheetAsync(storeId, targetDate, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = targetDate.ToString("yyyyMMdd");
        var fileName = $"BalanceSheet_{storeCode}_{dateStr}";

        var flattened = FlattenBalanceSheet(data);
        return ExportData(flattened, "Balance Sheet", storeCode, dateStr, format, fileName);
    }

    [HttpGet("cash-flow")]
    public async Task<IActionResult> GetCashFlow([FromQuery] Guid? storeId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string format = "json")
    {
        var start = startDate ?? DateTime.Today.AddDays(-30);
        var end = endDate ?? DateTime.Today;
        var data = await _reportingService.GetCashFlowStatementAsync(storeId, start, end, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = $"{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fileName = $"CashFlow_{storeCode}_{dateStr}";

        var flattened = FlattenCashFlow(data);
        return ExportData(flattened, "Cash Flow Statement", storeCode, dateStr, format, fileName);
    }

    [HttpGet("general-ledger")]
    public async Task<IActionResult> GetGeneralLedger([FromQuery] Guid? storeId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string format = "json")
    {
        var start = startDate ?? DateTime.Today.AddDays(-30);
        var end = endDate ?? DateTime.Today;
        var data = await _reportingService.GetGeneralLedgerReportAsync(storeId, start, end, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = $"{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fileName = $"GeneralLedger_{storeCode}_{dateStr}";

        var flattened = FlattenGeneralLedger(data);
        return ExportData(flattened, "General Ledger Report", storeCode, dateStr, format, fileName);
    }

    [HttpGet("account-ledger")]
    public async Task<IActionResult> GetAccountLedger([FromQuery] Guid? storeId, [FromQuery] string accountCode, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string format = "json")
    {
        if (string.IsNullOrEmpty(accountCode))
        {
            return BadRequest("Account code is required.");
        }

        var start = startDate ?? DateTime.Today.AddDays(-30);
        var end = endDate ?? DateTime.Today;
        var data = await _reportingService.GetAccountLedgerReportAsync(storeId, accountCode, start, end, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = $"{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fileName = $"AccountLedger_{accountCode}_{storeCode}_{dateStr}";

        return ExportData(data.Lines, $"Account Ledger - {accountCode} ({data.AccountName})", storeCode, dateStr, format, fileName);
    }

    [HttpGet("daily-sales")]
    public async Task<IActionResult> GetDailySales([FromQuery] Guid? storeId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string format = "json")
    {
        var start = startDate ?? DateTime.Today.AddDays(-30);
        var end = endDate ?? DateTime.Today;
        var data = await _reportingService.GetDailySalesSummaryAsync(storeId, start, end, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = $"{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fileName = $"DailySales_{storeCode}_{dateStr}";

        return ExportData(data.Days, "Daily Sales Summary", storeCode, dateStr, format, fileName);
    }

    #region Flattening Helpers

    private List<AccountBalanceDto> FlattenProfitAndLoss(ProfitAndLossDto pl)
    {
        var list = new List<AccountBalanceDto>();
        list.Add(new AccountBalanceDto { AccountCode = "HEADER-REV", AccountName = "--- REVENUES ---" });
        list.AddRange(pl.RevenueAccounts);
        list.Add(new AccountBalanceDto { AccountCode = "TOTAL-REV", AccountName = "TOTAL REVENUE", CreditBalance = pl.TotalRevenue });

        list.Add(new AccountBalanceDto { AccountCode = "HEADER-COGS", AccountName = "--- COST OF GOODS SOLD ---" });
        list.Add(new AccountBalanceDto { AccountCode = "TOTAL-COGS", AccountName = "TOTAL COGS", DebitBalance = pl.TotalCOGS });

        list.Add(new AccountBalanceDto { AccountCode = "HEADER-EXP", AccountName = "--- OPERATING EXPENSES ---" });
        list.AddRange(pl.ExpenseAccounts);
        list.Add(new AccountBalanceDto { AccountCode = "TOTAL-EXP", AccountName = "TOTAL OPERATING EXPENSES", DebitBalance = pl.TotalOperatingExpenses });

        list.Add(new AccountBalanceDto { AccountCode = "SUMMARY-GP", AccountName = "GROSS PROFIT", CreditBalance = pl.GrossProfit >= 0 ? pl.GrossProfit : 0, DebitBalance = pl.GrossProfit < 0 ? -pl.GrossProfit : 0 });
        list.Add(new AccountBalanceDto { AccountCode = "SUMMARY-NP", AccountName = "NET PROFIT", CreditBalance = pl.NetProfit >= 0 ? pl.NetProfit : 0, DebitBalance = pl.NetProfit < 0 ? -pl.NetProfit : 0 });

        return list;
    }

    private List<AccountBalanceDto> FlattenBalanceSheet(BalanceSheetDto bs)
    {
        var list = new List<AccountBalanceDto>();
        list.Add(new AccountBalanceDto { AccountCode = "HEADER-AST", AccountName = "--- ASSETS ---" });
        list.AddRange(bs.AssetAccounts);
        list.Add(new AccountBalanceDto { AccountCode = "TOTAL-AST", AccountName = "TOTAL ASSETS", DebitBalance = bs.TotalAssets });

        list.Add(new AccountBalanceDto { AccountCode = "HEADER-LIAB", AccountName = "--- LIABILITIES ---" });
        list.AddRange(bs.LiabilityAccounts);
        list.Add(new AccountBalanceDto { AccountCode = "TOTAL-LIAB", AccountName = "TOTAL LIABILITIES", CreditBalance = bs.TotalLiabilities });

        list.Add(new AccountBalanceDto { AccountCode = "HEADER-EQ", AccountName = "--- EQUITY ---" });
        list.AddRange(bs.EquityAccounts);
        list.Add(new AccountBalanceDto { AccountCode = "TOTAL-EQ", AccountName = "TOTAL EQUITY", CreditBalance = bs.TotalEquity });

        return list;
    }

    public class CashFlowLineItem
    {
        public string LineName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    private List<CashFlowLineItem> FlattenCashFlow(CashFlowStatementDto cf)
    {
        return new List<CashFlowLineItem>
        {
            new() { LineName = "Beginning Cash Balance", Amount = cf.BeginningCashBalance },
            new() { LineName = "Operating Cash Inflows", Amount = cf.OperatingCashInflows },
            new() { LineName = "Operating Cash Outflows", Amount = cf.OperatingCashOutflows },
            new() { LineName = "Net Cash from Operating Activities", Amount = cf.NetCashFromOperatingActivities },
            new() { LineName = "Investing Cash Inflows", Amount = cf.InvestingCashInflows },
            new() { LineName = "Investing Cash Outflows", Amount = cf.InvestingCashOutflows },
            new() { LineName = "Net Cash from Investing Activities", Amount = cf.NetCashFromInvestingActivities },
            new() { LineName = "Financing Cash Inflows", Amount = cf.FinancingCashInflows },
            new() { LineName = "Financing Cash Outflows", Amount = cf.FinancingCashOutflows },
            new() { LineName = "Net Cash from Financing Activities", Amount = cf.NetCashFromFinancingActivities },
            new() { LineName = "Net Increase in Cash", Amount = cf.NetIncreaseInCash },
            new() { LineName = "Ending Cash Balance", Amount = cf.EndingCashBalance }
        };
    }

    public class FlatGeneralLedgerRow
    {
        public string EntryNumber { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public string EntryDescription { get; set; } = string.Empty;
        public string StoreCode { get; set; } = string.Empty;
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string LineDescription { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
    }

    private List<FlatGeneralLedgerRow> FlattenGeneralLedger(GeneralLedgerReportDto gl)
    {
        var list = new List<FlatGeneralLedgerRow>();
        foreach (var e in gl.Entries)
        {
            foreach (var l in e.Lines)
            {
                list.Add(new FlatGeneralLedgerRow
                {
                    EntryNumber = e.EntryNumber,
                    EntryDate = e.EntryDate,
                    EntryDescription = e.Description,
                    StoreCode = e.StoreCode,
                    AccountCode = l.AccountCode,
                    AccountName = l.AccountName,
                    LineDescription = l.Description,
                    DebitAmount = l.DebitAmount,
                    CreditAmount = l.CreditAmount
                });
            }
        }
        return list;
    }

    #endregion
}
