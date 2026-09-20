using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PosErp.Application.Features.Finance.Queries;
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
    private readonly ILogger<FinanceController> _logger;

    public FinanceController(IMediator mediator, ILogger<FinanceController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] Guid? storeId)
    {
        try
        {
            var activeStoreId = storeId ?? Guid.Parse("00000000-0000-0000-0000-000000000000");
            var result = await _mediator.Send(new GetFinanceDashboardQuery(activeStoreId));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading finance dashboard for store {StoreId}", storeId);
            return StatusCode(500, new { message = "Error loading finance dashboard.", detail = ex.Message });
        }
    }
}
