using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PosErp.Application.Features.Purchasing.Commands.CreatePurchaseOrder;
using PosErp.Application.Features.Purchasing.Queries.GetPurchaseOrders;
using PosErp.Application.Features.Purchasing.Queries.GetPurchaseOrderById;
using PosErp.Application.Features.Purchasing.Commands.ApprovePurchaseOrder;
using System;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Ensure all endpoints are protected
public class PurchasingController : ControllerBase
{
    private readonly IMediator _mediator;

    public PurchasingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("purchase-orders")]
    public async Task<IActionResult> GetPurchaseOrders()
    {
        var result = await _mediator.Send(new GetPurchaseOrdersQuery());
        return Ok(result);
    }

    [HttpGet("purchase-orders/{id}")]
    public async Task<IActionResult> GetPurchaseOrderById(Guid id)
    {
        var result = await _mediator.Send(new GetPurchaseOrderByIdQuery { Id = id });
        
        if (result == null)
            return NotFound();
            
        return Ok(result);
    }

    [HttpPost("purchase-orders")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePurchaseOrderCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetPurchaseOrderById), new { id }, new { id });
    }

    [HttpPost("purchase-orders/{id}/approve")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> ApprovePurchaseOrder(Guid id)
    {
        var result = await _mediator.Send(new ApprovePurchaseOrderCommand(id, null));
        return Ok(result);
    }
}
