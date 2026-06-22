using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosErp.Application.Features.Finance.Commands;
using PosErp.Application.Features.Finance.Queries;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JournalEntriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public JournalEntriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetJournals(
        [FromQuery] Guid? storeId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? status)
    {
        var result = await _mediator.Send(new GetJournalEntriesQuery(storeId, startDate, endDate, status));
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetJournalById(Guid id)
    {
        var result = await _mediator.Send(new GetJournalEntryByIdQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDraft([FromBody] CreateJournalCommand command)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid.TryParse(callerIdStr, out Guid userId);

        try
        {
            var updatedCommand = command with { UserId = userId };
            var id = await _mediator.Send(updatedCommand);
            return CreatedAtAction(nameof(GetJournalById), new { id }, new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/post")]
    public async Task<IActionResult> PostJournal(Guid id)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(callerIdStr, out Guid userId))
        {
            return Unauthorized("User ID claim not found.");
        }

        try
        {
            var result = await _mediator.Send(new PostJournalEntryCommand(id, userId));
            return Ok(new { status = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/reverse")]
    public async Task<IActionResult> ReverseJournal(Guid id)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(callerIdStr, out Guid userId))
        {
            return Unauthorized("User ID claim not found.");
        }

        try
        {
            var newId = await _mediator.Send(new ReverseJournalEntryCommand(id, userId));
            return Ok(new { id = newId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> VoidJournal(Guid id)
    {
        try
        {
            var success = await _mediator.Send(new VoidJournalEntryCommand(id));
            if (!success) return NotFound();
            return Ok(new { message = "Journal entry voided successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("approvals/pending")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> GetPendingApprovals([FromQuery] Guid? storeId)
    {
        var result = await _mediator.Send(new GetPendingApprovalsQuery(storeId));
        return Ok(result);
    }

    [HttpPost("approvals/{id}/action")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> ActionApproval(Guid id, [FromBody] ActionStepInput input)
    {
        var callerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(callerIdStr, out Guid userId))
        {
            return Unauthorized("User ID claim not found.");
        }

        try
        {
            var fullyApproved = await _mediator.Send(new ApproveJournalStepCommand(id, userId, input.Comments));
            return Ok(new { fullyApproved });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public class ActionStepInput
{
    public string? Comments { get; set; }
}
