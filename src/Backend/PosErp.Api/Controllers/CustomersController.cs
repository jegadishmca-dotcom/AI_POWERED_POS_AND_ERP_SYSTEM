using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using PosErp.Application.Features.Crm.Commands.RegisterCustomer;
using PosErp.Application.Features.Crm.Queries.SearchCustomers;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
}
