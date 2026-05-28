using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosErp.Application.Features.Purchasing.Commands.CreateGRN;
using PosErp.Application.Features.Purchasing.Commands.ConfirmGRN;
using PosErp.Application.Features.Inventory.Commands.CreateStockAdjustment;
using PosErp.Application.Features.Inventory.Commands.ApproveStockAdjustment;
using PosErp.Application.Features.Inventory.Queries.GetStockPosition;
using PosErp.Application.Features.Inventory.Queries.GetNearExpiryAlerts;
using PosErp.Application.Features.Inventory.Queries.GetProductBatches;
using PosErp.Application.Features.Inventory.Commands.CreateOrUpdateStockTake;
using PosErp.Application.Features.Inventory.Commands.ApproveStockTake;
using PosErp.Application.Features.Inventory.Commands.RejectStockTake;
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
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> CreateGRN([FromBody] CreateGRNCommand command)
    {
        try
        {
            var id = await _mediator.Send(command);
            return Ok(new { id });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.ToString() });
        }
    }

    [HttpPost("grn/{id}/confirm")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> ConfirmGRN(Guid id)
    {
        try
        {
            // For UserId, usually we extract from Claims, here passing null for simplicity as backend supports it
            var result = await _mediator.Send(new ConfirmGRNCommand(id, null));
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.ToString() });
        }
    }

    [HttpGet("stock-position")]
    public async Task<IActionResult> GetStockPosition([FromQuery] Guid? storeId, [FromQuery] Guid? categoryId, [FromQuery] string? searchToken)
    {
        var result = await _mediator.Send(new GetStockPositionQuery(storeId, categoryId, searchToken));
        return Ok(result);
    }

    [HttpGet("ledger")]
    public async Task<IActionResult> GetStockLedger([FromQuery] Guid? storeId, [FromQuery] string? searchToken, [FromQuery] string? movementType)
    {
        var result = await _mediator.Send(new PosErp.Application.Features.Inventory.Queries.GetStockLedger.GetStockLedgerQuery(storeId, searchToken, movementType));
        return Ok(result);
    }

    [HttpGet("batches")]
    public async Task<IActionResult> GetProductBatches([FromQuery] Guid productId)
    {
        var result = await _mediator.Send(new GetProductBatchesQuery(productId));
        return Ok(result);
    }

    [HttpGet("alerts/near-expiry")]
    public async Task<IActionResult> GetNearExpiryAlerts([FromQuery] int daysThreshold = 30)
    {
        var result = await _mediator.Send(new GetNearExpiryAlertsQuery(daysThreshold));
        return Ok(result);
    }

    [HttpGet("stock-adjustment")]
    public async Task<IActionResult> GetStockAdjustments([FromQuery] Guid? storeId)
    {
        var result = await _mediator.Send(new PosErp.Application.Features.Inventory.Queries.GetStockAdjustments.GetStockAdjustmentsQuery(storeId));
        return Ok(result);
    }

    [HttpPost("stock-adjustment")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> CreateStockAdjustment([FromBody] CreateStockAdjustmentCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    [HttpPost("stock-adjustment/{id}/approve")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> ApproveStockAdjustment(Guid id)
    {
        // For security, can extract User ID from claims if present. For simplicity we support null
        var result = await _mediator.Send(new ApproveStockAdjustmentCommand(id, null));
        return Ok(result);
    }

    [HttpPost("stock-adjustment/{id}/reject")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> RejectStockAdjustment(Guid id)
    {
        var result = await _mediator.Send(new PosErp.Application.Features.Inventory.Commands.RejectStockAdjustment.RejectStockAdjustmentCommand(id, null));
        return Ok(result);
    }

    [HttpGet("stock-take")]
    public async Task<IActionResult> GetStockTakes([FromQuery] Guid? storeId)
    {
        var result = await _mediator.Send(new PosErp.Application.Features.Inventory.Queries.GetStockTakes.GetStockTakesQuery(storeId));
        return Ok(result);
    }

    [HttpGet("stock-take/{id}")]
    public async Task<IActionResult> GetStockTakeDetails(Guid id)
    {
        var result = await _mediator.Send(new PosErp.Application.Features.Inventory.Queries.GetStockTakeDetails.GetStockTakeDetailsQuery(id));
        return Ok(result);
    }

    [HttpPost("stock-take")]
    public async Task<IActionResult> CreateOrUpdateStockTake([FromBody] CreateOrUpdateStockTakeCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    [HttpPost("stock-take/{id}/approve")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> ApproveStockTake(Guid id)
    {
        var result = await _mediator.Send(new ApproveStockTakeCommand(id, null));
        return Ok(result);
    }

    [HttpPost("stock-take/{id}/reject")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> RejectStockTake(Guid id)
    {
        var result = await _mediator.Send(new RejectStockTakeCommand(id, null));
        return Ok(result);
    }

    [HttpGet("debug-logs")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDebugLogs()
    {
        if (!System.IO.File.Exists("ef_sql.log"))
        {
            return Ok("No logs found.");
        }
        var logs = await System.IO.File.ReadAllTextAsync("ef_sql.log");
        return Content(logs, "text/plain");
    }

    [HttpDelete("debug-logs")]
    [AllowAnonymous]
    public IActionResult DeleteDebugLogs()
    {
        if (System.IO.File.Exists("ef_sql.log"))
        {
            System.IO.File.Delete("ef_sql.log");
            return Ok("Logs deleted.");
        }
        return Ok("No logs existed.");
    }
}
