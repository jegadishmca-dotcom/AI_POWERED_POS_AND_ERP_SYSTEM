using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Catalog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Catalog.Commands.CreateProduct;

public record CreateProductCommand(
    string ProductCode,
    string Name,
    string? TamilName,
    string? Description,
    decimal Mrp,
    decimal SellingPrice,
    decimal PurchasePrice,
    string BarcodeValue,
    Guid? TaxSlabId,
    Guid? CategoryId,
    Guid? UnitOfMeasureId
) : IRequest<Guid>;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        // 1. Get requested Tax Slab or default to first if not provided
        PosErp.Domain.Entities.Catalog.TaxSlab? taxSlab = null;
        if (request.TaxSlabId.HasValue)
        {
            taxSlab = await _context.TaxSlabs.FirstOrDefaultAsync(t => t.Id == request.TaxSlabId.Value, cancellationToken);
        }

        if (taxSlab == null)
        {
            taxSlab = await _context.TaxSlabs.FirstOrDefaultAsync(cancellationToken);
        }

        if (taxSlab == null)
        {
            taxSlab = new TaxSlab
            {
                Id = Guid.NewGuid(),
                Name = "GST 18%",
                CgstRate = 9.0m,
                SgstRate = 9.0m,
                IgstRate = 18.0m,
                CessRate = 0.0m
            };
            _context.TaxSlabs.Add(taxSlab);
            await _context.SaveChangesAsync(cancellationToken);
        }

        // 2. Map new product
        var uomId = request.UnitOfMeasureId ?? new Guid("u0000000-0000-0000-0000-000000000001");
        var uom = await _context.UnitOfMeasures.FirstOrDefaultAsync(u => u.Id == uomId, cancellationToken);
        var isWeighable = uom != null && (uom.Symbol == "Kgs" || uom.Symbol == "Gms");

        var product = new Product
        {
            Id = Guid.NewGuid(),
            ProductCode = request.ProductCode,
            Name = request.Name,
            TamilName = request.TamilName,
            Description = request.Description,
            CategoryId = request.CategoryId,
            UnitOfMeasureId = uomId,
            TaxSlabId = taxSlab.Id,
            Mrp = request.Mrp,
            SellingPrice = request.SellingPrice,
            PurchasePrice = request.PurchasePrice,
            IsWeighable = isWeighable,
            IsActive = true
        };

        // 3. Add Barcode (auto-generate if blank)
        string finalBarcode = request.BarcodeValue;
        if (string.IsNullOrEmpty(finalBarcode))
        {
            var ticks = DateTime.UtcNow.Ticks.ToString();
            finalBarcode = "29" + ticks.Substring(ticks.Length - 11);
        }

        product.Barcodes.Add(new Barcode
        {
            Id = Guid.NewGuid(),
            BarcodeValue = finalBarcode,
            IsPrimary = true
        });

        try
        {
            _context.Products.Add(product);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException != null ? ex.InnerException.Message : "";
            throw new Exception($"DB Save Fail: {ex.Message}. Inner: {inner}");
        }

        return product.Id;
    }
}
