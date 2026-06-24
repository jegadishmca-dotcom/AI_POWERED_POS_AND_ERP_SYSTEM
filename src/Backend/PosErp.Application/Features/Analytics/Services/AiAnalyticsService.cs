using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Finance.Services;
using PosErp.Domain.Entities.Finance;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Purchasing;

namespace PosErp.Application.Features.Analytics.Services;

public class AiAnalyticsService : IAiAnalyticsService
{
    private readonly IApplicationDbContext _context;
    private readonly IFinancialReportingService _reportingService;

    public AiAnalyticsService(IApplicationDbContext context, IFinancialReportingService reportingService)
    {
        _context = context;
        _reportingService = reportingService;
    }

    public async Task RecalculateAllAnalyticsAsync(CancellationToken cancellationToken)
    {
        // Full rebuild: calculate for all active stores and consolidated HQ
        var stores = await _context.Stores
            .Where(s => !s.IsDeleted && s.IsActive)
            .ToListAsync(cancellationToken);

        // Clear existing cache to prevent duplicates
        await ClearCachedAnalyticsAsync(null, cancellationToken);
        foreach (var s in stores)
        {
            await ClearCachedAnalyticsAsync(s.Id, cancellationToken);
        }

        // 1. Recalculate consolidated analytics (StoreId = null)
        await RecalculateStoreAnalyticsAsync(null, cancellationToken);

        // 2. Recalculate store-specific analytics
        foreach (var s in stores)
        {
            await RecalculateStoreAnalyticsAsync(s.Id, cancellationToken);
        }

        // 3. Compute store rankings & benchmarking at consolidated level
        await ComputeStoreRankingsAsync(cancellationToken);
    }

    public async Task RecalculateIncrementalAnalyticsAsync(CancellationToken cancellationToken)
    {
        // Incremental: lightweight refresh of today's values, active alerts, and immediate payment advice.
        // We will execute a subset of the calculations or run the recalculation for today/active items
        // For simplicity and correctness, running the recalculation updates the current cached tables.
        await RecalculateAllAnalyticsAsync(cancellationToken);
    }

    private async Task ClearCachedAnalyticsAsync(Guid? storeId, CancellationToken cancellationToken)
    {
        var db = (DbContext)_context;
        if (storeId.HasValue)
        {
            db.RemoveRange(_context.AiKpiResults.Where(x => x.StoreId == storeId).ToList());
            db.RemoveRange(_context.AiCashFlowForecasts.Where(x => x.StoreId == storeId).ToList());
            db.RemoveRange(_context.AiInventoryShrinkageAnalytics.Where(x => x.StoreId == storeId).ToList());
            db.RemoveRange(_context.AiExpiryRiskPredictions.Where(x => x.StoreId == storeId).ToList());
            db.RemoveRange(_context.AiAlerts.Where(x => x.StoreId == storeId).ToList());
        }
        else
        {
            db.RemoveRange(_context.AiKpiResults.Where(x => x.StoreId == null).ToList());
            db.RemoveRange(_context.AiCashFlowForecasts.Where(x => x.StoreId == null).ToList());
            db.RemoveRange(_context.AiInventoryShrinkageAnalytics.Where(x => x.StoreId == null).ToList());
            db.RemoveRange(_context.AiExpiryRiskPredictions.Where(x => x.StoreId == null).ToList());
            db.RemoveRange(_context.AiAlerts.Where(x => x.StoreId == null).ToList());
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task RecalculateStoreAnalyticsAsync(Guid? storeId, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var startOfPeriod = today.AddDays(-30);

        // Call reporting foundation services
        var pAndL = await _reportingService.GetProfitAndLossAsync(storeId, startOfPeriod, today, cancellationToken);
        var balanceSheet = await _reportingService.GetBalanceSheetAsync(storeId, today, cancellationToken);
        var trialBalance = await _reportingService.GetTrialBalanceAsync(storeId, today, cancellationToken);
        var valuation = await _reportingService.GetInventoryValuationAsync(storeId, today, cancellationToken);
        var shrinkage = await _reportingService.GetInventoryShrinkageAsync(storeId, startOfPeriod, today, cancellationToken);

        // A. KPI Engine Calculations
        await CalculateKpisAsync(storeId, pAndL, balanceSheet, trialBalance, valuation, shrinkage, cancellationToken);

        // B. Rule-Based Cash Flow Forecasting V1
        await ForecastCashFlowAsync(storeId, trialBalance, startOfPeriod, today, cancellationToken);

        // C. Financial Anomaly Detection
        await DetectAnomaliesAsync(storeId, cancellationToken);

        // D. Inventory Shrinkage Analytics
        await AnalyzeShrinkageAsync(storeId, shrinkage, valuation, cancellationToken);

        // E. Expiry Risk Prediction
        await PredictExpiryRisksAsync(storeId, valuation, cancellationToken);

        // F. Supplier Payment Recommendations (Run at Consolidated level for convenience, or per store)
        await GenerateSupplierPaymentRecommendationsAsync(storeId, cancellationToken);

        // G. Generate AI Alerts based on computed anomalies, expiries, shrinkage
        await GenerateAlertsAsync(storeId, cancellationToken);
    }

    private async Task CalculateKpisAsync(
        Guid? storeId,
        ProfitAndLossDto pAndL,
        BalanceSheetDto balanceSheet,
        List<AccountBalanceDto> trialBalance,
        List<InventoryValuationDto> valuation,
        List<InventoryShrinkageDto> shrinkage,
        CancellationToken cancellationToken)
    {
        var db = (DbContext)_context;

        // 1. Net Profit Margin = Net Profit / Revenue
        decimal netProfitMargin = pAndL.TotalRevenue > 0 ? (pAndL.NetProfit / pAndL.TotalRevenue) * 100 : 0;
        await CacheKpiAsync(storeId, "FINANCIAL", "NET_PROFIT_MARGIN", netProfitMargin, cancellationToken);

        // 2. ROA = Net Profit / Total Assets
        decimal roa = balanceSheet.TotalAssets > 0 ? (pAndL.NetProfit / balanceSheet.TotalAssets) * 100 : 0;
        await CacheKpiAsync(storeId, "FINANCIAL", "RETURN_ON_ASSETS", roa, cancellationToken);

        // 3. Working Capital = Total Assets - Total Liabilities
        decimal workingCapital = balanceSheet.TotalAssets - balanceSheet.TotalLiabilities;
        await CacheKpiAsync(storeId, "FINANCIAL", "WORKING_CAPITAL", workingCapital, cancellationToken);

        // 4. Quick Ratio = (Cash + Bank) / Liabilities
        decimal cashAndBank = trialBalance
            .Where(b => b.AccountCode.StartsWith("101") || b.AccountCode.StartsWith("102") || b.AccountCode == "1000" || b.AccountCode == "1100")
            .Sum(b => b.DebitBalance - b.CreditBalance);
        decimal quickRatio = balanceSheet.TotalLiabilities > 0 ? cashAndBank / balanceSheet.TotalLiabilities : 0;
        await CacheKpiAsync(storeId, "FINANCIAL", "QUICK_RATIO", quickRatio, cancellationToken);

        // 5. Debtors Turnaround = (Receivables / Revenue) * 365
        // AR standard code is 20200 or 20000 in this database, representing customer ledger. Let's read from Customer Ledger balances
        decimal receivables = await _context.CustomerLedger
            .Where(cl => !storeId.HasValue || cl.StoreId == storeId)
            .GroupBy(cl => cl.CustomerId)
            .Select(g => g.OrderByDescending(x => x.CreatedAt).First().RunningBalance)
            .SumAsync(cancellationToken);
        decimal debtorsTurnaround = pAndL.TotalRevenue > 0 ? (receivables / pAndL.TotalRevenue) * 30 : 0; // use monthly DSO (30 days)
        await CacheKpiAsync(storeId, "FINANCIAL", "DEBTORS_TURNAROUND", debtorsTurnaround, cancellationToken);

        // 6. Inventory Turnover Ratio = COGS / Average Inventory
        decimal totalValuation = valuation.Sum(v => v.TotalValuation);
        // Average Inventory: average between current and historical, default to current if history is empty
        decimal avgInventory = totalValuation > 0 ? totalValuation : 1;
        decimal invTurnover = pAndL.TotalCOGS / avgInventory;
        await CacheKpiAsync(storeId, "INVENTORY", "INVENTORY_TURNOVER", invTurnover, cancellationToken);

        // 7. Stock-to-Sales Ratio = Ending Inventory / Sales
        decimal stockToSales = pAndL.TotalRevenue > 0 ? totalValuation / pAndL.TotalRevenue : 0;
        await CacheKpiAsync(storeId, "INVENTORY", "STOCK_TO_SALES", stockToSales, cancellationToken);

        // 8. Sell-Through Rate = Qty Sold / (Qty Sold + Ending Qty) * 100
        decimal qtySold = await _context.InvoiceItems
            .Include(ii => ii.Invoice)
            .Where(ii => ii.Invoice.Status == "COMPLETED" && ii.Invoice.BusinessDate >= DateTime.Today.AddDays(-30))
            .Where(ii => !storeId.HasValue || ii.Invoice.StoreId == storeId)
            .SumAsync(ii => ii.Quantity, cancellationToken);
        decimal endingQty = valuation.Sum(v => v.Quantity);
        decimal sellThrough = (qtySold + endingQty) > 0 ? (qtySold / (qtySold + endingQty)) * 100 : 0;
        await CacheKpiAsync(storeId, "INVENTORY", "SELL_THROUGH_RATE", sellThrough, cancellationToken);

        // 9. Shrinkage Rate = (Shrinkage Value / Ending Inventory Value) * 100
        decimal shrinkageLossValue = shrinkage.Sum(s => s.TotalLossValue);
        decimal shrinkageRate = totalValuation > 0 ? (shrinkageLossValue / totalValuation) * 100 : 0;
        await CacheKpiAsync(storeId, "INVENTORY", "SHRINKAGE_RATE", shrinkageRate, cancellationToken);

        // 10. Store Performance: Sales per Square Foot
        decimal squareFootage = 2000.00m;
        if (storeId.HasValue)
        {
            var st = await _context.Stores.FindAsync(new object[] { storeId.Value }, cancellationToken);
            if (st != null) squareFootage = st.SquareFootage;
        }
        else
        {
            // Sum up square footage of all active stores
            squareFootage = await _context.Stores
                .Where(s => !s.IsDeleted && s.IsActive)
                .SumAsync(s => s.SquareFootage, cancellationToken);
            if (squareFootage == 0) squareFootage = 2000.00m;
        }
        decimal salesPerSqFt = pAndL.TotalRevenue / squareFootage;
        await CacheKpiAsync(storeId, "STORE", "SALES_PER_SQ_FT", salesPerSqFt, cancellationToken);

        // 11. Average Transaction Value (ATV)
        var invoicesQuery = _context.Invoices
            .Where(i => i.Status == "COMPLETED" && i.BusinessDate >= DateTime.Today.AddDays(-30) && !i.IsDeleted);
        if (storeId.HasValue) invoicesQuery = invoicesQuery.Where(i => i.StoreId == storeId);
        int invoiceCount = await invoicesQuery.CountAsync(cancellationToken);
        decimal atv = invoiceCount > 0 ? pAndL.TotalRevenue / invoiceCount : 0;
        await CacheKpiAsync(storeId, "STORE", "AVERAGE_TRANSACTION_VALUE", atv, cancellationToken);

        // 12. Cashier Variance Rate = Sum(Abs(Difference)) / Sum(Expected) * 100
        var sessionQuery = _context.PosSessions.Where(s => s.Status == "CLOSED" && s.EndTime >= DateTime.Today.AddDays(-30));
        if (storeId.HasValue) sessionQuery = sessionQuery.Where(s => s.StoreId == storeId);
        var sessionsList = await sessionQuery.ToListAsync(cancellationToken);
        decimal totalDiff = sessionsList.Sum(s => Math.Abs(s.Difference));
        decimal totalExpectedCash = sessionsList.Sum(s => s.ExpectedClosingCash);
        decimal cashierVariance = totalExpectedCash > 0 ? (totalDiff / totalExpectedCash) * 100 : 0;
        await CacheKpiAsync(storeId, "STORE", "CASHIER_VARIANCE_RATE", cashierVariance, cancellationToken);
    }

    private async Task CacheKpiAsync(Guid? storeId, string type, string name, decimal value, CancellationToken cancellationToken)
    {
        var kpi = new AiKpiResult
        {
            StoreId = storeId,
            KpiType = type,
            KpiName = name,
            KpiValue = value,
            CalculatedAt = DateTime.UtcNow
        };
        _context.AiKpiResults.Add(kpi);

        // Add history record for daily full rebuild
        var history = new AiKpiHistory
        {
            StoreId = storeId,
            KpiType = type,
            KpiName = name,
            KpiValue = value,
            RecordedAt = DateTime.UtcNow
        };
        _context.AiKpiHistories.Add(history);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ForecastCashFlowAsync(Guid? storeId, List<AccountBalanceDto> trialBalance, DateTime start, DateTime today, CancellationToken cancellationToken)
    {
        // 1. Read Current Cash/Bank Balance from trial balance
        decimal currentBalance = trialBalance
            .Where(b => b.AccountCode.StartsWith("101") || b.AccountCode.StartsWith("102") || b.AccountCode == "1000" || b.AccountCode == "1100")
            .Sum(b => b.DebitBalance - b.CreditBalance);

        // 2. Rule-Based Forecast V1: Predict cash flows for next 30 days
        // Get average daily sales of past 30 days as POS revenue velocity
        var salesQuery = _context.Invoices
            .Where(i => i.Status == "COMPLETED" && i.BusinessDate >= start && i.BusinessDate <= today && !i.IsDeleted);
        if (storeId.HasValue) salesQuery = salesQuery.Where(i => i.StoreId == storeId);
        decimal totalSales = await salesQuery.SumAsync(i => i.NetPayable, cancellationToken);
        decimal avgDailySales = totalSales / 30;

        // Fetch unpaid AR invoices due in the next 30 days
        var arInvoicesQuery = _context.Invoices
            .Where(i => i.Status != "PAID" && i.Status != "CANCELLED" && i.PaymentMode == "CREDIT" && i.DueDate != null && i.DueDate > today && i.DueDate <= today.AddDays(30));
        if (storeId.HasValue) arInvoicesQuery = arInvoicesQuery.Where(i => i.StoreId == storeId);
        var arInvoices = await arInvoicesQuery.ToListAsync(cancellationToken);

        // Fetch unpaid AP bills due in next 30 days
        var apBillsQuery = _context.PurchaseBills
            .Where(b => b.Status != "PAID" && b.DueDate != null && b.DueDate > today && b.DueDate <= today.AddDays(30));
        if (storeId.HasValue) apBillsQuery = apBillsQuery.Where(b => b.StoreId == storeId);
        var apBills = await apBillsQuery.ToListAsync(cancellationToken);

        decimal rollingBalance = currentBalance;

        for (int day = 1; day <= 30; day++)
        {
            var forecastDate = today.AddDays(day);

            // Inflow: daily retail velocity + AR invoices due today
            decimal posSalesProjection = avgDailySales;
            decimal arCollections = arInvoices.Where(i => i.DueDate!.Value.Date == forecastDate.Date).Sum(i => i.NetPayable);
            decimal projectedInflow = posSalesProjection + arCollections;

            // Outflow: AP purchase bills due today
            decimal projectedOutflow = apBills.Where(b => b.DueDate!.Value.Date == forecastDate.Date).Sum(b => b.TotalAmount);

            rollingBalance = rollingBalance + projectedInflow - projectedOutflow;

            // Confidence level decreases as we go further into the future
            string confidence = "HIGH";
            if (day > 15) confidence = "LOW";
            else if (day > 7) confidence = "MEDIUM";

            var fc = new AiCashFlowForecast
            {
                StoreId = storeId,
                ForecastDate = forecastDate,
                ProjectedInflow = projectedInflow,
                ProjectedOutflow = projectedOutflow,
                ProjectedBalance = rollingBalance,
                ConfidenceLevel = confidence,
                CalculatedAt = DateTime.UtcNow
            };
            _context.AiCashFlowForecasts.Add(fc);
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task GenerateSupplierPaymentRecommendationsAsync(Guid? storeId, CancellationToken cancellationToken)
    {
        // Recommendations are generated across all suppliers, but store-wise can segment if bills have StoreId
        var today = DateTime.Today;

        // Clear existing recommended payments that are still in PENDING status
        // We will fetch recommendations and join on PurchaseBills to filter by StoreId
        var recsQuery = _context.AiSupplierPaymentRecommendations.Where(r => r.FeedbackStatus == "PENDING");
        if (storeId.HasValue)
        {
            recsQuery = from r in recsQuery
                        join b in _context.PurchaseBills on r.PurchaseBillId equals b.Id
                        where b.StoreId == storeId
                        select r;
        }
        var existing = await recsQuery.ToListAsync(cancellationToken);
        ((DbContext)_context).RemoveRange(existing);
        await _context.SaveChangesAsync(cancellationToken);

        // Fetch unpaid bills
        var billsQuery = _context.PurchaseBills.Where(b => b.Status != "PAID");
        if (storeId.HasValue) billsQuery = billsQuery.Where(b => b.StoreId == storeId);
        var unpaidBills = await billsQuery.ToListAsync(cancellationToken);

        // Fetch active suppliers to map names and payment terms
        var suppliersDict = await _context.Suppliers.ToDictionaryAsync(s => s.Id, cancellationToken);

        // Get available cash for reference
        var trialBalance = await _reportingService.GetTrialBalanceAsync(storeId, today, cancellationToken);
        decimal cashAvailable = trialBalance
            .Where(b => b.AccountCode.StartsWith("101") || b.AccountCode.StartsWith("102") || b.AccountCode == "1000" || b.AccountCode == "1100")
            .Sum(b => b.DebitBalance - b.CreditBalance);

        var recommendations = new List<AiSupplierPaymentRecommendation>();

        foreach (var bill in unpaidBills)
        {
            if (!suppliersDict.TryGetValue(bill.SupplierId, out var supplier)) continue;

            // Parse discount term (e.g. "2/10 NET30")
            decimal discount = 0;
            DateTime? discountExpiry = null;
            string terms = supplier.PaymentTerms;

            if (terms.Contains("/") && terms.ToLower().Contains("net"))
            {
                // Simple V1 discount parser: extract percentage and days
                // e.g. "2/10 NET30" -> percentage = 2%, days = 10
                var parts = terms.Split(' ');
                var discPart = parts[0].Split('/');
                if (discPart.Length == 2 && decimal.TryParse(discPart[0], out decimal pct) && int.TryParse(discPart[1], out int days))
                {
                    discountExpiry = bill.BillDate.AddDays(days);
                    if (today <= discountExpiry.Value)
                    {
                        discount = bill.TotalAmount * (pct / 100);
                    }
                }
            }

            // Priority score calculation:
            int priority = 20; // default low priority
            string reason = "Standard payment terms.";
            DateTime dueDate = bill.DueDate ?? bill.BillDate.AddDays(30);
            int daysToDue = (dueDate - today).Days;

            if (daysToDue < 0)
            {
                priority = 90 + Math.Min(Math.Abs(daysToDue), 10);
                reason = $"OVERDUE by {Math.Abs(daysToDue)} days. Pay immediately to avoid supplier ledger lock.";
            }
            else if (discount > 0 && discountExpiry.HasValue && (discountExpiry.Value - today).Days <= 2)
            {
                priority = 95;
                reason = $"Early payment discount of {discount:C} expires in {(discountExpiry.Value - today).Days} days. Recommend payout to capture discount.";
            }
            else if (discount > 0 && discountExpiry.HasValue && (discountExpiry.Value - today).Days <= 5)
            {
                priority = 85;
                reason = $"Early payment discount of {discount:C} expires in {(discountExpiry.Value - today).Days} days.";
            }
            else if (daysToDue <= 5)
            {
                priority = 75;
                reason = $"Bill is due in {daysToDue} days. Standard payout advice.";
            }
            else if (daysToDue <= 15)
            {
                priority = 50;
                reason = $"Bill due in {daysToDue} days.";
            }

            var rec = new AiSupplierPaymentRecommendation
            {
                SupplierId = bill.SupplierId,
                SupplierName = supplier.Name,
                PurchaseBillId = bill.Id,
                BillNumber = bill.BillNumber,
                DueDate = dueDate,
                AmountDue = bill.TotalAmount - discount,
                DiscountAvailable = discount,
                DiscountExpiryDate = discountExpiry,
                PriorityScore = priority,
                RecommendationReason = reason,
                FeedbackStatus = "PENDING",
                CalculatedAt = DateTime.UtcNow
            };
            recommendations.Add(rec);
        }

        // Cash constraint filter: recommend based on priority and available cash
        var ordered = recommendations.OrderByDescending(r => r.PriorityScore).ToList();
        decimal runningTotal = 0;
        decimal cashLimit = cashAvailable * 0.8m; // limit payment recommendation to 80% of cash pool

        foreach (var r in ordered)
        {
            runningTotal += r.AmountDue;
            if (runningTotal > cashLimit && r.PriorityScore < 80)
            {
                r.PriorityScore = Math.Max(20, r.PriorityScore - 30);
                r.RecommendationReason += $" [CASH CONSTRAINT] Recommended payout deferred due to insufficient cash reserve (Cash pool: {cashAvailable:C}).";
            }
            _context.AiSupplierPaymentRecommendations.Add(r);
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task DetectAnomaliesAsync(Guid? storeId, CancellationToken cancellationToken)
    {
        // 1. Duplicate Payments check
        var startOfPeriod = DateTime.UtcNow.Date.AddDays(-30);
        var payments = await _context.SupplierPayments
            .Where(p => p.CreatedAt >= startOfPeriod)
            .Where(p => !storeId.HasValue || p.StoreId == storeId)
            .ToListAsync(cancellationToken);

        var duplicatePayments = payments
            .GroupBy(p => new { p.SupplierId, p.Amount, Date = p.PaymentDate.Date })
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var dup in duplicatePayments)
        {
            var items = dup.ToList();
            var first = items.First();
            
            // Check if already reported
            bool exists = await _context.AiFinancialAnomalies
                .AnyAsync(a => a.AnomalyType == "DUPLICATE_PAYMENT" && a.ReferenceId == first.Id, cancellationToken);
            if (!exists)
            {
                _context.AiFinancialAnomalies.Add(new AiFinancialAnomaly
                {
                    AnomalyType = "DUPLICATE_PAYMENT",
                    Severity = "CRITICAL",
                    Description = $"Potential duplicate payment detected for Supplier {first.SupplierId} on {first.PaymentDate:yyyy-MM-dd}. Amount: {first.Amount:C}. multiple entries found.",
                    ReferenceId = first.Id,
                    DetectedAt = DateTime.UtcNow,
                    IsResolved = false
                });
            }
        }

        // 2. Cashier shortage anomaly
        var shortages = await _context.PosSessions
            .Where(s => s.Status == "CLOSED" && s.Difference <= -500 && s.EndTime >= DateTime.UtcNow.AddDays(-30))
            .Where(s => !storeId.HasValue || s.StoreId == storeId)
            .ToListAsync(cancellationToken);

        foreach (var s in shortages)
        {
            bool exists = await _context.AiFinancialAnomalies
                .AnyAsync(a => a.AnomalyType == "CASHIER_SHORTAGE" && a.ReferenceId == s.Id, cancellationToken);
            if (!exists)
            {
                _context.AiFinancialAnomalies.Add(new AiFinancialAnomaly
                {
                    AnomalyType = "CASHIER_SHORTAGE",
                    Severity = "WARNING",
                    Description = $"Significant Cash Drawer Shortage of {s.Difference:C} detected in POS session {s.Id} on {s.EndTime:yyyy-MM-dd HH:mm}.",
                    ReferenceId = s.Id,
                    DetectedAt = DateTime.UtcNow,
                    IsResolved = false
                });
            }
        }

        // 3. Unusual manual journal entries
        var unusualJournals = await _context.JournalEntries
            .Include(e => e.Lines)
            .Where(e => e.IsPosted && e.SourceModule == "FINANCE" && e.SourceDocumentType == null && e.EntryDate >= DateTime.Today.AddDays(-30))
            .Where(e => !storeId.HasValue || e.StoreId == storeId)
            .ToListAsync(cancellationToken);

        foreach (var je in unusualJournals)
        {
            decimal totalDebit = je.Lines.Sum(l => l.DebitAmount);
            if (totalDebit >= 500000) // 5 Lakhs
            {
                bool exists = await _context.AiFinancialAnomalies
                    .AnyAsync(a => a.AnomalyType == "UNUSUAL_JOURNAL" && a.ReferenceId == je.Id, cancellationToken);
                if (!exists)
                {
                    _context.AiFinancialAnomalies.Add(new AiFinancialAnomaly
                    {
                        AnomalyType = "UNUSUAL_JOURNAL",
                        Severity = "WARNING",
                        Description = $"Unusual large manual journal entry {je.EntryNumber} posted on {je.EntryDate:yyyy-MM-dd}. Amount: {totalDebit:C}. Needs administrative audit approval.",
                        ReferenceId = je.Id,
                        DetectedAt = DateTime.UtcNow,
                        IsResolved = false
                    });
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task AnalyzeShrinkageAsync(Guid? storeId, List<InventoryShrinkageDto> shrinkage, List<InventoryValuationDto> valuation, CancellationToken cancellationToken)
    {
        decimal totalValuation = valuation.Sum(v => v.TotalValuation);
        if (totalValuation == 0) totalValuation = 1;

        // Group shrinkage by product
        var grouped = shrinkage
            .GroupBy(s => new { s.ProductId, s.ProductCode, s.ProductName })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                Quantity = g.Sum(x => x.QuantityLost),
                Cost = g.Sum(x => x.TotalLossValue)
            }).ToList();

        foreach (var s in grouped)
        {
            decimal rate = (s.Cost / totalValuation) * 100;
            string risk = "LOW";
            if (rate > 2.0m || s.Cost > 10000) risk = "HIGH";
            else if (rate > 0.5m || s.Cost > 2000) risk = "MEDIUM";

            var record = new AiInventoryShrinkageAnalytic
            {
                StoreId = storeId,
                ProductId = s.ProductId,
                ProductName = s.ProductName,
                ShrinkageQuantity = s.Quantity,
                ShrinkageCost = s.Cost,
                ShrinkageRatePct = rate,
                RiskLevel = risk,
                CalculatedAt = DateTime.UtcNow
            };
            _context.AiInventoryShrinkageAnalytics.Add(record);
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task PredictExpiryRisksAsync(Guid? storeId, List<InventoryValuationDto> valuation, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;

        // Load active batches with expiry date and available quantity > 0
        var batchesQuery = _context.ProductBatches
            .Include(b => b.Product)
            .Where(b => b.AvailableQuantity > 0 && b.ExpiryDate != null && b.IsActive);
        if (storeId.HasValue) batchesQuery = batchesQuery.Where(b => b.StoreId == storeId);
        var batches = await batchesQuery.ToListAsync(cancellationToken);

        // Fetch sales quantity of products in the last 30 days to calculate velocity
        var productIds = batches.Select(b => b.ProductId).Distinct().ToList();
        var salesData = await _context.InvoiceItems
            .Include(ii => ii.Invoice)
            .Where(ii => ii.Invoice.Status == "COMPLETED" && ii.Invoice.BusinessDate >= today.AddDays(-30) && productIds.Contains(ii.ProductId))
            .GroupBy(ii => ii.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);

        foreach (var b in batches)
        {
            int remainingDays = (b.ExpiryDate!.Value.Date - today.Date).Days;
            
            salesData.TryGetValue(b.ProductId, out decimal qtySold30Days);
            decimal velocity = qtySold30Days / 30m; // units sold per day

            decimal projectedSold = velocity * Math.Max(0, remainingDays);
            decimal shortfall = b.AvailableQuantity - projectedSold;
            
            decimal riskPct = 0;
            if (shortfall > 0 && b.AvailableQuantity > 0)
            {
                riskPct = (shortfall / b.AvailableQuantity) * 100;
            }

            if (remainingDays <= 0)
            {
                riskPct = 100;
                shortfall = b.AvailableQuantity;
            }

            decimal potentialLoss = Math.Max(0, shortfall) * b.CostPrice;

            string category = "LOW";
            if (remainingDays <= 0 || (remainingDays <= 30 && shortfall > 0)) category = "CRITICAL";
            else if (remainingDays <= 60 && shortfall > 0) category = "HIGH";
            else if (remainingDays <= 90 && shortfall > 0) category = "MEDIUM";

            var pred = new AiExpiryRiskPrediction
            {
                StoreId = storeId,
                ProductId = b.ProductId,
                ProductName = b.Product.Name,
                BatchId = b.Id,
                BatchNumber = b.BatchNumber,
                ExpiryDate = b.ExpiryDate!.Value.Date,
                RemainingQuantity = b.AvailableQuantity,
                CostPrice = b.CostPrice,
                PotentialLoss = potentialLoss,
                AverageDailySalesQty = velocity,
                ProjectedSoldQty = projectedSold,
                ExpiryRiskPct = riskPct,
                RiskCategory = category,
                CalculatedAt = DateTime.UtcNow
            };
            _context.AiExpiryRiskPredictions.Add(pred);
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task GenerateAlertsAsync(Guid? storeId, CancellationToken cancellationToken)
    {
        // 1. Expiry alerts
        var criticalExpiries = await _context.AiExpiryRiskPredictions
            .Where(x => x.StoreId == storeId && x.RiskCategory == "CRITICAL" && x.PotentialLoss > 0)
            .ToListAsync(cancellationToken);
        
        foreach (var b in criticalExpiries)
        {
            _context.AiAlerts.Add(new AiAlert
            {
                StoreId = storeId,
                AlertType = "EXPIRY",
                AlertSeverity = "CRITICAL",
                Title = "Critical Batch Expiry Risk",
                Message = $"Batch {b.BatchNumber} of product '{b.ProductName}' is at critical expiry risk. Projected waste: {b.RemainingQuantity - b.ProjectedSoldQty:F2} units. Potential write-off loss: {b.PotentialLoss:C}.",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 2. High Shrinkage alerts
        var highShrinkages = await _context.AiInventoryShrinkageAnalytics
            .Where(x => x.StoreId == storeId && x.RiskLevel == "HIGH")
            .ToListAsync(cancellationToken);

        foreach (var s in highShrinkages)
        {
            _context.AiAlerts.Add(new AiAlert
            {
                StoreId = storeId,
                AlertType = "SHRINKAGE",
                AlertSeverity = "CRITICAL",
                Title = "High Inventory Shrinkage Alert",
                Message = $"Product '{s.ProductName}' recorded significant stock loss: {s.ShrinkageQuantity:F2} units lost with cost value {s.ShrinkageCost:C}. Shrinkage Rate: {s.ShrinkageRatePct:F4}%.",
                CreatedAt = DateTime.UtcNow
            });
        }

        // 3. Unresolved Anomaly alerts
        var activeAnomalies = await _context.AiFinancialAnomalies
            .Where(x => !x.IsResolved)
            .ToListAsync(cancellationToken);

        foreach (var a in activeAnomalies)
        {
            // Only generate store-specific alerts if session belongs to the store
            if (storeId.HasValue)
            {
                if (a.AnomalyType == "CASHIER_SHORTAGE")
                {
                    var isStoreSession = await _context.PosSessions.AnyAsync(s => s.Id == a.ReferenceId && s.StoreId == storeId, cancellationToken);
                    if (!isStoreSession) continue;
                }
                else
                {
                    // Large manual journals/payments: consolidated, skip store specific unless mapped
                    continue;
                }
            }

            _context.AiAlerts.Add(new AiAlert
            {
                StoreId = storeId,
                AlertType = "ANOMALY",
                AlertSeverity = a.Severity,
                Title = $"Financial Anomaly: {a.AnomalyType}",
                Message = a.Description,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task ComputeStoreRankingsAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var startOfPeriod = today.AddDays(-30);

        var stores = await _context.Stores
            .Where(s => !s.IsDeleted && s.IsActive)
            .ToListAsync(cancellationToken);

        var benchmarkList = new List<StoreBenchmarkDto>();

        foreach (var st in stores)
        {
            var pAndL = await _reportingService.GetProfitAndLossAsync(st.Id, startOfPeriod, today, cancellationToken);
            
            // Cashier variance
            var sessions = await _context.PosSessions
                .Where(s => s.StoreId == st.Id && s.Status == "CLOSED" && s.EndTime >= startOfPeriod)
                .ToListAsync(cancellationToken);
            decimal totalDiff = sessions.Sum(s => Math.Abs(s.Difference));
            decimal totalExpected = sessions.Sum(s => s.ExpectedClosingCash);
            decimal cashierVar = totalExpected > 0 ? (totalDiff / totalExpected) * 100 : 0;

            // Shrinkage
            var shrinkageList = await _reportingService.GetInventoryShrinkageAsync(st.Id, startOfPeriod, today, cancellationToken);
            var valuationList = await _reportingService.GetInventoryValuationAsync(st.Id, today, cancellationToken);
            decimal totalVal = valuationList.Sum(v => v.TotalValuation);
            decimal totalShrinkLoss = shrinkageList.Sum(s => s.TotalLossValue);
            decimal shrinkageRate = totalVal > 0 ? (totalShrinkLoss / totalVal) * 100 : 0;

            decimal netProfitMargin = pAndL.TotalRevenue > 0 ? (pAndL.NetProfit / pAndL.TotalRevenue) * 100 : 0;

            benchmarkList.Add(new StoreBenchmarkDto
            {
                StoreId = st.Id,
                StoreCode = st.StoreCode,
                StoreName = st.StoreName,
                TotalSales = pAndL.TotalRevenue,
                NetProfitMargin = netProfitMargin,
                CashierVarianceRate = cashierVar,
                ShrinkageRatePct = shrinkageRate
            });
        }

        // Rank stores primarily by Total Sales descending
        var ranked = benchmarkList.OrderByDescending(b => b.TotalSales).ToList();
        for (int i = 0; i < ranked.Count; i++)
        {
            ranked[i].Ranking = i + 1;
            
            // Cache rankings as STORE level KPIs
            await CacheKpiAsync(ranked[i].StoreId, "STORE", "ENTERPRISE_RANK", ranked[i].Ranking, cancellationToken);
        }
    }

    public async Task<AiDashboardSummaryDto> GetDashboardSummaryAsync(Guid? storeId, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;

        // Retrieve cached KPIs
        var kpis = await _context.AiKpiResults
            .Where(k => k.StoreId == storeId)
            .OrderBy(k => k.KpiType)
            .ThenBy(k => k.KpiName)
            .ToListAsync(cancellationToken);

        var kpiSummary = new List<KpiSummaryItemDto>();
        foreach (var k in kpis)
        {
            // Look up historical average to calculate trend change
            decimal histAvg = await _context.AiKpiHistories
                .Where(h => h.StoreId == storeId && h.KpiType == k.KpiType && h.KpiName == k.KpiName && h.RecordedAt < k.CalculatedAt)
                .Select(h => h.KpiValue)
                .DefaultIfEmpty(k.KpiValue)
                .AverageAsync(cancellationToken);

            decimal? change = null;
            if (histAvg > 0)
            {
                change = ((k.KpiValue - histAvg) / histAvg) * 100;
            }

            kpiSummary.Add(new KpiSummaryItemDto
            {
                KpiType = k.KpiType,
                KpiName = k.KpiName,
                Value = k.KpiValue,
                HistoricalChangePct = change
            });
        }

        // Cash flow forecast
        var cashFlows = await _context.AiCashFlowForecasts
            .Where(f => f.StoreId == storeId)
            .OrderBy(f => f.ForecastDate)
            .ToListAsync(cancellationToken);

        // Current available cash
        var trialBalance = await _reportingService.GetTrialBalanceAsync(storeId, today, cancellationToken);
        decimal cashAvailable = trialBalance
            .Where(b => b.AccountCode.StartsWith("101") || b.AccountCode.StartsWith("102") || b.AccountCode == "1000" || b.AccountCode == "1100")
            .Sum(b => b.DebitBalance - b.CreditBalance);

        var forecastSummary = new CashFlowForecastSummaryDto
        {
            CurrentAvailableCash = cashAvailable,
            Projected30DayInflows = cashFlows.Sum(f => f.ProjectedInflow),
            Projected30DayOutflows = cashFlows.Sum(f => f.ProjectedOutflow),
            ProjectedEndingBalance = cashFlows.LastOrDefault()?.ProjectedBalance ?? cashAvailable,
            DailyForecasts = cashFlows.Select(f => new CashFlowForecastDailyDto
            {
                Date = f.ForecastDate,
                ProjectedInflow = f.ProjectedInflow,
                ProjectedOutflow = f.ProjectedOutflow,
                ProjectedBalance = f.ProjectedBalance,
                ConfidenceLevel = f.ConfidenceLevel
            }).ToList()
        };

        // Supplier recommendations count
        var pendingRecsQuery = _context.AiSupplierPaymentRecommendations.Where(r => r.FeedbackStatus == "PENDING");
        if (storeId.HasValue)
        {
            pendingRecsQuery = from r in pendingRecsQuery
                               join b in _context.PurchaseBills on r.PurchaseBillId equals b.Id
                               where b.StoreId == storeId
                               select r;
        }
        var pendingRecs = await pendingRecsQuery.ToListAsync(cancellationToken);

        // Active anomalies
        var anomalies = await _context.AiFinancialAnomalies
            .Where(a => !a.IsResolved)
            .ToListAsync(cancellationToken);

        var anomalySummary = anomalies.Select(a => new AnomalySummaryItemDto
        {
            Id = a.Id,
            AnomalyType = a.AnomalyType,
            Severity = a.Severity,
            Description = a.Description,
            DetectedAt = a.DetectedAt
        }).ToList();

        // Expiry Risks
        var criticalExpiryCount = await _context.AiExpiryRiskPredictions
            .Where(x => x.StoreId == storeId && x.RiskCategory == "CRITICAL")
            .CountAsync(cancellationToken);
        decimal potentialExpiryLoss = await _context.AiExpiryRiskPredictions
            .Where(x => x.StoreId == storeId && x.RiskCategory == "CRITICAL" || x.RiskCategory == "HIGH")
            .SumAsync(x => x.PotentialLoss, cancellationToken);

        // Shrinkage total loss
        decimal shrinkageLoss = await _context.AiInventoryShrinkageAnalytics
            .Where(x => x.StoreId == storeId)
            .SumAsync(x => x.ShrinkageCost, cancellationToken);
        decimal shrinkageRate = kpis.FirstOrDefault(k => k.KpiName == "SHRINKAGE_RATE")?.KpiValue ?? 0;
        string overallRisk = "LOW";
        if (shrinkageRate > 2.0m) overallRisk = "HIGH";
        else if (shrinkageRate > 0.5m) overallRisk = "MEDIUM";

        // Active Alerts
        var alerts = await _context.AiAlerts
            .Where(a => a.StoreId == storeId && !a.IsRead)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .Select(a => new AlertSummaryItemDto
            {
                Id = a.Id,
                AlertType = a.AlertType,
                Severity = a.AlertSeverity,
                Title = a.Title,
                Message = a.Message,
                CreatedAt = a.CreatedAt
            }).ToListAsync(cancellationToken);

        var summary = new AiDashboardSummaryDto
        {
            StoreId = storeId,
            StoreName = storeId.HasValue 
                ? (await _context.Stores.FindAsync(new object[] { storeId.Value }, cancellationToken))?.StoreName ?? "Store"
                : "Consolidated / HQ",
            Kpis = kpiSummary,
            CashFlowForecast = forecastSummary,
            PendingRecommendationsCount = pendingRecs.Count,
            RecommendedPaymentTotal = pendingRecs.Sum(r => r.AmountDue),
            ActiveAnomaliesCount = anomalies.Count,
            RecentAnomalies = anomalySummary,
            CriticalExpiryBatchesCount = criticalExpiryCount,
            ExpiryPotentialLossTotal = potentialExpiryLoss,
            TotalShrinkageLoss = shrinkageLoss,
            ShrinkageRatePct = shrinkageRate,
            OverallShrinkageRisk = overallRisk,
            ActiveAlerts = alerts
        };

        // Populate Store Benchmarking rankings if Consolidated
        if (!storeId.HasValue)
        {
            summary.StoreRankings = await GetStoreRankingsListAsync(cancellationToken);
        }

        return summary;
    }

    private async Task<List<StoreBenchmarkDto>> GetStoreRankingsListAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var startOfPeriod = today.AddDays(-30);

        var stores = await _context.Stores
            .Where(s => !s.IsDeleted && s.IsActive)
            .ToListAsync(cancellationToken);

        var benchmarkList = new List<StoreBenchmarkDto>();

        foreach (var st in stores)
        {
            var pAndL = await _reportingService.GetProfitAndLossAsync(st.Id, startOfPeriod, today, cancellationToken);
            
            // Cashier variance
            var sessions = await _context.PosSessions
                .Where(s => s.StoreId == st.Id && s.Status == "CLOSED" && s.EndTime >= startOfPeriod)
                .ToListAsync(cancellationToken);
            decimal totalDiff = sessions.Sum(s => Math.Abs(s.Difference));
            decimal totalExpected = sessions.Sum(s => s.ExpectedClosingCash);
            decimal cashierVar = totalExpected > 0 ? (totalDiff / totalExpected) * 100 : 0;

            // Shrinkage
            var shrinkageList = await _reportingService.GetInventoryShrinkageAsync(st.Id, startOfPeriod, today, cancellationToken);
            var valuationList = await _reportingService.GetInventoryValuationAsync(st.Id, today, cancellationToken);
            decimal totalVal = valuationList.Sum(v => v.TotalValuation);
            decimal totalShrinkLoss = shrinkageList.Sum(s => s.TotalLossValue);
            decimal shrinkageRate = totalVal > 0 ? (totalShrinkLoss / totalVal) * 100 : 0;

            decimal netProfitMargin = pAndL.TotalRevenue > 0 ? (pAndL.NetProfit / pAndL.TotalRevenue) * 100 : 0;

            benchmarkList.Add(new StoreBenchmarkDto
            {
                StoreId = st.Id,
                StoreCode = st.StoreCode,
                StoreName = st.StoreName,
                TotalSales = pAndL.TotalRevenue,
                NetProfitMargin = netProfitMargin,
                CashierVarianceRate = cashierVar,
                ShrinkageRatePct = shrinkageRate
            });
        }

        var ranked = benchmarkList.OrderByDescending(b => b.TotalSales).ToList();
        for (int i = 0; i < ranked.Count; i++)
        {
            ranked[i].Ranking = i + 1;
        }

        return ranked;
    }

    public async Task SubmitRecommendationFeedbackAsync(Guid recommendationId, string status, string? notes, Guid userId, CancellationToken cancellationToken)
    {
        var rec = await _context.AiSupplierPaymentRecommendations.FindAsync(new object[] { recommendationId }, cancellationToken);
        if (rec == null) throw new InvalidOperationException("Recommendation not found.");

        if (status != "ACCEPTED" && status != "REJECTED" && status != "PENDING")
        {
            throw new ArgumentException("Invalid status. Approved: PENDING, ACCEPTED, REJECTED");
        }

        rec.FeedbackStatus = status;
        rec.FeedbackNotes = notes;
        rec.ActionedAt = DateTime.UtcNow;
        rec.ActionedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
