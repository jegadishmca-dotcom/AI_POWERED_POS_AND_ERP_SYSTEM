using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Catalog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Catalog.Commands.UpdateProduct;

public record UpdateProductCommand(
    Guid Id,
    string ProductCode,
    string Name,
    string? TamilName,
    string? Description,
    decimal Mrp,
    decimal SellingPrice,
    decimal PurchasePrice,
    string BarcodeValue
) : IRequest<bool>;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UpdateProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Barcodes)
            .FirstOrDefaultAsync(p => p.Id == request.Id && !p.IsDeleted, cancellationToken);

        if (product == null)
        {
            throw new Exception("Product not found.");
        }

        // Verify product code uniqueness if changed (case-insensitive)
        if (!string.Equals(product.ProductCode, request.ProductCode, StringComparison.OrdinalIgnoreCase))
        {
            var codeExists = await _context.Products
                .AnyAsync(p => p.ProductCode.ToLower() == request.ProductCode.ToLower() && p.Id != product.Id && !p.IsDeleted, cancellationToken);
            if (codeExists)
            {
                throw new Exception("Product code already exists on another product.");
            }
            product.ProductCode = request.ProductCode;
        }

        product.Name = request.Name;
        product.TamilName = request.TamilName;
        product.Description = request.Description;
        product.Mrp = request.Mrp;
        product.SellingPrice = request.SellingPrice;
        product.PurchasePrice = request.PurchasePrice;
        product.UpdatedAt = DateTime.UtcNow;

        // Handle Barcode Update
        if (!string.IsNullOrEmpty(request.BarcodeValue))
        {
            // Check if new barcode is unique among other products' active barcodes
            var barcodeExistsOnOtherProduct = await _context.Barcodes
                .AnyAsync(b => b.BarcodeValue == request.BarcodeValue && b.ProductId != product.Id && !b.IsDeleted, cancellationToken);
            if (barcodeExistsOnOtherProduct)
            {
                throw new Exception("Barcode already exists on another product.");
            }

            // Find if this product already has this barcode associated with it (active or deleted)
            var existingBarcode = product.Barcodes.FirstOrDefault(b => b.BarcodeValue == request.BarcodeValue);
            if (existingBarcode != null)
            {
                existingBarcode.IsDeleted = false;
                existingBarcode.IsPrimary = true;
            }
            else
            {
                product.Barcodes.Add(new Barcode
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    BarcodeValue = request.BarcodeValue,
                    IsPrimary = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Set all other barcodes of this product to not primary
            foreach (var b in product.Barcodes.Where(b => b.BarcodeValue != request.BarcodeValue))
            {
                b.IsPrimary = false;
            }
        }
        else
        {
            // If barcode was cleared, mark all barcodes as not primary and deleted
            foreach (var b in product.Barcodes)
            {
                b.IsPrimary = false;
                b.IsDeleted = true;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
