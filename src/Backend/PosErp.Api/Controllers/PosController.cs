using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using PosErp.Application.Features.Pos.Commands.SyncInvoices;
using PosErp.Infrastructure.Printing;
using System;
using PosErp.Application.Features.Pos.Queries.GetZReport;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPrintService _printService;

    public PosController(IMediator mediator, IPrintService printService)
    {
        _mediator = mediator;
        _printService = printService;
    }

    [HttpGet("z-report")]
    public async Task<IActionResult> GetZReport([FromQuery] Guid terminalId, [FromQuery] DateTime businessDate)
    {
        return Ok(await _mediator.Send(new GetZReportQuery(terminalId, businessDate)));
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] SyncInvoicesCommand command)
    {
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceCommand command)
    {
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("session/open")]
    public async Task<IActionResult> OpenSession([FromBody] PosErp.Application.Features.Pos.Commands.CreatePosSessionCommand command)
    {
        return Ok(await _mediator.Send(command));
    }

    [HttpGet("session/current")]
    public async Task<IActionResult> GetCurrentSession([FromQuery] Guid terminalId, [FromQuery] Guid cashierId)
    {
        var session = await _mediator.Send(new PosErp.Application.Features.Pos.Queries.GetActivePosSessionQuery(terminalId, cashierId));
        return Ok(session);
    }

    [HttpPost("session/close")]
    public async Task<IActionResult> CloseSession([FromBody] PosErp.Application.Features.Pos.Commands.ClosePosSessionCommand command)
    {
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("print/{invoiceId}")]
    public async Task<IActionResult> PrintReceipt(Guid invoiceId)
    {
        // Fetch invoice from DB using MediatR query (assumed)
        // Convert to ESC/POS bytes
        // Send to Printer IP
        await _printService.PrintReceiptAsync("192.168.1.100", 9100, "Receipt Content Placeholder for " + invoiceId);
        return Ok();
    }
}
