using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Api.Helpers;
using PosErp.Application.Features.Finance.Services;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Manager,Owner")]
public class InventoryReportsController : ControllerBase
{
    private readonly IFinancialReportingService _reportingService;
    private readonly IApplicationDbContext _context;

    public InventoryReportsController(IFinancialReportingService reportingService, IApplicationDbContext context)
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

    [HttpGet("valuation")]
    public async Task<IActionResult> GetValuation([FromQuery] Guid? storeId, [FromQuery] DateTime? asOfDate, [FromQuery] string format = "json")
    {
        var targetDate = asOfDate ?? DateTime.Today;
        var data = await _reportingService.GetInventoryValuationAsync(storeId, targetDate, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = targetDate.ToString("yyyyMMdd");
        var fileName = $"InventoryValuation_{storeCode}_{dateStr}";

        return ExportData(data, "Inventory Valuation Report", storeCode, dateStr, format, fileName);
    }

    [HttpGet("aging")]
    public async Task<IActionResult> GetAging([FromQuery] Guid? storeId, [FromQuery] DateTime? asOfDate, [FromQuery] string format = "json")
    {
        var targetDate = asOfDate ?? DateTime.Today;
        var data = await _reportingService.GetStockAgingAsync(storeId, targetDate, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = targetDate.ToString("yyyyMMdd");
        var fileName = $"StockAging_{storeCode}_{dateStr}";

        return ExportData(data, "Stock Aging Report", storeCode, dateStr, format, fileName);
    }

    [HttpGet("expiry")]
    public async Task<IActionResult> GetExpiry([FromQuery] Guid? storeId, [FromQuery] DateTime? asOfDate, [FromQuery] string format = "json")
    {
        var targetDate = asOfDate ?? DateTime.Today;
        var data = await _reportingService.GetExpiryAnalysisAsync(storeId, targetDate, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = targetDate.ToString("yyyyMMdd");
        var fileName = $"ExpiryAnalysis_{storeCode}_{dateStr}";

        return ExportData(data, "Expiry Analysis Report", storeCode, dateStr, format, fileName);
    }

    [HttpGet("movement")]
    public async Task<IActionResult> GetMovement([FromQuery] Guid? storeId, [FromQuery] Guid productId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string format = "json")
    {
        if (productId == Guid.Empty)
        {
            return BadRequest("Product ID is required.");
        }

        var start = startDate ?? DateTime.Today.AddDays(-30);
        var end = endDate ?? DateTime.Today;
        var data = await _reportingService.GetInventoryMovementLedgerAsync(storeId, productId, start, end, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = $"{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fileName = $"InventoryMovement_{data.ProductCode}_{storeCode}_{dateStr}";

        return ExportData(data.Lines, $"Inventory Movement - {data.ProductCode} ({data.ProductName})", storeCode, dateStr, format, fileName);
    }

    [HttpGet("shrinkage")]
    public async Task<IActionResult> GetShrinkage([FromQuery] Guid? storeId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string format = "json")
    {
        var start = startDate ?? DateTime.Today.AddDays(-30);
        var end = endDate ?? DateTime.Today;
        var data = await _reportingService.GetInventoryShrinkageAsync(storeId, start, end, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = $"{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fileName = $"InventoryShrinkage_{storeCode}_{dateStr}";

        return ExportData(data, "Inventory Shrinkage Report", storeCode, dateStr, format, fileName);
    }

    [HttpGet("top-selling")]
    public async Task<IActionResult> GetTopSelling([FromQuery] Guid? storeId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] int limit = 10, [FromQuery] string format = "json")
    {
        var start = startDate ?? DateTime.Today.AddDays(-30);
        var end = endDate ?? DateTime.Today;
        var data = await _reportingService.GetTopSellingProductsAsync(storeId, start, end, limit, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = $"{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fileName = $"TopSellingProducts_{storeCode}_{dateStr}";

        return ExportData(data, "Top-Selling Products Report", storeCode, dateStr, format, fileName);
    }

    [HttpGet("slow-moving")]
    public async Task<IActionResult> GetSlowMoving([FromQuery] Guid? storeId, [FromQuery] int thresholdDays = 30, [FromQuery] string format = "json")
    {
        var data = await _reportingService.GetSlowMovingInventoryAsync(storeId, thresholdDays, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = DateTime.Today.ToString("yyyyMMdd");
        var fileName = $"SlowMovingInventory_{storeCode}_{dateStr}";

        return ExportData(data, "Slow-Moving Inventory Report", storeCode, dateStr, format, fileName);
    }
}
