using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosErp.Application.Features.Finance.Queries;
using PosErp.Application.Features.Finance.Commands;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Manager,Owner")]
public class FinanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public FinanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] Guid? storeId)
    {
        var activeStoreId = storeId ?? Guid.Parse("00000000-0000-0000-0000-000000000000");
        var result = await _mediator.Send(new GetFinanceDashboardQuery(activeStoreId));
        return Ok(result);
    }

    [HttpPost("transfers")]
    public async Task<IActionResult> ProcessTransfer([FromBody] ProcessInterStoreTransferCommand command)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(callerIdStr, out Guid userId))
        {
            return Unauthorized("User ID claim not found.");
        }
        var updatedCommand = new ProcessInterStoreTransferCommand(
            command.FromStoreId,
            command.ToStoreId,
            command.TransferDate,
            command.Items,
            userId
        );
        var id = await _mediator.Send(updatedCommand);
        return Ok(new { id });
    }
}
