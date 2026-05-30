using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Purchasing.Commands.CreateGRN;
using PosErp.Application.Features.Purchasing.Commands.ConfirmGRN;
using PosErp.Application.Features.Inventory.Commands.CreateStockAdjustment;
using PosErp.Application.Features.Inventory.Commands.ApproveStockAdjustment;
using PosErp.Application.Features.Inventory.Queries.GetStockPosition;
using PosErp.Application.Features.Inventory.Queries.GetNearExpiryAlerts;
using PosErp.Application.Features.Inventory.Queries.GetProductBatches;
using PosErp.Application.Features.Inventory.Commands.CreateOrUpdateStockTake;
using PosErp.Application.Features.Inventory.Commands.ApproveStockTake;
using PosErp.Application.Features.Inventory.Commands.RejectStockTake;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventoryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("grn")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> CreateGRN([FromBody] CreateGRNCommand command)
    {
        try
        {
            var id = await _mediator.Send(command);
            return Ok(new { id });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.ToString() });
        }
    }

    [HttpPost("grn/{id}/confirm")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> ConfirmGRN(Guid id)
    {
        try
        {
            // For UserId, usually we extract from Claims, here passing null for simplicity as backend supports it
            var result = await _mediator.Send(new ConfirmGRNCommand(id, null));
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.ToString() });
        }
    }

    [HttpGet("stock-position")]
    public async Task<IActionResult> GetStockPosition(
        [FromQuery] Guid? storeId, 
        [FromQuery] Guid? categoryId, 
        [FromQuery] string? searchToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new GetStockPositionQuery(storeId, categoryId, searchToken, page, pageSize));
        return Ok(result);
    }

    [HttpGet("ledger")]
    public async Task<IActionResult> GetStockLedger(
        [FromQuery] Guid? storeId, 
        [FromQuery] string? searchToken, 
        [FromQuery] string? movementType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _mediator.Send(new PosErp.Application.Features.Inventory.Queries.GetStockLedger.GetStockLedgerQuery(storeId, searchToken, movementType, page, pageSize));
        return Ok(result);
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromServices] IApplicationDbContext dbContext)
    {
        var categories = await dbContext.Categories
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();
        return Ok(categories);
    }

    [HttpGet("batches")]
    public async Task<IActionResult> GetProductBatches([FromQuery] Guid productId)
    {
        var result = await _mediator.Send(new GetProductBatchesQuery(productId));
        return Ok(result);
    }

    [HttpGet("alerts/near-expiry")]
    public async Task<IActionResult> GetNearExpiryAlerts([FromQuery] int daysThreshold = 30)
    {
        var result = await _mediator.Send(new GetNearExpiryAlertsQuery(daysThreshold));
        return Ok(result);
    }

    [HttpGet("stock-adjustment")]
    public async Task<IActionResult> GetStockAdjustments([FromQuery] Guid? storeId)
    {
        var result = await _mediator.Send(new PosErp.Application.Features.Inventory.Queries.GetStockAdjustments.GetStockAdjustmentsQuery(storeId));
        return Ok(result);
    }

    [HttpGet("stock-adjustment/{id}")]
    public async Task<IActionResult> GetStockAdjustment(Guid id, [FromServices] IApplicationDbContext dbContext)
    {
        var adjustment = await dbContext.StockAdjustments
            .Include(sa => sa.Items)
            .FirstOrDefaultAsync(sa => sa.Id == id);

        if (adjustment == null)
        {
            return NotFound("Stock adjustment not found.");
        }

        var itemsWithProducts = new List<object>();
        foreach (var item in adjustment.Items)
        {
            var product = await dbContext.Products.FindAsync(item.ProductId);
            itemsWithProducts.Add(new
            {
                item.Id,
                item.ProductId,
                ProductName = product?.Name ?? "Unknown Product",
                ProductCode = product?.ProductCode ?? string.Empty,
                item.AdjustedQuantity,
                item.UnitCost
            });
        }

        var approvedByUser = adjustment.ApprovedBy.HasValue 
            ? await dbContext.Users.FindAsync(adjustment.ApprovedBy.Value) 
            : null;

        return Ok(new
        {
            adjustment.Id,
            adjustment.StoreId,
            adjustment.AdjustmentNumber,
            adjustment.Reason,
            adjustment.Status,
            adjustment.CreatedAt,
            ApprovedBy = adjustment.ApprovedBy,
            ApprovedByName = approvedByUser?.FullName ?? string.Empty,
            Items = itemsWithProducts
        });
    }

    [HttpGet("grn/{id}")]
    public async Task<IActionResult> GetGRN(Guid id, [FromServices] IApplicationDbContext dbContext)
    {
        var grn = await dbContext.GRNHeaders
            .Include(g => g.Items)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (grn == null)
        {
            return NotFound("GRN not found.");
        }

        var supplier = await dbContext.Suppliers.FindAsync(grn.SupplierId);
        string supplierName = supplier?.Name ?? "Unknown Supplier";

        var itemsWithProducts = new List<object>();
        foreach (var item in grn.Items)
        {
            var product = await dbContext.Products.FindAsync(item.ProductId);
            itemsWithProducts.Add(new
            {
                item.Id,
                item.ProductId,
                ProductName = product?.Name ?? "Unknown Product",
                ProductCode = product?.ProductCode ?? string.Empty,
                item.BatchNumber,
                item.ExpiryDate,
                item.MfgDate,
                item.ReceivedQuantity,
                item.AcceptedQuantity,
                item.RejectedQuantity,
                item.RejectionReason,
                item.UnitCost,
                item.TotalCost
            });
        }

        return Ok(new
        {
            grn.Id,
            grn.StoreId,
            grn.PurchaseOrderHeaderId,
            grn.SupplierId,
            SupplierName = supplierName,
            grn.GrnNumber,
            grn.SupplierInvoiceNumber,
            grn.ReceivedDate,
            grn.TotalAmount,
            grn.Status,
            grn.CreatedAt,
            Items = itemsWithProducts
        });
    }

    [HttpPost("stock-adjustment")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> CreateStockAdjustment([FromBody] CreateStockAdjustmentCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    [HttpPost("stock-adjustment/{id}/approve")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> ApproveStockAdjustment(Guid id)
    {
        // For security, can extract User ID from claims if present. For simplicity we support null
        var result = await _mediator.Send(new ApproveStockAdjustmentCommand(id, null));
        return Ok(result);
    }

    [HttpPost("stock-adjustment/{id}/reject")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> RejectStockAdjustment(Guid id)
    {
        var result = await _mediator.Send(new PosErp.Application.Features.Inventory.Commands.RejectStockAdjustment.RejectStockAdjustmentCommand(id, null));
        return Ok(result);
    }

    [HttpGet("stock-take")]
    public async Task<IActionResult> GetStockTakes([FromQuery] Guid? storeId)
    {
        var result = await _mediator.Send(new PosErp.Application.Features.Inventory.Queries.GetStockTakes.GetStockTakesQuery(storeId));
        return Ok(result);
    }

    [HttpGet("stock-take/{id}")]
    public async Task<IActionResult> GetStockTakeDetails(Guid id)
    {
        var result = await _mediator.Send(new PosErp.Application.Features.Inventory.Queries.GetStockTakeDetails.GetStockTakeDetailsQuery(id));
        return Ok(result);
    }

    [HttpPost("stock-take")]
    public async Task<IActionResult> CreateOrUpdateStockTake([FromBody] CreateOrUpdateStockTakeCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(new { id });
    }

    [HttpPost("stock-take/{id}/approve")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> ApproveStockTake(Guid id)
    {
        var result = await _mediator.Send(new ApproveStockTakeCommand(id, null));
        return Ok(result);
    }

    [HttpPost("stock-take/{id}/reject")]
    [Authorize(Roles = "Admin,Manager,Owner")]
    public async Task<IActionResult> RejectStockTake(Guid id)
    {
        var result = await _mediator.Send(new RejectStockTakeCommand(id, null));
        return Ok(result);
    }

    [HttpPost("stock-take/parse-csv")]
    public async Task<IActionResult> ParseStockTakeCsv(IFormFile file, [FromServices] IApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var headerLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                return BadRequest("CSV file is empty.");
            }

            var headers = headerLine.Split(',').Select(h => h.Trim().ToLower()).ToList();
            int codeIdx = headers.IndexOf("productcode");
            int barcodeIdx = headers.IndexOf("barcode");
            int batchIdx = headers.IndexOf("batchno");
            if (batchIdx == -1) batchIdx = headers.IndexOf("batchnumber");
            
            int qtyIdx = headers.IndexOf("physicalcount");
            if (qtyIdx == -1) qtyIdx = headers.IndexOf("physicalquantity");
            if (qtyIdx == -1) qtyIdx = headers.IndexOf("quantity");

            if ((codeIdx == -1 && barcodeIdx == -1) || qtyIdx == -1)
            {
                return BadRequest("CSV missing required headers. Required: ProductCode or Barcode, and PhysicalCount/Quantity.");
            }

            var resolvedLines = new List<object>();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = ParseCsvLine(line);
                if (values.Count == 0) continue;

                Product? product = null;
                if (barcodeIdx != -1 && barcodeIdx < values.Count && !string.IsNullOrWhiteSpace(values[barcodeIdx]))
                {
                    var barcodeVal = values[barcodeIdx];
                    product = await dbContext.Products
                        .Include(p => p.Barcodes)
                        .FirstOrDefaultAsync(p => !p.IsDeleted && p.IsActive && p.Barcodes.Any(b => b.BarcodeValue == barcodeVal), cancellationToken);
                }

                if (product == null && codeIdx != -1 && codeIdx < values.Count && !string.IsNullOrWhiteSpace(values[codeIdx]))
                {
                    var codeVal = values[codeIdx];
                    product = await dbContext.Products
                        .Include(p => p.Barcodes)
                        .FirstOrDefaultAsync(p => !p.IsDeleted && p.IsActive && p.ProductCode == codeVal, cancellationToken);
                }

                if (product == null) continue;

                ProductBatch? batch = null;
                string parsedBatchNo = "";
                if (batchIdx != -1 && batchIdx < values.Count && !string.IsNullOrWhiteSpace(values[batchIdx]))
                {
                    parsedBatchNo = values[batchIdx];
                    batch = await dbContext.ProductBatches
                        .FirstOrDefaultAsync(b => b.ProductId == product.Id && b.BatchNumber == parsedBatchNo && b.IsActive, cancellationToken);
                }

                if (batch == null)
                {
                    var activeBatches = await dbContext.ProductBatches
                        .Where(b => b.ProductId == product.Id && b.IsActive)
                        .ToListAsync(cancellationToken);
                    
                    if (!string.IsNullOrEmpty(parsedBatchNo) && activeBatches.Any())
                    {
                        batch = activeBatches.FirstOrDefault(b => b.BatchNumber.Equals(parsedBatchNo, StringComparison.OrdinalIgnoreCase)) ?? activeBatches.FirstOrDefault();
                    }
                    else
                    {
                        batch = activeBatches.FirstOrDefault();
                    }
                }

                decimal physicalQty = 0;
                if (qtyIdx < values.Count)
                {
                    decimal.TryParse(values[qtyIdx], out physicalQty);
                }

                decimal systemQty = 0;
                if (batch != null)
                {
                    systemQty = await dbContext.StockLedger
                        .Where(sl => sl.ProductId == product.Id && sl.BatchId == batch.Id)
                        .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;
                }
                else
                {
                    systemQty = await dbContext.StockLedger
                        .Where(sl => sl.ProductId == product.Id && sl.BatchId == null)
                        .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;
                }

                resolvedLines.Add(new
                {
                    productId = product.Id,
                    productName = product.Name,
                    batchId = batch?.Id ?? Guid.Empty,
                    batchNumber = batch?.BatchNumber ?? "NO BATCH",
                    systemQuantity = systemQty,
                    physicalQuantity = physicalQty
                });
            }

            return Ok(resolvedLines);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"CSV parse fail: {ex.Message}" });
        }
    }

    [HttpPost("stock-adjustment/parse-csv")]
    public async Task<IActionResult> ParseStockAdjustmentCsv(IFormFile file, [FromServices] IApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        try
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var headerLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                return BadRequest("CSV file is empty.");
            }

            var headers = headerLine.Split(',').Select(h => h.Trim().ToLower()).ToList();
            int codeIdx = headers.IndexOf("productcode");
            int barcodeIdx = headers.IndexOf("barcode");
            int batchIdx = headers.IndexOf("batchno");
            if (batchIdx == -1) batchIdx = headers.IndexOf("batchnumber");
            
            int qtyIdx = headers.IndexOf("adjustedqty");
            if (qtyIdx == -1) qtyIdx = headers.IndexOf("adjustedquantity");
            if (qtyIdx == -1) qtyIdx = headers.IndexOf("quantity");

            int costIdx = headers.IndexOf("unitcost");
            if (costIdx == -1) costIdx = headers.IndexOf("cost");

            if ((codeIdx == -1 && barcodeIdx == -1) || qtyIdx == -1)
            {
                return BadRequest("CSV missing required headers. Required: ProductCode or Barcode, and AdjustedQty/Quantity.");
            }

            var resolvedLines = new List<object>();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = ParseCsvLine(line);
                if (values.Count == 0) continue;

                Product? product = null;
                if (barcodeIdx != -1 && barcodeIdx < values.Count && !string.IsNullOrWhiteSpace(values[barcodeIdx]))
                {
                    var barcodeVal = values[barcodeIdx];
                    product = await dbContext.Products
                        .Include(p => p.Barcodes)
                        .FirstOrDefaultAsync(p => !p.IsDeleted && p.IsActive && p.Barcodes.Any(b => b.BarcodeValue == barcodeVal), cancellationToken);
                }

                if (product == null && codeIdx != -1 && codeIdx < values.Count && !string.IsNullOrWhiteSpace(values[codeIdx]))
                {
                    var codeVal = values[codeIdx];
                    product = await dbContext.Products
                        .Include(p => p.Barcodes)
                        .FirstOrDefaultAsync(p => !p.IsDeleted && p.IsActive && p.ProductCode == codeVal, cancellationToken);
                }

                if (product == null) continue;

                ProductBatch? batch = null;
                string parsedBatchNo = "";
                if (batchIdx != -1 && batchIdx < values.Count && !string.IsNullOrWhiteSpace(values[batchIdx]))
                {
                    parsedBatchNo = values[batchIdx];
                    batch = await dbContext.ProductBatches
                        .FirstOrDefaultAsync(b => b.ProductId == product.Id && b.BatchNumber == parsedBatchNo && b.IsActive, cancellationToken);
                }

                if (batch == null)
                {
                    var activeBatches = await dbContext.ProductBatches
                        .Where(b => b.ProductId == product.Id && b.IsActive)
                        .ToListAsync(cancellationToken);
                    
                    if (!string.IsNullOrEmpty(parsedBatchNo) && activeBatches.Any())
                    {
                        batch = activeBatches.FirstOrDefault(b => b.BatchNumber.Equals(parsedBatchNo, StringComparison.OrdinalIgnoreCase)) ?? activeBatches.FirstOrDefault();
                    }
                    else
                    {
                        batch = activeBatches.FirstOrDefault();
                    }
                }

                decimal adjustedQty = 0;
                if (qtyIdx < values.Count)
                {
                    decimal.TryParse(values[qtyIdx], out adjustedQty);
                }

                decimal unitCost = 0;
                if (costIdx != -1 && costIdx < values.Count)
                {
                    decimal.TryParse(values[costIdx], out unitCost);
                }

                if (unitCost == 0)
                {
                    unitCost = batch != null && batch.CostPrice > 0 ? batch.CostPrice : product.PurchasePrice;
                }

                decimal currentStock = 0;
                if (batch != null)
                {
                    currentStock = await dbContext.StockLedger
                        .Where(sl => sl.ProductId == product.Id && sl.BatchId == batch.Id)
                        .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;
                }
                else
                {
                    currentStock = await dbContext.StockLedger
                        .Where(sl => sl.ProductId == product.Id && sl.BatchId == null)
                        .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;
                }

                resolvedLines.Add(new
                {
                    productId = product.Id,
                    productName = product.Name,
                    batchId = batch?.Id ?? Guid.Empty,
                    batchNumber = batch?.BatchNumber ?? "NO BATCH",
                    adjustedQuantity = adjustedQty,
                    unitCost = unitCost,
                    currentStock = currentStock
                });
            }

            return Ok(resolvedLines);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"CSV parse fail: {ex.Message}" });
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var currentToken = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(currentToken.ToString().Trim(' ', '"'));
                currentToken.Clear();
            }
            else
            {
                currentToken.Append(c);
            }
        }
        result.Add(currentToken.ToString().Trim(' ', '"'));
        return result;
    }
}
