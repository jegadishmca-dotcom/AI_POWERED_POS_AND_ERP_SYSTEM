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
    string BarcodeValue
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
        // 1. Get default Tax Slab or create one if empty
        var taxSlab = await _context.TaxSlabs.FirstOrDefaultAsync(cancellationToken);
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
        var product = new Product
        {
            Id = Guid.NewGuid(),
            ProductCode = request.ProductCode,
            Name = request.Name,
            TamilName = request.TamilName,
            Description = request.Description,
            TaxSlabId = taxSlab.Id,
            UnitOfMeasureId = Guid.Parse("00000000-0000-0000-0000-000000000003"), // Default PCS Guid
            Mrp = request.Mrp,
            SellingPrice = request.SellingPrice,
            PurchasePrice = request.PurchasePrice,
            IsWeighable = false,
            IsActive = true
        };

        // 3. Add Barcode if supplied
        if (!string.IsNullOrEmpty(request.BarcodeValue))
        {
            product.Barcodes.Add(new Barcode
            {
                Id = Guid.NewGuid(),
                BarcodeValue = request.BarcodeValue,
                IsPrimary = true
            });
        }

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
