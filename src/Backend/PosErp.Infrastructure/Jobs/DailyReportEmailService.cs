using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Jobs;

public class DailyReportEmailService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    public DailyReportEmailService(IServiceScopeFactory scopeFactory, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("[DailyReportEmailService] Background worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calculate delay until 11:59 PM India Standard Time (UTC+5.30)
                var nowIst = DateTime.UtcNow.AddHours(5.5);
                var targetTimeIst = nowIst.Date.AddHours(23).AddMinutes(59).AddSeconds(0);

                if (nowIst >= targetTimeIst)
                {
                    // Target has already passed for today, schedule for tomorrow
                    targetTimeIst = targetTimeIst.AddDays(1);
                }

                var delay = targetTimeIst - nowIst;
                Console.WriteLine($"[DailyReportEmailService] Next daily report email scheduled for {targetTimeIst} IST (Delay: {delay.TotalHours:F2} hours)");

                await Task.Delay(delay, stoppingToken);

                // Run report generation and email sending
                await SendDailyReportAsync(stoppingToken);

                // Wait 2 minutes to prevent double execution in the same minute
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Background service is stopping
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DailyReportEmailService] [ERROR] Background worker loop encountered exception: {ex.Message}");
                // Wait 5 minutes before retrying to prevent hot looping on persistent errors
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }

    public async Task SendDailyReportAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var recipientEmail = _configuration["EmailSettings:RecipientEmail"] ?? "jegadishmca@gmail.com";
        var today = DateTime.UtcNow.AddHours(5.5).Date;

        Console.WriteLine($"[DailyReportEmailService] Generating EOD Daily Report for {today:yyyy-MM-dd}...");

        // 1. Fetch completed invoices for sales summary and payment breakdown
        var todayInvoices = await context.Invoices
            .Where(i => i.BusinessDate == today && i.Status == "COMPLETED")
            .ToListAsync(cancellationToken);

        int invoiceCount = todayInvoices.Count;
        decimal totalSales = todayInvoices.Sum(i => i.TotalAmount);
        decimal cashTotal = todayInvoices.Sum(i => i.CashAmount);
        decimal upiTotal = todayInvoices.Sum(i => i.UpiAmount);
        decimal cardTotal = todayInvoices.Sum(i => i.CardAmount);
        decimal walletTotal = todayInvoices.Sum(i => i.WalletAmount);

        // 2. Fetch margin data
        var invoiceItems = await context.InvoiceItems
            .Include(ii => ii.Product)
            .Where(ii => ii.Invoice.BusinessDate == today && ii.Invoice.Status == "COMPLETED")
            .ToListAsync(cancellationToken);

        decimal totalCost = invoiceItems.Sum(ii => ii.Quantity * (ii.Product != null ? ii.Product.PurchasePrice : 0m));
        decimal totalProfit = totalSales - totalCost;
        decimal marginPercent = totalSales > 0 ? (totalProfit / totalSales) * 100m : 0m;

        // 3. Fetch Top 5 Selling Products today
        var topSellingItems = invoiceItems
            .GroupBy(ii => new { ii.ProductId, ii.ProductName })
            .Select(g => new
            {
                ProductName = g.Key.ProductName,
                Quantity = g.Sum(ii => ii.Quantity),
                TotalSales = g.Sum(ii => ii.TotalAmount)
            })
            .OrderByDescending(x => x.Quantity)
            .Take(5)
            .ToList();

        // 4. Fetch Low Stock Items (< 10 units)
        var lowStockItems = await context.Products
            .Where(p => !p.IsDeleted && p.IsActive)
            .Select(p => new
            {
                p.Name,
                p.ProductCode,
                Stock = context.StockLedger.Where(sl => sl.ProductId == p.Id).Sum(sl => sl.Quantity)
            })
            .Where(x => x.Stock < 10)
            .OrderBy(x => x.Stock)
            .Take(20) // Cap to avoid massive emails
            .ToListAsync(cancellationToken);

        // Generate Premium HTML Body
        var htmlBody = BuildHtmlReportBody(today, invoiceCount, totalSales, cashTotal, upiTotal, cardTotal, walletTotal, totalCost, totalProfit, marginPercent, topSellingItems, lowStockItems);

        // Send Email
        var subject = $"🍎 Daily EOD Sales & Margin Report - {today:dd MMM yyyy}";
        await emailService.SendEmailAsync(recipientEmail, subject, htmlBody);
    }

    private string BuildHtmlReportBody(
        DateTime reportDate,
        int invoiceCount,
        decimal totalSales,
        decimal cashTotal,
        decimal upiTotal,
        decimal cardTotal,
        decimal walletTotal,
        decimal totalCost,
        decimal totalProfit,
        decimal marginPercent,
        dynamic topSellingItems,
        dynamic lowStockItems)
    {
        var sb = new StringBuilder();

        sb.Append($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>EOD Daily Sales & Margin Report</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background-color: #f3f4f6;
            margin: 0;
            padding: 20px;
            color: #1f2937;
        }}
        .container {{
            max-width: 650px;
            background-color: #ffffff;
            margin: 0 auto;
            border-radius: 12px;
            box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06);
            overflow: hidden;
            border: 1px border #e5e7eb;
        }}
        .header {{
            background: linear-gradient(135deg, #1e1b4b 0%, #312e81 100%);
            color: #ffffff;
            padding: 30px 24px;
            text-align: center;
        }}
        .header h1 {{
            margin: 0;
            font-size: 24px;
            font-weight: 700;
            letter-spacing: -0.025em;
        }}
        .header p {{
            margin: 6px 0 0 0;
            font-size: 14px;
            color: #c7d2fe;
            font-weight: 500;
        }}
        .content {{
            padding: 24px;
        }}
        .grid {{
            display: table;
            width: 100%;
            table-layout: fixed;
            margin-bottom: 24px;
            border-collapse: separate;
            border-spacing: 12px 0;
            margin-left: -12px;
            margin-right: -12px;
        }}
        .grid-col {{
            display: table-cell;
            background-color: #f9fafb;
            border: 1px solid #f3f4f6;
            border-radius: 8px;
            padding: 16px;
            text-align: center;
        }}
        .metric-title {{
            font-size: 11px;
            text-transform: uppercase;
            letter-spacing: 0.05em;
            color: #6b7280;
            margin-bottom: 4px;
            font-weight: 600;
        }}
        .metric-value {{
            font-size: 18px;
            font-weight: 700;
            color: #1e1b4b;
        }}
        .metric-value.success {{
            color: #059669;
        }}
        .section-title {{
            font-size: 15px;
            font-weight: 700;
            color: #111827;
            border-bottom: 2px solid #e5e7eb;
            padding-bottom: 8px;
            margin-top: 24px;
            margin-bottom: 12px;
            text-transform: uppercase;
            letter-spacing: 0.025em;
        }}
        table {{
            width: 100%;
            border-collapse: collapse;
            margin-bottom: 16px;
        }}
        th {{
            background-color: #f3f4f6;
            color: #374151;
            font-weight: 600;
            text-align: left;
            padding: 10px 12px;
            font-size: 12px;
            border-bottom: 1px solid #e5e7eb;
        }}
        td {{
            padding: 10px 12px;
            font-size: 13px;
            border-bottom: 1px solid #f3f4f6;
            color: #4b5563;
        }}
        tr:last-child td {{
            border-bottom: none;
        }}
        .badge {{
            display: inline-block;
            padding: 2px 8px;
            font-size: 11px;
            font-weight: 600;
            border-radius: 9999px;
        }}
        .badge-danger {{
            background-color: #fee2e2;
            color: #991b1b;
        }}
        .badge-warning {{
            background-color: #fef3c7;
            color: #92400e;
        }}
        .footer {{
            background-color: #f9fafb;
            padding: 20px 24px;
            text-align: center;
            border-top: 1px solid #e5e7eb;
            font-size: 12px;
            color: #9ca3af;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Apple Supermarket</h1>
            <p>End-Of-Day Daily Business Performance Report</p>
            <p style='font-size: 13px; font-weight: bold; margin-top: 8px; background: rgba(255,255,255,0.15); display: inline-block; padding: 4px 12px; border-radius: 4px;'>
                {reportDate:dd MMMM yyyy}
            </p>
        </div>
        <div class='content'>
            
            <div class='section-title'>📊 Key Financial Indicators</div>
            <div class='grid'>
                <div class='grid-col'>
                    <div class='metric-title'>Total Sales</div>
                    <div class='metric-value'>₹{totalSales:N2}</div>
                </div>
                <div class='grid-col'>
                    <div class='metric-title'>Invoices Raised</div>
                    <div class='metric-value'>{invoiceCount}</div>
                </div>
                <div class='grid-col'>
                    <div class='metric-title'>Gross Margin</div>
                    <div class='metric-value success'>₹{totalProfit:N2}</div>
                </div>
                <div class='grid-col'>
                    <div class='metric-title'>Margin %</div>
                    <div class='metric-value success'>{marginPercent:F2}%</div>
                </div>
            </div>

            <div class='section-title'>💳 Sales Breakdown by Payment Type</div>
            <table>
                <thead>
                    <tr>
                        <th>Payment Mode</th>
                        <th style='text-align: right;'>Collection Amount</th>
                        <th style='text-align: right;'>Percentage Split</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td>💵 Cash</td>
                        <td style='text-align: right; font-weight: 500;'>₹{cashTotal:N2}</td>
                        <td style='text-align: right;'>{(totalSales > 0 ? (cashTotal / totalSales) * 100m : 0m):F1}%</td>
                    </tr>
                    <tr>
                        <td>📱 UPI / QR Code</td>
                        <td style='text-align: right; font-weight: 500;'>₹{upiTotal:N2}</td>
                        <td style='text-align: right;'>{(totalSales > 0 ? (upiTotal / totalSales) * 100m : 0m):F1}%</td>
                    </tr>
                    <tr>
                        <td>💳 Card Payment</td>
                        <td style='text-align: right; font-weight: 500;'>₹{cardTotal:N2}</td>
                        <td style='text-align: right;'>{(totalSales > 0 ? (cardTotal / totalSales) * 100m : 0m):F1}%</td>
                    </tr>
                    <tr>
                        <td>👛 Store Wallet</td>
                        <td style='text-align: right; font-weight: 500;'>₹{walletTotal:N2}</td>
                        <td style='text-align: right;'>{(totalSales > 0 ? (walletTotal / totalSales) * 100m : 0m):F1}%</td>
                    </tr>
                </tbody>
            </table>

            <div class='section-title'>🔥 Top 5 Selling Products Today</div>
            <table>
                <thead>
                    <tr>
                        <th>Product Name</th>
                        <th style='text-align: center;'>Qty Sold</th>
                        <th style='text-align: right;'>Revenue Contribution</th>
                    </tr>
                </thead>
                <tbody>");

        if (topSellingItems.Count == 0)
        {
            sb.Append("<tr><td colspan='3' style='text-align: center; color: #9ca3af;'>No products sold today.</td></tr>");
        }
        else
        {
            foreach (var item in topSellingItems)
            {
                sb.Append($@"
                    <tr>
                        <td style='font-weight: 500;'>{item.ProductName}</td>
                        <td style='text-align: center;'>{item.Quantity:N0}</td>
                        <td style='text-align: right; font-weight: 500; color: #059669;'>₹{item.TotalSales:N2}</td>
                    </tr>");
            }
        }

        sb.Append($@"
                </tbody>
            </table>

            <div class='section-title'>⚠️ Inventory Alerts & Low Stock Items</div>
            <table>
                <thead>
                    <tr>
                        <th>Code</th>
                        <th>Product Name</th>
                        <th style='text-align: center;'>Current Stock</th>
                        <th style='text-align: center;'>Status</th>
                    </tr>
                </thead>
                <tbody>");

        if (lowStockItems.Count == 0)
        {
            sb.Append("<tr><td colspan='4' style='text-align: center; color: #059669; font-weight: 500;'>All products have healthy stock levels (> 10 units).</td></tr>");
        }
        else
        {
            foreach (var item in lowStockItems)
            {
                var badgeClass = item.Stock <= 0 ? "badge-danger" : "badge-warning";
                var statusText = item.Stock <= 0 ? "OUT OF STOCK" : "LOW STOCK";

                sb.Append($@"
                    <tr>
                        <td><code>{item.ProductCode}</code></td>
                        <td style='font-weight: 500;'>{item.Name}</td>
                        <td style='text-align: center; font-weight: bold;'>{item.Stock:N0}</td>
                        <td style='text-align: center;'>
                            <span class='badge {badgeClass}'>{statusText}</span>
                        </td>
                    </tr>");
            }
        }

        sb.Append($@"
                </tbody>
            </table>

        </div>
        <div class='footer'>
            <p>This is an automated EOD business report generated by the Apple Supermarket ERP System.</p>
            <p style='margin-top: 4px; font-size: 11px;'>System Date Time: {DateTime.UtcNow.AddHours(5.5):dd MMM yyyy HH:mm} IST</p>
        </div>
    </div>
</body>
</html>");

        return sb.ToString();
    }
}
