using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosErp.Application.Features.Finance.Commands;
using PosErp.Application.Features.Finance.Queries;
using PosErp.Application.Features.Finance.Services;
using PosErp.Infrastructure.Printing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using PosErp.Api.Helpers;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager,Owner,Supervisor,Cashier")]
public class AccountsReceivableController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPrintService? _printService;
    private readonly Microsoft.Extensions.Logging.ILogger<AccountsReceivableController> _logger;

    public AccountsReceivableController(
        IMediator mediator, 
        Microsoft.Extensions.Logging.ILogger<AccountsReceivableController> logger,
        IPrintService? printService = null)
    {
        _mediator = mediator;
        _logger = logger;
        _printService = printService;
    }

    [HttpPost("receipts")]
    public async Task<IActionResult> ProcessReceipt([FromBody] ProcessCustomerReceiptRequest request)
    {
        try
        {
            var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid.TryParse(callerIdStr, out Guid userId);

            var storeId = request.StoreId != Guid.Empty ? request.StoreId : Guid.Parse("00000000-0000-0000-0000-000000000000");

            var command = new ProcessCustomerReceiptCommand(
                storeId,
                request.CustomerId,
                request.ReceiptDate,
                request.PaymentMode,
                request.ReferenceNumber,
                request.Amount,
                request.Notes,
                request.AllocationMode,
                request.ManualAllocations,
                userId
            );

            var id = await _mediator.Send(command);
            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing customer receipt for customer {CustomerId}", request.CustomerId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("returns")]
    public async Task<IActionResult> ProcessReturn([FromBody] ProcessSalesReturnRequest request)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(callerIdStr, out Guid userId);

        var scope = StoreScopeHelper.GetCallerStoreScope(User);
        if (scope.IsDenied)
        {
            return Forbid();
        }

        // If caller is store-scoped (Cashier, Supervisor), enforce their assigned store.
        // If caller is Global (Owner/Admin), pass request.StoreId (or Guid.Empty if not provided).
        // The command handler strictly validates request.StoreId against the original invoice.StoreId
        // and guarantees all return records, stock movements, and journal entries anchor to invoice.StoreId.
        var storeIdToPass = scope.StoreId ?? request.StoreId;

        var command = new ProcessSalesReturnCommand(
            storeIdToPass,
            request.InvoiceId,
            request.ReturnDate,
            request.RefundMode,
            request.Items,
            userId,
            request.ManagerOverridePin
        );

        try
        {
            var id = await _mediator.Send(command);
            return Ok(new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpGet("returns")]
    public async Task<IActionResult> GetSalesReturns(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var scope = StoreScopeHelper.GetCallerStoreScope(User);
        if (scope.IsDenied)
        {
            return Ok(new List<SalesReturnSummaryDto>());
        }

        var result = await _mediator.Send(new GetSalesReturnsQuery(fromDate, toDate, limit, scope.StoreId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("ledger")]
    public async Task<IActionResult> GetLedger([FromQuery] Guid customerId, [FromQuery] Guid storeId)
    {
        var result = await _mediator.Send(new GetCustomerLedgerQuery(customerId, storeId));
        return Ok(result);
    }

    [HttpGet("aging")]
    public async Task<IActionResult> GetAging([FromQuery] Guid storeId, [FromQuery] DateTime asOfDate)
    {
        var result = await _mediator.Send(new GetCustomerAgingReportQuery(storeId, asOfDate));
        return Ok(result);
    }

    [HttpGet("receipts")]
    public async Task<IActionResult> GetReceipts([FromQuery] Guid? storeId)
    {
        var activeStoreId = storeId ?? Guid.Parse("00000000-0000-0000-0000-000000000000");
        var result = await _mediator.Send(new GetCustomerReceiptsQuery(activeStoreId));
        return Ok(result);
    }

    [HttpGet("returns/{id}")]
    public async Task<IActionResult> GetReturnDetails(string id, CancellationToken cancellationToken = default)
    {
        var scope = StoreScopeHelper.GetCallerStoreScope(User);
        if (scope.IsDenied)
        {
            return NotFound(new { message = $"Sales return '{id}' not found." });
        }

        var result = await _mediator.Send(new GetSalesReturnByIdQuery(id, scope.StoreId), cancellationToken);
        if (result == null)
        {
            return NotFound(new { message = $"Sales return '{id}' not found." });
        }
        return Ok(result);
    }

    [HttpPost("returns/{id}/print")]
    public async Task<IActionResult> PrintReturnReceipt(string id, [FromQuery] string printerIp = "192.168.1.100", CancellationToken cancellationToken = default)
    {
        var scope = StoreScopeHelper.GetCallerStoreScope(User);
        if (scope.IsDenied)
        {
            return NotFound(new { message = $"Sales return '{id}' not found." });
        }

        var returnDto = await _mediator.Send(new GetSalesReturnByIdQuery(id, scope.StoreId), cancellationToken);
        if (returnDto == null)
        {
            return NotFound(new { message = $"Sales return '{id}' not found." });
        }

        if (_printService == null)
        {
            return BadRequest(new { message = "Print service is not configured on this server." });
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("         ஆப்பிள் சூப்பர் மார்க்கெட்");
        sb.AppendLine("            Apple Super Market");
        sb.AppendLine("       1E-16, Matha Kovil Street,");
        sb.AppendLine("          Ilayankudi - 630702");
        sb.AppendLine("      Ph: 7339056767 / 04564-221190");
        sb.AppendLine("          GSTIN: 33ABTFA7190F1Z7");
        sb.AppendLine("          FSSAI: 12421019000047");
        sb.AppendLine("         SALES RETURN / CREDIT NOTE");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Return No: {returnDto.ReturnNumber}");
        if (!string.IsNullOrWhiteSpace(returnDto.OriginalInvoiceNumber))
            sb.AppendLine($"Orig Bill: {returnDto.OriginalInvoiceNumber}");
        sb.AppendLine($"Date: {returnDto.ReturnDate:dd/MM/yyyy}  Time: {returnDto.CreatedAt:HH:mm}");
        sb.AppendLine($"Cashier: {(returnDto.CashierName ?? "Cashier").PadRight(15)} Term: {returnDto.TerminalCode ?? "POS-01"}");
        if (!string.IsNullOrWhiteSpace(returnDto.CustomerName))
            sb.AppendLine($"Customer: {returnDto.CustomerName} | {returnDto.CustomerPhone ?? ""}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("Item                     Qty  Rate   Amt");
        sb.AppendLine("----------------------------------------");

        foreach (var item in returnDto.Items)
        {
            var name = item.ProductName.Length > 20 ? item.ProductName.Substring(0, 19) + "." : item.ProductName;
            sb.AppendLine($"{name.PadRight(20)} {item.Quantity.ToString("0.##").PadLeft(3)} {item.UnitPrice.ToString("0.00").PadLeft(6)} {item.TotalAmount.ToString("0.00").PadLeft(7)}");
        }

        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Items Count: {returnDto.Items.Count,-4} Total Qty: {returnDto.Items.Sum(x => x.Quantity):0.##}");
        sb.AppendLine($"Sub Total:                     ₹{returnDto.SubTotal:0.00}");
        sb.AppendLine($"Tax / GST:                      ₹{returnDto.TaxAmount:0.00}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"TOTAL REFUND:                  ₹{returnDto.RefundAmount:0.00}");
        sb.AppendLine($"Refund Mode:                   {returnDto.RefundMode.ToUpper().PadLeft(10)}");
        sb.AppendLine($"Status:                        {returnDto.Status.PadLeft(10)}");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("             அனைத்தும் வாங்க");
        sb.AppendLine("            ஆப்பிளுக்கு வாங்க");
        sb.AppendLine("   Thank you! Please visit again!");

        await _printService.PrintReceiptAsync(printerIp, 9100, sb.ToString());
        return Ok(new { message = "Receipt sent to printer." });
    }

    [HttpGet("credit-monitoring")]
    public async Task<IActionResult> GetCreditMonitoring([FromQuery] Guid? storeId)
    {
        var activeStoreId = storeId ?? Guid.Parse("00000000-0000-0000-0000-000000000000");
        var result = await _mediator.Send(new GetCreditMonitoringQuery(activeStoreId));
        return Ok(result);
    }

    [HttpPost("returns/{id}/cancel")]
    [Authorize(Roles = "Owner,Manager,Developer")]
    public async Task<IActionResult> CancelReturn(Guid id, [FromBody] CancelSalesReturnRequest request)
    {
        var command = new CancelSalesReturnCommand(id, request.Reason);
        var success = await _mediator.Send(command);
        return Ok(new { success });
    }
}

public class CancelSalesReturnRequest
{
    public string? Reason { get; set; }
}

public class ProcessCustomerReceiptRequest
{
    public Guid StoreId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime ReceiptDate { get; set; }
    public string PaymentMode { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public string AllocationMode { get; set; } = "AUTO_FIFO";
    public List<ManualAllocationInputDto>? ManualAllocations { get; set; }
}

public class ProcessSalesReturnRequest
{
    public Guid StoreId { get; set; }
    public Guid InvoiceId { get; set; }
    public DateTime ReturnDate { get; set; }
    public string RefundMode { get; set; } = string.Empty; // CASH, UPI, CREDIT_NOTE
    public List<SalesReturnItemInputDto> Items { get; set; } = new();
    public string? ManagerOverridePin { get; set; }
}
