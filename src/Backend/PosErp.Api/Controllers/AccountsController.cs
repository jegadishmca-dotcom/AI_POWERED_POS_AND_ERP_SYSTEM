using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosErp.Application.Features.Finance.Commands;
using PosErp.Application.Features.Finance.Queries;
using System;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAccounts([FromQuery] bool onlyActive = true, [FromQuery] bool buildTree = true)
    {
        var result = await _mediator.Send(new GetAccountsQuery(onlyActive, buildTree));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountCommand command)
    {
        try
        {
            var id = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetAccounts), new { id }, new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] UpdateAccountCommand command)
    {
        if (id != command.Id)
            return BadRequest(new { message = "ID mismatch in URL and body." });

        try
        {
            var success = await _mediator.Send(command);
            if (!success) return NotFound();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/toggle")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> ToggleAccountStatus(Guid id, [FromQuery] bool isActive)
    {
        try
        {
            var success = await _mediator.Send(new ToggleAccountStatusCommand(id, isActive));
            if (!success) return NotFound();
            return Ok(new { success, isActive });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
