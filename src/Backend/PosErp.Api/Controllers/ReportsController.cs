using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosErp.Application.Features.Reports.Queries.GetGSTReport;
using PosErp.Application.Features.Reports.Queries.GetMarginReport;
using PosErp.Application.Features.Reports.Queries.GetInventoryInsights;
using System;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Manager,Owner")] // Owner/Manager roles only can view financial reports
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("gst")]
    public async Task<IActionResult> GetGSTReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var result = await _mediator.Send(new GetGSTReportQuery(fromDate, toDate));
        return Ok(result);
    }

    [HttpGet("margin")]
    public async Task<IActionResult> GetMarginReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var result = await _mediator.Send(new GetMarginReportQuery(fromDate, toDate));
        return Ok(result);
    }

    [HttpGet("inventory-insights")]
    public async Task<IActionResult> GetInventoryInsights()
    {
        var result = await _mediator.Send(new GetInventoryInsightsQuery());
        return Ok(result);
    }
}
