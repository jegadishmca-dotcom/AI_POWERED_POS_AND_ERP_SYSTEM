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
public class GstReportsController : ControllerBase
{
    private readonly IFinancialReportingService _reportingService;
    private readonly IApplicationDbContext _context;

    public GstReportsController(IFinancialReportingService reportingService, IApplicationDbContext context)
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

    [HttpGet("summary")]
    public async Task<IActionResult> GetGstSummary([FromQuery] Guid? storeId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string format = "json")
    {
        var start = startDate ?? DateTime.Today.AddDays(-30);
        var end = endDate ?? DateTime.Today;
        var data = await _reportingService.GetGstSummaryAsync(storeId, start, end, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = $"{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fileName = $"GstSummary_{storeCode}_{dateStr}";

        var flattened = FlattenGstSummary(data);
        return ExportData(flattened, "GST Summary Report", storeCode, dateStr, format, fileName);
    }

    [HttpGet("sales-register")]
    public async Task<IActionResult> GetSalesRegister([FromQuery] Guid? storeId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string format = "json")
    {
        var start = startDate ?? DateTime.Today.AddDays(-30);
        var end = endDate ?? DateTime.Today;
        var data = await _reportingService.GetSalesRegisterAsync(storeId, start, end, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = $"{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fileName = $"GstSalesRegister_{storeCode}_{dateStr}";

        return ExportData(data, "GST Sales Register", storeCode, dateStr, format, fileName);
    }

    [HttpGet("purchase-register")]
    public async Task<IActionResult> GetPurchaseRegister([FromQuery] Guid? storeId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string format = "json")
    {
        var start = startDate ?? DateTime.Today.AddDays(-30);
        var end = endDate ?? DateTime.Today;
        var data = await _reportingService.GetPurchaseRegisterAsync(storeId, start, end, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = $"{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fileName = $"GstPurchaseRegister_{storeCode}_{dateStr}";

        return ExportData(data, "GST Purchase Register", storeCode, dateStr, format, fileName);
    }

    [HttpGet("gstr1")]
    public async Task<IActionResult> GetGstr1([FromQuery] Guid? storeId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string format = "json")
    {
        var start = startDate ?? DateTime.Today.AddDays(-30);
        var end = endDate ?? DateTime.Today;
        var data = await _reportingService.GetGstr1ReportAsync(storeId, start, end, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = $"{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fileName = $"GSTR1_{storeCode}_{dateStr}";

        // Default export formats export HSN Summary for GSTR-1 outward supply details
        return ExportData(data.HsnSummary, "GSTR-1 HSN Summary", storeCode, dateStr, format, fileName);
    }

    [HttpGet("gstr3b")]
    public async Task<IActionResult> GetGstr3b([FromQuery] Guid? storeId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, [FromQuery] string format = "json")
    {
        var start = startDate ?? DateTime.Today.AddDays(-30);
        var end = endDate ?? DateTime.Today;
        var data = await _reportingService.GetGstr3BReportAsync(storeId, start, end, HttpContext.RequestAborted);

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(data);
        }

        var storeCode = await GetStoreCodeAsync(storeId);
        var dateStr = $"{start:yyyyMMdd}_{end:yyyyMMdd}";
        var fileName = $"GSTR3B_{storeCode}_{dateStr}";

        var flattened = FlattenGstr3B(data);
        return ExportData(flattened, "GSTR-3B Summary", storeCode, dateStr, format, fileName);
    }

    #region Flattening Helpers

    public class GstSummaryRow
    {
        public string Metric { get; set; } = string.Empty;
        public decimal TaxableAmount { get; set; }
        public decimal Cgst { get; set; }
        public decimal Sgst { get; set; }
        public decimal Igst { get; set; }
        public decimal TotalTax { get; set; }
    }

    private List<GstSummaryRow> FlattenGstSummary(GstSummaryDto summary)
    {
        return new List<GstSummaryRow>
        {
            new() { Metric = "Outward Supplies (Sales)", TaxableAmount = summary.OutwardTaxableAmount, Cgst = summary.OutwardCgst, Sgst = summary.OutwardSgst, Igst = summary.OutwardIgst, TotalTax = summary.OutwardTotalTax },
            new() { Metric = "Inward Supplies (ITC)", TaxableAmount = summary.InwardTaxableAmount, Cgst = summary.InwardCgst, Sgst = summary.InwardSgst, Igst = summary.InwardIgst, TotalTax = summary.InwardTotalTax },
            new() { Metric = "Net GST Payable", TaxableAmount = 0, Cgst = summary.NetCgstPayable, Sgst = summary.NetSgstPayable, Igst = summary.NetIgstPayable, TotalTax = summary.NetTotalTaxPayable }
        };
    }

    private List<GstSummaryRow> FlattenGstr3B(Gstr3BReportDto r)
    {
        return new List<GstSummaryRow>
        {
            new() { Metric = "3.1 Outward Taxable Supplies", TaxableAmount = r.OutwardTaxableValue, Cgst = r.OutwardCgst, Sgst = r.OutwardSgst, Igst = r.OutwardIgst, TotalTax = r.OutwardCgst + r.OutwardSgst + r.OutwardIgst },
            new() { Metric = "4. Eligible ITC", TaxableAmount = r.ItcTaxableValue, Cgst = r.ItcCgst, Sgst = r.ItcSgst, Igst = r.ItcIgst, TotalTax = r.ItcCgst + r.ItcSgst + r.ItcIgst },
            new() { Metric = "Net Liability / (Refund)", TaxableAmount = 0, Cgst = r.NetCgstLiability, Sgst = r.NetSgstLiability, Igst = r.NetIgstLiability, TotalTax = r.NetCgstLiability + r.NetSgstLiability + r.NetIgstLiability }
        };
    }

    #endregion
}
