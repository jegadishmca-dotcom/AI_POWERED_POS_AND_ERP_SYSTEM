using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Features.Purchasing.Commands.CreatePurchaseOrder;
using PosErp.Application.Features.Purchasing.Queries.GetPurchaseOrders;
using PosErp.Application.Features.Purchasing.Queries.GetPurchaseOrderById;
using PosErp.Application.Features.Purchasing.Commands.ApprovePurchaseOrder;
using PosErp.Application.Features.Purchasing.Commands.UpdatePurchaseOrder;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
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

    [HttpPost("purchase-orders/auto-generate-reorder")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> AutoGenerateReorderPurchaseOrders([FromServices] IApplicationDbContext context)
    {
        var lowStockProducts = await context.Products
            .Where(p => !p.IsDeleted && p.IsActive)
            .ToListAsync();

        var batches = await context.ProductBatches.Where(b => b.IsActive).ToListAsync();
        var defaultSupplier = await context.Suppliers.FirstOrDefaultAsync(s => s.IsActive);
        
        if (defaultSupplier == null)
        {
            defaultSupplier = new PosErp.Domain.Entities.Purchasing.Supplier 
            { 
                Id = Guid.NewGuid(), 
                Name = "PRIMARY WHOLESALE SUPPLIER", 
                PaymentTerms = "NET30",
                IsActive = true
            };
            context.Suppliers.Add(defaultSupplier);
            await context.SaveChangesAsync(default);
        }

        var itemsToOrder = new List<PurchaseOrderItemDto>();
        int poGeneratedCount = 0;

        foreach (var p in lowStockProducts)
        {
            var currentStock = batches.Where(b => b.ProductId == p.Id).Sum(b => b.AvailableQuantity);
            var reorderThreshold = p.ReorderPoint > 0 ? p.ReorderPoint : 10m;

            if (currentStock <= reorderThreshold)
            {
                var orderQty = Math.Max((reorderThreshold * 2m) - currentStock, 10m);
                var unitCost = p.PurchasePrice > 0 ? p.PurchasePrice : (p.SellingPrice * 0.75m);
                itemsToOrder.Add(new PurchaseOrderItemDto(p.Id, orderQty, unitCost));
            }
        }

        if (itemsToOrder.Count > 0)
        {
            var itemGroups = itemsToOrder
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / 20)
                .Select(g => g.Select(x => x.item).ToList())
                .ToList();

            foreach (var group in itemGroups)
            {
                var poCommand = new CreatePurchaseOrderCommand(
                    null,
                    defaultSupplier.Id,
                    DateTime.UtcNow.AddDays(3),
                    group,
                    null
                );

                await _mediator.Send(poCommand);
                poGeneratedCount++;
            }
        }

        return Ok(new
        {
            Success = true,
            PoCount = poGeneratedCount,
            TotalItemsOrdered = itemsToOrder.Count,
            Message = itemsToOrder.Count > 0 
                ? $"Successfully auto-generated {poGeneratedCount} Purchase Orders for {itemsToOrder.Count} low-stock items." 
                : "All items are currently above reorder threshold. No new POs needed."
        });
    }

    [HttpPost("purchase-orders/{id}/approve")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> ApprovePurchaseOrder(Guid id)
    {
        var callerIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        Guid? callerId = null;
        if (Guid.TryParse(callerIdStr, out var parsedId))
        {
            callerId = parsedId;
        }

        var result = await _mediator.Send(new ApprovePurchaseOrderCommand(id, callerId));
        return Ok(result);
    }

    [HttpPut("purchase-orders/{id}")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> UpdatePurchaseOrder(Guid id, [FromBody] UpdatePurchaseOrderCommand command)
    {
        if (id != command.PurchaseOrderId)
            return BadRequest("Id in URL does not match PurchaseOrderId in body.");

        var result = await _mediator.Send(command);
        return Ok(result);
    }
}
