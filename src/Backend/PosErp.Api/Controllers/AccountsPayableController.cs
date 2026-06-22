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
[Authorize]
public class AccountsPayableController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountsPayableController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("bills")]
    public async Task<IActionResult> CreateBill([FromBody] CreatePurchaseBillRequest request)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(callerIdStr, out Guid userId);

        var command = new CreatePurchaseBillCommand(
            request.StoreId,
            request.GRNHeaderId,
            request.BillNumber,
            request.BillDate,
            userId
        );

        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    [HttpPost("payments")]
    public async Task<IActionResult> ProcessPayment([FromBody] ProcessSupplierPaymentRequest request)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(callerIdStr, out Guid userId);

        var command = new ProcessSupplierPaymentCommand(
            request.StoreId,
            request.SupplierId,
            request.PaymentDate,
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
    public async Task<IActionResult> ProcessReturn([FromBody] ProcessPurchaseReturnRequest request)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(callerIdStr, out Guid userId);

        var command = new ProcessPurchaseReturnCommand(
            request.StoreId,
            request.SupplierId,
            request.GRNHeaderId,
            request.ReturnDate,
            request.Items,
            userId
        );

        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    [HttpGet("ledger")]
    public async Task<IActionResult> GetLedger([FromQuery] Guid supplierId, [FromQuery] Guid storeId)
    {
        var result = await _mediator.Send(new GetSupplierLedgerQuery(supplierId, storeId));
        return Ok(result);
    }

    [HttpGet("aging")]
    public async Task<IActionResult> GetAging([FromQuery] Guid storeId, [FromQuery] DateTime asOfDate)
    {
        var result = await _mediator.Send(new GetSupplierAgingReportQuery(storeId, asOfDate));
        return Ok(result);
    }
}

public class CreatePurchaseBillRequest
{
    public Guid StoreId { get; set; }
    public Guid GRNHeaderId { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
}

public class ProcessSupplierPaymentRequest
{
    public Guid StoreId { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime PaymentDate { get; set; }
    public string PaymentMode { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
    public string AllocationMode { get; set; } = "AUTO_FIFO";
    public List<ManualAllocationInputDto>? ManualAllocations { get; set; }
}

public class ProcessPurchaseReturnRequest
{
    public Guid StoreId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid? GRNHeaderId { get; set; }
    public DateTime ReturnDate { get; set; }
    public List<PurchaseReturnItemInputDto> Items { get; set; } = new();
}
