using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosErp.Application.Features.Analytics.Queries.GetTodayDashboard;
using PosErp.Application.Features.Analytics.Queries.GetSalesAnalytics;
using PosErp.Application.Features.Analytics.Queries.GetTopProducts;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Manager,Owner")] // Strict Role-Based Access Control
public class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;

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
    public IActionResult ExportPdfReport()
    {
        // Integration with QuestPDF goes here
        // var document = new DailySalesReportDocument(kpis, trends);
        // byte[] pdfBytes = document.GeneratePdf();
        byte[] mockPdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // %PDF
        return File(mockPdf, "application/pdf", "DailySalesReport.pdf");
    }

    [HttpGet("export/excel")]
    public IActionResult ExportExcelReport()
    {
        // Integration with EPPlus goes here
        // using var package = new ExcelPackage();
        // var sheet = package.Workbook.Worksheets.Add("Sales");
        byte[] mockExcel = new byte[] { 0x50, 0x4B, 0x03, 0x04 }; // PK zip magic number
        return File(mockExcel, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SalesAnalytics.xlsx");
    }
}
