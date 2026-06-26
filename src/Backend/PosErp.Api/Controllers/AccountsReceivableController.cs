using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosErp.Application.Features.Finance.Commands;
using PosErp.Application.Features.Finance.Services;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager,Owner")]
public class AccountsReceivableController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountsReceivableController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("receipts")]
    public async Task<IActionResult> ProcessReceipt([FromBody] ProcessCustomerReceiptRequest request)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(callerIdStr, out Guid userId);

        var command = new ProcessCustomerReceiptCommand(
            request.StoreId,
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

    [HttpPost("returns")]
    public async Task<IActionResult> ProcessReturn([FromBody] ProcessSalesReturnRequest request)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(callerIdStr, out Guid userId);

        var command = new ProcessSalesReturnCommand(
            request.StoreId,
            request.InvoiceId,
            request.ReturnDate,
            request.RefundMode,
            request.Items,
            userId
        );

        var id = await _mediator.Send(command);
        return Ok(new { id });
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

    [HttpGet("credit-monitoring")]
    public async Task<IActionResult> GetCreditMonitoring([FromQuery] Guid? storeId)
    {
        var activeStoreId = storeId ?? Guid.Parse("00000000-0000-0000-0000-000000000000");
        var result = await _mediator.Send(new GetCreditMonitoringQuery(activeStoreId));
        return Ok(result);
    }
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
}
