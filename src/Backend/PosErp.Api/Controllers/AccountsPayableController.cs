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
public class AccountsPayableController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly Microsoft.Extensions.Logging.ILogger<AccountsPayableController> _logger;

    public AccountsPayableController(IMediator mediator, Microsoft.Extensions.Logging.ILogger<AccountsPayableController> logger)
    {
        _mediator = mediator;
        _logger = logger;
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
        try
        {
            var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid.TryParse(callerIdStr, out Guid userId);

            var storeId = request.StoreId != Guid.Empty ? request.StoreId : Guid.Parse("00000000-0000-0000-0000-000000000000");

            var command = new ProcessSupplierPaymentCommand(
                storeId,
                request.SupplierId,
                request.PaymentDate,
                request.PaymentMode,
                request.ReferenceNumber,
                request.Amount,
                request.Notes,
                string.IsNullOrEmpty(request.AllocationMode) ? "AUTO_FIFO" : request.AllocationMode,
                request.ManualAllocations,
                userId
            );

            var id = await _mediator.Send(command);
            return Ok(new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing supplier payment for supplier {SupplierId}", request.SupplierId);
            return BadRequest(new { message = ex.Message });
        }
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

    [HttpGet("bills")]
    public async Task<IActionResult> GetBills([FromQuery] Guid? storeId, [FromQuery] Guid? supplierId)
    {
        try
        {
            var result = await _mediator.Send(new GetPurchaseBillsQuery(storeId, supplierId));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching supplier bills for store {StoreId}, supplier {SupplierId}", storeId, supplierId);
            return StatusCode(500, new { message = "Error loading supplier bills.", detail = ex.Message });
        }
    }

    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments([FromQuery] Guid? storeId, [FromQuery] Guid? supplierId)
    {
        try
        {
            var result = await _mediator.Send(new GetSupplierPaymentsQuery(storeId, supplierId));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching supplier payments for store {StoreId}, supplier {SupplierId}", storeId, supplierId);
            return StatusCode(500, new { message = "Error loading supplier payments.", detail = ex.Message });
        }
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
