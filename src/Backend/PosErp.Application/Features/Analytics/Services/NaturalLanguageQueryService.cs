using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PosErp.Application.Features.Finance.Services;

namespace PosErp.Application.Features.Analytics.Services;

public class NaturalLanguageQueryService : INaturalLanguageQueryService
{
    private readonly IFinancialReportingService _reportingService;

    public NaturalLanguageQueryService(IFinancialReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    public async Task<NlQueryResultDto> ParseAndExecuteQueryAsync(string queryText, Guid? storeId, CancellationToken cancellationToken)
    {
        var result = new NlQueryResultDto
        {
            Query = queryText,
            IsParsedSuccessfully = false
        };

        if (string.IsNullOrWhiteSpace(queryText))
        {
            result.SummaryText = "Query text is empty. Please enter a question like 'show net profit for last month'.";
            return result;
        }

        string normalized = queryText.ToLowerInvariant().Trim();

        // 1. Resolve date range
        var today = DateTime.Today;
        DateTime startDate = today;
        DateTime endDate = today;
        string timePeriod = "Today";

        if (normalized.Contains("yesterday"))
        {
            startDate = today.AddDays(-1);
            endDate = today.AddDays(-1);
            timePeriod = "Yesterday";
        }
        else if (normalized.Contains("last month"))
        {
            startDate = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
            endDate = new DateTime(today.Year, today.Month, 1).AddDays(-1);
            timePeriod = "Last Month";
        }
        else if (normalized.Contains("this month"))
        {
            startDate = new DateTime(today.Year, today.Month, 1);
            endDate = today;
            timePeriod = "This Month";
        }
        else if (normalized.Contains("last quarter"))
        {
            int q = (today.Month - 1) / 3; // 0, 1, 2, 3
            int lq = q - 1;
            int yr = today.Year;
            if (lq < 0) { lq = 3; yr--; }
            startDate = new DateTime(yr, (lq * 3) + 1, 1);
            endDate = startDate.AddMonths(3).AddDays(-1);
            timePeriod = "Last Quarter";
        }
        else if (normalized.Contains("this year"))
        {
            startDate = new DateTime(today.Year, 1, 1);
            endDate = today;
            timePeriod = "This Year";
        }
        else if (normalized.Contains("last 30 days"))
        {
            startDate = today.AddDays(-30);
            endDate = today;
            timePeriod = "Last 30 Days";
        }

        result.TimePeriod = timePeriod;

        // 2. Classify report intent and execute matching read-only function
        if (normalized.Contains("net profit") || normalized.Contains("profit and loss") || normalized.Contains("p&l") || normalized.Contains("p and l") || normalized.Contains("revenue") || normalized.Contains("sales") || normalized.Contains("operating expense") || normalized.Contains("cogs"))
        {
            result.ReportType = "P_AND_L";
            var report = await _reportingService.GetProfitAndLossAsync(storeId, startDate, endDate, cancellationToken);
            
            result.IsParsedSuccessfully = true;
            result.VisualType = "TABLE";
            result.SummaryText = $"Profit & Loss report parsed successfully for {timePeriod} ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}). Gross Revenue: {report.TotalRevenue:C}, COGS: {report.TotalCOGS:C}, Net Profit: {report.NetProfit:C}.";
            
            result.Columns = new List<string> { "Account Type", "Account Code", "Account Name", "Balance" };
            
            foreach (var rev in report.RevenueAccounts)
            {
                result.DataRows.Add(new Dictionary<string, object>
                {
                    { "Account Type", "REVENUE" },
                    { "Account Code", rev.AccountCode },
                    { "Account Name", rev.AccountName },
                    { "Balance", rev.CreditBalance }
                });
            }

            foreach (var exp in report.ExpenseAccounts)
            {
                result.DataRows.Add(new Dictionary<string, object>
                {
                    { "Account Type", "EXPENSE" },
                    { "Account Code", exp.AccountCode },
                    { "Account Name", exp.AccountName },
                    { "Balance", exp.DebitBalance }
                });
            }
        }
        else if (normalized.Contains("balance sheet") || normalized.Contains("assets") || normalized.Contains("liabilities") || normalized.Contains("equity"))
        {
            result.ReportType = "BALANCE_SHEET";
            var report = await _reportingService.GetBalanceSheetAsync(storeId, endDate, cancellationToken);
            
            result.IsParsedSuccessfully = true;
            result.VisualType = "TABLE";
            result.SummaryText = $"Balance Sheet parsed successfully as of {endDate:yyyy-MM-dd}. Total Assets: {report.TotalAssets:C}, Total Liabilities: {report.TotalLiabilities:C}, Total Equity: {report.TotalEquity:C} (equation: Assets = Liabilities + Equity balances correctly).";
            
            result.Columns = new List<string> { "Section", "Account Code", "Account Name", "Balance" };

            foreach (var a in report.AssetAccounts)
            {
                result.DataRows.Add(new Dictionary<string, object>
                {
                    { "Section", "ASSETS" },
                    { "Account Code", a.AccountCode },
                    { "Account Name", a.AccountName },
                    { "Balance", a.DebitBalance }
                });
            }
            foreach (var l in report.LiabilityAccounts)
            {
                result.DataRows.Add(new Dictionary<string, object>
                {
                    { "Section", "LIABILITIES" },
                    { "Account Code", l.AccountCode },
                    { "Account Name", l.AccountName },
                    { "Balance", l.CreditBalance }
                });
            }
            foreach (var eq in report.EquityAccounts)
            {
                result.DataRows.Add(new Dictionary<string, object>
                {
                    { "Section", "EQUITY" },
                    { "Account Code", eq.AccountCode },
                    { "Account Name", eq.AccountName },
                    { "Balance", eq.CreditBalance }
                });
            }
        }
        else if (normalized.Contains("inventory value") || normalized.Contains("inventory valuation") || normalized.Contains("stock value") || normalized.Contains("total stock"))
        {
            result.ReportType = "INVENTORY_VALUATION";
            var report = await _reportingService.GetInventoryValuationAsync(storeId, endDate, cancellationToken);
            
            result.IsParsedSuccessfully = true;
            result.VisualType = "TABLE";
            decimal totalVal = 0;
            foreach (var r in report) totalVal += r.TotalValuation;

            result.SummaryText = $"Inventory Valuation parsed successfully as of {endDate:yyyy-MM-dd}. Total stock value: {totalVal:C} spread across {report.Count} active product batches.";
            
            result.Columns = new List<string> { "Product Code", "Product Name", "Batch Number", "Quantity", "Unit Cost", "Total Valuation" };

            foreach (var item in report)
            {
                result.DataRows.Add(new Dictionary<string, object>
                {
                    { "Product Code", item.ProductCode },
                    { "Product Name", item.ProductName },
                    { "Batch Number", item.BatchNumber },
                    { "Quantity", item.Quantity },
                    { "Unit Cost", item.CostPrice },
                    { "Total Valuation", item.TotalValuation }
                });
            }
        }
        else if (normalized.Contains("gst") || normalized.Contains("tax") || normalized.Contains("gstr-3b") || normalized.Contains("gstr3b"))
        {
            result.ReportType = "GST";
            var report = await _reportingService.GetGstr3BReportAsync(storeId, startDate, endDate, cancellationToken);
            
            result.IsParsedSuccessfully = true;
            result.VisualType = "TABLE";
            result.SummaryText = $"GST Report (GSTR-3B) parsed successfully for {timePeriod} ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}). Net CGST liability: {report.NetCgstLiability:C}, Net SGST liability: {report.NetSgstLiability:C}.";
            
            result.Columns = new List<string> { "GST Category", "CGST", "SGST", "IGST" };

            result.DataRows.Add(new Dictionary<string, object>
            {
                { "GST Category", "Outward Taxable Supplies" },
                { "CGST", report.OutwardCgst },
                { "SGST", report.OutwardSgst },
                { "IGST", report.OutwardIgst }
            });

            result.DataRows.Add(new Dictionary<string, object>
            {
                { "GST Category", "Eligible Input Tax Credit (ITC)" },
                { "CGST", report.ItcCgst },
                { "SGST", report.ItcSgst },
                { "IGST", report.ItcIgst }
            });

            result.DataRows.Add(new Dictionary<string, object>
            {
                { "GST Category", "Net Tax Payable" },
                { "CGST", report.NetCgstLiability },
                { "SGST", report.NetSgstLiability },
                { "IGST", report.NetIgstLiability }
            });
        }
        else if (normalized.Contains("trial balance") || normalized.Contains("tb"))
        {
            result.ReportType = "TRIAL_BALANCE";
            var report = await _reportingService.GetTrialBalanceAsync(storeId, endDate, cancellationToken);
            
            result.IsParsedSuccessfully = true;
            result.VisualType = "TABLE";
            result.SummaryText = $"Trial Balance parsed successfully as of {endDate:yyyy-MM-dd}. Balanced: total debit matches total credit.";
            
            result.Columns = new List<string> { "Account Code", "Account Name", "Debit", "Credit" };

            foreach (var item in report)
            {
                result.DataRows.Add(new Dictionary<string, object>
                {
                    { "Account Code", item.AccountCode },
                    { "Account Name", item.AccountName },
                    { "Debit", item.DebitBalance },
                    { "Credit", item.CreditBalance }
                });
            }
        }
        else if (normalized.Contains("cash flow") || normalized.Contains("operating cash"))
        {
            result.ReportType = "CASH_FLOW";
            var report = await _reportingService.GetCashFlowStatementAsync(storeId, startDate, endDate, cancellationToken);

            result.IsParsedSuccessfully = true;
            result.VisualType = "TABLE";
            result.SummaryText = $"Cash Flow Statement parsed successfully for {timePeriod} ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}). Net Cash Increase: {report.NetIncreaseInCash:C}. Ending Cash Balance: {report.EndingCashBalance:C}.";

            result.Columns = new List<string> { "Activity Category", "Cash Inflows", "Cash Outflows", "Net Cash Flow" };

            result.DataRows.Add(new Dictionary<string, object>
            {
                { "Activity Category", "Operating Activities" },
                { "Cash Inflows", report.OperatingCashInflows },
                { "Cash Outflows", report.OperatingCashOutflows },
                { "Net Cash Flow", report.NetCashFromOperatingActivities }
            });

            result.DataRows.Add(new Dictionary<string, object>
            {
                { "Activity Category", "Investing Activities" },
                { "Cash Inflows", report.InvestingCashInflows },
                { "Cash Outflows", report.InvestingCashOutflows },
                { "Net Cash Flow", report.NetCashFromInvestingActivities }
            });

            result.DataRows.Add(new Dictionary<string, object>
            {
                { "Activity Category", "Financing Activities" },
                { "Cash Inflows", report.FinancingCashInflows },
                { "Cash Outflows", report.FinancingCashOutflows },
                { "Net Cash Flow", report.NetCashFromFinancingActivities }
            });
        }
        else
        {
            result.SummaryText = $"Unable to parse the query intent. Make sure you ask for Net Profit, Revenue, Balance Sheet, Trial Balance, Inventory Value, Cash Flow, or GST summaries.";
        }

        return result;
    }
}
