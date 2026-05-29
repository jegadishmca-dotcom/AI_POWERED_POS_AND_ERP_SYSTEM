using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosErp.Application.Features.Analytics.Queries.GetTodayDashboard;
using PosErp.Application.Features.Analytics.Queries.GetSalesAnalytics;
using PosErp.Application.Features.Analytics.Queries.GetTopProducts;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Manager,Owner")] // Strict Role-Based Access Control
public class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;

    static AnalyticsController()
    {
        // Register QuestPDF community license
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public AnalyticsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var result = await _mediator.Send(new GetTodayDashboardQuery(null));
        return Ok(result);
    }

    [HttpGet("sales-trend")]
    public async Task<IActionResult> GetSalesTrend([FromQuery] int days = 7)
    {
        var result = await _mediator.Send(new GetSalesAnalyticsQuery(days));
        return Ok(result);
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> GetTopProducts()
    {
        var result = await _mediator.Send(new GetTopProductsQuery(30));
        return Ok(result);
    }

    [HttpGet("export/pdf")]
    public async Task<IActionResult> ExportPdfReport()
    {
        try
        {
            var kpis = await _mediator.Send(new GetTodayDashboardQuery(null));
            var trends = await _mediator.Send(new GetSalesAnalyticsQuery(7));
            var products = await _mediator.Send(new GetTopProductsQuery(30));

            var document = new DailySalesReportDocument(kpis, trends, products);
            byte[] pdfBytes = document.GeneratePdf();

            return File(pdfBytes, "application/pdf", "DailySalesReport.pdf");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PDF Export failed: {ex.Message}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("export/excel")]
    public async Task<IActionResult> ExportExcelReport()
    {
        try
        {
            var kpis = await _mediator.Send(new GetTodayDashboardQuery(null));
            var trends = await _mediator.Send(new GetSalesAnalyticsQuery(7));
            var products = await _mediator.Send(new GetTopProductsQuery(30));

            byte[] excelBytes = GenerateExcelReport(kpis, trends, products);

            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SalesAnalytics.xlsx");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Excel Export failed: {ex.Message}");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    private byte[] GenerateExcelReport(DashboardKpiDto kpi, List<SalesTrendDto> trends, List<TopProductDto> products)
    {
        using (var workbook = new XLWorkbook())
        {
            // 1. KPI & Overview sheet
            var wsOverview = workbook.Worksheets.Add("Overview");
            
            wsOverview.Cell("A1").Value = "Apple Super Market - Daily Sales Report";
            wsOverview.Range("A1:D1").Merge();
            wsOverview.Cell("A1").Style.Font.FontSize = 16;
            wsOverview.Cell("A1").Style.Font.Bold = true;
            wsOverview.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            
            wsOverview.Cell("A3").Value = "Report Date:";
            wsOverview.Cell("B3").Value = DateTime.Now.ToString("dd/MM/yyyy hh:mm tt");
            wsOverview.Cell("A3").Style.Font.Bold = true;

            // KPI block
            wsOverview.Cell("A5").Value = "Metric";
            wsOverview.Cell("B5").Value = "Value";
            wsOverview.Range("A5:B5").Style.Font.Bold = true;
            wsOverview.Range("A5:B5").Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            
            wsOverview.Cell("A6").Value = "Today's Net Sales";
            wsOverview.Cell("B6").Value = kpi.TodaySales;
            wsOverview.Cell("B6").Style.NumberFormat.Format = "₹#,##0.00";
            
            wsOverview.Cell("A7").Value = "Total Invoices";
            wsOverview.Cell("B7").Value = kpi.TodayOrders;
            wsOverview.Cell("B7").Style.NumberFormat.Format = "#,##0";
            
            wsOverview.Cell("A8").Value = "Average Basket Value";
            wsOverview.Cell("B8").Value = kpi.AvgOrderValue;
            wsOverview.Cell("B8").Style.NumberFormat.Format = "₹#,##0.00";

            wsOverview.Range("A6:A8").Style.Font.Bold = true;
            wsOverview.Columns().AdjustToContents();

            // 2. Sales Trend sheet
            var wsTrend = workbook.Worksheets.Add("Sales Trend");
            wsTrend.Cell("A1").Value = "Date";
            wsTrend.Cell("B1").Value = "Gross Sales";
            wsTrend.Cell("C1").Value = "Net Sales";
            wsTrend.Cell("D1").Value = "Total Invoices";
            wsTrend.Range("A1:D1").Style.Font.Bold = true;
            wsTrend.Range("A1:D1").Style.Border.BottomBorder = XLBorderStyleValues.Medium;

            int rowIdx = 2;
            foreach (var trend in trends)
            {
                wsTrend.Cell(rowIdx, 1).Value = trend.Date;
                wsTrend.Cell(rowIdx, 2).Value = trend.GrossSales;
                wsTrend.Cell(rowIdx, 3).Value = trend.NetSales;
                wsTrend.Cell(rowIdx, 4).Value = trend.TotalInvoices;
                
                wsTrend.Cell(rowIdx, 2).Style.NumberFormat.Format = "₹#,##0.00";
                wsTrend.Cell(rowIdx, 3).Style.NumberFormat.Format = "₹#,##0.00";
                wsTrend.Cell(rowIdx, 4).Style.NumberFormat.Format = "#,##0";
                rowIdx++;
            }
            wsTrend.Cell(rowIdx, 1).Value = "Total";
            wsTrend.Cell(rowIdx, 1).Style.Font.Bold = true;
            wsTrend.Cell(rowIdx, 2).FormulaA1 = $"SUM(B2:B{rowIdx-1})";
            wsTrend.Cell(rowIdx, 3).FormulaA1 = $"SUM(C2:C{rowIdx-1})";
            wsTrend.Cell(rowIdx, 4).FormulaA1 = $"SUM(D2:D{rowIdx-1})";
            wsTrend.Range(rowIdx, 2, rowIdx, 4).Style.Font.Bold = true;
            wsTrend.Cell(rowIdx, 2).Style.NumberFormat.Format = "₹#,##0.00";
            wsTrend.Cell(rowIdx, 3).Style.NumberFormat.Format = "₹#,##0.00";
            wsTrend.Cell(rowIdx, 4).Style.NumberFormat.Format = "#,##0";

            wsTrend.Columns().AdjustToContents();

            // 3. Top Products sheet
            var wsProducts = workbook.Worksheets.Add("Top Products");
            wsProducts.Cell("A1").Value = "Product Name";
            wsProducts.Cell("B1").Value = "Quantity Sold";
            wsProducts.Cell("C1").Value = "Total Revenue";
            wsProducts.Cell("A1:C1").Style.Font.Bold = true;
            wsProducts.Cell("A1:C1").Style.Border.BottomBorder = XLBorderStyleValues.Medium;

            rowIdx = 2;
            foreach (var prod in products)
            {
                wsProducts.Cell(rowIdx, 1).Value = prod.ProductName;
                wsProducts.Cell(rowIdx, 2).Value = prod.TotalQuantitySold;
                wsProducts.Cell(rowIdx, 3).Value = prod.TotalRevenue;
                
                wsProducts.Cell(rowIdx, 2).Style.NumberFormat.Format = "#,##0.00";
                wsProducts.Cell(rowIdx, 3).Style.NumberFormat.Format = "₹#,##0.00";
                rowIdx++;
            }
            wsProducts.Columns().AdjustToContents();

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                return stream.ToArray();
            }
        }
    }
}

public class DailySalesReportDocument : IDocument
{
    private readonly DashboardKpiDto _kpi;
    private readonly List<SalesTrendDto> _trends;
    private readonly List<TopProductDto> _products;

    public DailySalesReportDocument(DashboardKpiDto kpi, List<SalesTrendDto> trends, List<TopProductDto> products)
    {
        _kpi = kpi;
        _trends = trends;
        _products = products;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Margin(50);
                page.Size(PageSizes.A4);
                
                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
    }

    void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("Apple Super Market").FontSize(24).Bold().FontColor("#4f46e5");
                column.Item().Text("Daily Store Sales & Operations Analytics").FontSize(12).SemiBold().FontColor(Colors.Grey.Medium);
                column.Item().Text($"Generated: {DateTime.Now:dd/MM/yyyy hh:mm tt}").FontSize(9).Italic();
            });
        });
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Spacing(20);
            
            // KPI Summary Row
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(c => ComposeKpiCard(c, "Today's Net Sales", $"₹{_kpi.TodaySales:N2}"));
                row.ConstantItem(15);
                row.RelativeItem().Element(c => ComposeKpiCard(c, "Total Invoices", _kpi.TodayOrders.ToString()));
                row.ConstantItem(15);
                row.RelativeItem().Element(c => ComposeKpiCard(c, "Average Basket Value", $"₹{_kpi.AvgOrderValue:N2}"));
            });

            // 7-Day Trend Section
            column.Item().Column(trendCol =>
            {
                trendCol.Spacing(5);
                trendCol.Item().Text("7-Day Sales Performance").FontSize(14).Bold();
                trendCol.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("Date").Bold();
                        header.Cell().Element(CellStyle).Text("Gross Sales").Bold();
                        header.Cell().Element(CellStyle).Text("Net Sales").Bold();
                        header.Cell().Element(CellStyle).Text("Invoices").Bold();

                        IContainer CellStyle(IContainer c) => c.DefaultTextStyle(x => x.SemiBold()).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingVertical(5);
                    });

                    foreach (var trend in _trends)
                    {
                        table.Cell().Element(CellStyle).Text(trend.Date);
                        table.Cell().Element(CellStyle).Text($"₹{trend.GrossSales:N2}");
                        table.Cell().Element(CellStyle).Text($"₹{trend.NetSales:N2}");
                        table.Cell().Element(CellStyle).Text(trend.TotalInvoices.ToString());

                        IContainer CellStyle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                    }
                });
            });

            // Top Products Section
            column.Item().Column(prodCol =>
            {
                prodCol.Spacing(5);
                prodCol.Item().Text("Top Moving Products").FontSize(14).Bold();
                prodCol.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellStyle).Text("#").Bold();
                        header.Cell().Element(CellStyle).Text("Product Name").Bold();
                        header.Cell().Element(CellStyle).Text("Qty Sold").Bold();
                        header.Cell().Element(CellStyle).Text("Revenue").Bold();

                        IContainer CellStyle(IContainer c) => c.DefaultTextStyle(x => x.SemiBold()).BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingVertical(5);
                    });

                    int idx = 1;
                    foreach (var prod in _products)
                    {
                        table.Cell().Element(CellStyle).Text(idx.ToString());
                        table.Cell().Element(CellStyle).Text(prod.ProductName);
                        table.Cell().Element(CellStyle).Text(prod.TotalQuantitySold.ToString("0.##"));
                        table.Cell().Element(CellStyle).Text($"₹{prod.TotalRevenue:N2}");
                        idx++;

                        IContainer CellStyle(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                    }
                });
            });
        });
    }

    void ComposeKpiCard(IContainer container, string title, string value)
    {
        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten5)
            .Padding(10)
            .Column(col =>
            {
                col.Item().Text(title).FontSize(9).FontColor(Colors.Grey.Medium).Bold();
                col.Item().Text(value).FontSize(16).Bold().FontColor("#4f46e5");
            });
    }
}
