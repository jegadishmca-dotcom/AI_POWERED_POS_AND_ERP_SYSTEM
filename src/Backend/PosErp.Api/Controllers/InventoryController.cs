using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosErp.Application.Features.Purchasing.Commands.CreateGRN;
using PosErp.Application.Features.Purchasing.Commands.ConfirmGRN;
using PosErp.Application.Features.Inventory.Commands.CreateStockAdjustment;
using PosErp.Application.Features.Inventory.Commands.ApproveStockAdjustment;
using PosErp.Application.Features.Inventory.Queries.GetStockPosition;
using PosErp.Application.Features.Inventory.Queries.GetNearExpiryAlerts;
using System;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventoryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("grn")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreateGRN([FromBody] CreateGRNCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    [HttpPost("grn/{id}/confirm")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> ConfirmGRN(Guid id)
    {
        // For UserId, usually we extract from Claims, here passing null for simplicity as backend supports it
        var result = await _mediator.Send(new ConfirmGRNCommand(id, null));
        return Ok(result);
    }

    [HttpGet("stock-position")]
    public async Task<IActionResult> GetStockPosition([FromQuery] Guid? storeId, [FromQuery] Guid? categoryId, [FromQuery] string? searchToken)
    {
        var result = await _mediator.Send(new GetStockPositionQuery(storeId, categoryId, searchToken));
        return Ok(result);
    }

    [HttpGet("alerts/near-expiry")]
    public async Task<IActionResult> GetNearExpiryAlerts([FromQuery] int daysThreshold = 30)
    {
        var result = await _mediator.Send(new GetNearExpiryAlertsQuery(daysThreshold));
        return Ok(result);
    }

    [HttpPost("stock-adjustment")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreateStockAdjustment([FromBody] CreateStockAdjustmentCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    [HttpPost("stock-adjustment/{id}/approve")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> ApproveStockAdjustment(Guid id)
    {
        var result = await _mediator.Send(new ApproveStockAdjustmentCommand(id, null));
        return Ok(result);
    }
}
