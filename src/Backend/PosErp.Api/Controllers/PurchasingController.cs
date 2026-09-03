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
        var allSuppliers = await context.Suppliers.Where(s => s.IsActive).ToListAsync();
        
        // Idempotency check: Exclude products that already have open, draft, or partially-received POs
        var pendingProductIds = await context.PurchaseOrderItems
            .Where(poi => poi.PurchaseOrderHeader.Status != "FULL_GRN" && poi.PurchaseOrderHeader.Status != "CANCELLED")
            .Select(poi => poi.ProductId)
            .Distinct()
            .ToListAsync();
        var pendingProductSet = new HashSet<Guid>(pendingProductIds);

        var defaultSupplier = allSuppliers.FirstOrDefault();
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
            allSuppliers.Add(defaultSupplier);
        }

        // PERMANENT SAFETY LIMIT:
        // An interactive HTTP endpoint must never issue hundreds of synchronous database writes in one call.
        // Full store replenishment across thousands of catalog items must be handled via a background queue.
        // Cap interactive auto-generation to a safe operational batch: max 3 POs (up to 90 items).
        const int MaxPoPerInteractiveRun = 3;
        const int MaxItemsPerPo = 30;
        const int MaxTotalItemsPerRun = MaxPoPerInteractiveRun * MaxItemsPerPo;

        // Evaluate low stock candidates, prioritizing:
        // 1. Items with depleted active stock (currentStock > 0) before never-stocked catalog items (currentStock == 0)
        // 2. Items with a designated PreferredSupplier
        // 3. Lowest stock first
        var candidates = lowStockProducts
            .Where(p => !pendingProductSet.Contains(p.Id))
            .Select(p =>
            {
                var currentStock = batches.Where(b => b.ProductId == p.Id).Sum(b => b.AvailableQuantity);
                var reorderThreshold = p.ReorderPoint > 0 ? p.ReorderPoint : 10m;
                return new 
                { 
                    Product = p, 
                    CurrentStock = currentStock, 
                    ReorderThreshold = reorderThreshold,
                    IsLowStock = currentStock <= reorderThreshold
                };
            })
            .Where(x => x.IsLowStock)
            .OrderByDescending(x => x.CurrentStock > 0) // Depleted active stock first
            .ThenByDescending(x => x.Product.PreferredSupplierId != null) // Preferred supplier first
            .ThenBy(x => x.CurrentStock) // Lowest stock first
            .Take(MaxTotalItemsPerRun)
            .ToList();

        var itemsBySupplier = new Dictionary<Guid, List<PurchaseOrderItemDto>>();
        int totalItemsOrderedCount = 0;

        foreach (var c in candidates)
        {
            var p = c.Product;
            var orderQty = Math.Max((c.ReorderThreshold * 2m) - c.CurrentStock, 10m);
            var unitCost = p.PurchasePrice > 0 ? p.PurchasePrice : (p.SellingPrice * 0.75m);
            var itemDto = new PurchaseOrderItemDto(p.Id, orderQty, unitCost);

            var targetSupplierId = (p.PreferredSupplierId != null && allSuppliers.Any(s => s.Id == p.PreferredSupplierId))
                ? p.PreferredSupplierId.Value
                : defaultSupplier.Id;

            if (!itemsBySupplier.ContainsKey(targetSupplierId))
            {
                itemsBySupplier[targetSupplierId] = new List<PurchaseOrderItemDto>();
            }
            itemsBySupplier[targetSupplierId].Add(itemDto);
            totalItemsOrderedCount++;
        }

        int poGeneratedCount = 0;
        foreach (var entry in itemsBySupplier)
        {
            if (poGeneratedCount >= MaxPoPerInteractiveRun)
            {
                break;
            }

            var supplierId = entry.Key;
            var supplierItems = entry.Value;

            var itemChunks = supplierItems
                .Select((item, index) => new { item, index })
                .GroupBy(x => x.index / MaxItemsPerPo)
                .Select(g => g.Select(x => x.item).ToList())
                .ToList();

            foreach (var chunk in itemChunks)
            {
                if (poGeneratedCount >= MaxPoPerInteractiveRun)
                {
                    break;
                }

                var poCommand = new CreatePurchaseOrderCommand(
                    null,
                    supplierId,
                    DateTime.UtcNow.AddDays(3),
                    chunk,
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
            TotalItemsOrdered = totalItemsOrderedCount,
            Message = totalItemsOrderedCount > 0 
                ? $"Successfully auto-generated {poGeneratedCount} Purchase Orders across {itemsBySupplier.Count} vendors for {totalItemsOrderedCount} low-stock items." 
                : "All items are currently above reorder threshold or already have open purchase orders. No new POs needed."
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
