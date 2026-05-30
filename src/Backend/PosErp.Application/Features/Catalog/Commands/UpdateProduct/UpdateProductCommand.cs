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

        // Verify product code uniqueness if changed
        if (product.ProductCode != request.ProductCode)
        {
            var codeExists = await _context.Products
                .AnyAsync(p => p.ProductCode == request.ProductCode && p.Id != product.Id && !p.IsDeleted, cancellationToken);
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
        var primaryBarcode = product.Barcodes.FirstOrDefault(b => b.IsPrimary && !b.IsDeleted);
        if (!string.IsNullOrEmpty(request.BarcodeValue))
        {
            if (primaryBarcode != null)
            {
                if (primaryBarcode.BarcodeValue != request.BarcodeValue)
                {
                    // Check if new barcode is unique among active barcodes
                    var barcodeExists = await _context.Barcodes
                        .AnyAsync(b => b.BarcodeValue == request.BarcodeValue && b.ProductId != product.Id && !b.IsDeleted, cancellationToken);
                    if (barcodeExists)
                    {
                        throw new Exception("Barcode already exists on another product.");
                    }
                    primaryBarcode.BarcodeValue = request.BarcodeValue;
                }
            }
            else
            {
                var barcodeExists = await _context.Barcodes
                    .AnyAsync(b => b.BarcodeValue == request.BarcodeValue && !b.IsDeleted, cancellationToken);
                if (barcodeExists)
                {
                    throw new Exception("Barcode already exists on another product.");
                }
                product.Barcodes.Add(new Barcode
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    BarcodeValue = request.BarcodeValue,
                    IsPrimary = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        else
        {
            // If barcode was cleared, mark primary barcode as deleted
            if (primaryBarcode != null)
            {
                primaryBarcode.IsDeleted = true;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
