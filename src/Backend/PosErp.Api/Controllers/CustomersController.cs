using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MediatR;
using System.Threading.Tasks;
using PosErp.Application.Features.Crm.Commands.RegisterCustomer;
using PosErp.Application.Features.Crm.Queries.SearchCustomers;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CustomersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        var result = await _mediator.Send(new SearchCustomersQuery(q ?? string.Empty));
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterCustomerCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    public class MergeRequest { public System.Guid TargetCustomerId { get; set; } }

    [HttpPost("{id}/merge")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> Merge(System.Guid id, [FromBody] MergeRequest req)
    {
        var result = await _mediator.Send(new PosErp.Application.Features.Crm.Commands.MergeCustomersCommand(id, req.TargetCustomerId));
        return Ok(result);
    }
}
