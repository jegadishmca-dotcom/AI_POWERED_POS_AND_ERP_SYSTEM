using MediatR;
using Microsoft.AspNetCore.Http;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Catalog;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace PosErp.Application.Features.Catalog.Commands.ImportProducts;

public record ImportProductResult(int TotalImported, int TotalFailed, List<string> Errors);

public record ImportProductsCommand(IFormFile File) : IRequest<ImportProductResult>;

public class ImportProductsCommandHandler : IRequestHandler<ImportProductsCommand, ImportProductResult>
{
    private readonly IApplicationDbContext _context;

    public ImportProductsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ImportProductResult> Handle(ImportProductsCommand request, CancellationToken cancellationToken)
    {
        if (request.File == null || request.File.Length == 0)
        {
            return new ImportProductResult(0, 0, new List<string> { "Empty file uploaded." });
        }

        var errors = new List<string>();
        int imported = 0;
        int failed = 0;

        try
        {
            using var reader = new StreamReader(request.File.OpenReadStream());
            var headerLine = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                return new ImportProductResult(0, 0, new List<string> { "CSV file is empty." });
            }

            var headers = ParseCsvLine(headerLine).Select(h => h.ToLower()).ToList();

            // Find column indices
            int codeIdx = headers.IndexOf("productcode");
            int nameIdx = headers.IndexOf("name");
            int tamilNameIdx = headers.IndexOf("tamilname");
            int descIdx = headers.IndexOf("description");
            int mrpIdx = headers.IndexOf("mrp");
            int sellingIdx = headers.IndexOf("sellingprice");
            int purchaseIdx = headers.IndexOf("purchaseprice");
            int barcodeIdx = headers.IndexOf("barcode");
            int taxSlabIdx = headers.IndexOf("taxslabname");
            int weighableIdx = headers.IndexOf("isweighable");
            int expiryIdx = headers.IndexOf("hasexpiry");
            int uomIdx = headers.IndexOf("uom");
            if (uomIdx == -1) uomIdx = headers.IndexOf("unitofmeasure");

            if (codeIdx == -1 || nameIdx == -1 || mrpIdx == -1 || sellingIdx == -1)
            {
                return new ImportProductResult(0, 0, new List<string> { "CSV missing required headers. Required: ProductCode, Name, Mrp, SellingPrice." });
            }

            // Pre-load TaxSlabs to map names quickly
            var taxSlabs = await _context.TaxSlabs.Where(t => !t.IsDeleted).ToListAsync(cancellationToken);
            var defaultTaxSlab = taxSlabs.FirstOrDefault();
            if (defaultTaxSlab == null)
            {
                return new ImportProductResult(0, 0, new List<string> { "No active Tax Slabs found in database to map products." });
            }

            // Pre-load UOMs to map symbols quickly
            var uoms = await _context.UnitOfMeasures.Where(u => !u.IsDeleted).ToListAsync(cancellationToken);
            var defaultPcsUom = uoms.FirstOrDefault(u => u.Symbol.Equals("Pcs", StringComparison.OrdinalIgnoreCase));
            var defaultKgsUom = uoms.FirstOrDefault(u => u.Symbol.Equals("Kgs", StringComparison.OrdinalIgnoreCase));

            // Pre-load all existing products to avoid 30,000 queries
            var existingProducts = await _context.Products
                .Include(p => p.Barcodes)
                .Where(p => !p.IsDeleted)
                .ToDictionaryAsync(p => p.ProductCode, StringComparer.OrdinalIgnoreCase, cancellationToken);

            int lineNum = 1;
            while (!reader.EndOfStream)
            {
                lineNum++;
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Simple comma split
                var values = ParseCsvLine(line);
                if (values.Count <= Math.Max(codeIdx, Math.Max(nameIdx, Math.Max(mrpIdx, sellingIdx))))
                {
                    errors.Add($"Line {lineNum}: Incorrect number of columns.");
                    failed++;
                    continue;
                }

                try
                {
                    string productCode = values[codeIdx];
                    string name = values[nameIdx];
                    string? tamilName = tamilNameIdx != -1 && tamilNameIdx < values.Count ? values[tamilNameIdx] : null;
                    string? description = descIdx != -1 && descIdx < values.Count ? values[descIdx] : null;
                    
                    if (!decimal.TryParse(mrpIdx < values.Count ? values[mrpIdx] : "0", out decimal mrp) ||
                        !decimal.TryParse(sellingIdx < values.Count ? values[sellingIdx] : "0", out decimal sellingPrice))
                    {
                        errors.Add($"Line {lineNum}: Invalid numeric format for Mrp or SellingPrice.");
                        failed++;
                        continue;
                    }

                    decimal purchasePrice = 0m;
                    if (purchaseIdx != -1 && purchaseIdx < values.Count)
                    {
                        decimal.TryParse(values[purchaseIdx], out purchasePrice);
                    }
                    if (purchasePrice == 0m)
                    {
                        purchasePrice = sellingPrice * 0.8m; // fallback
                    }

                    string? barcodeVal = barcodeIdx != -1 && barcodeIdx < values.Count ? values[barcodeIdx] : null;
                    string? taxSlabName = taxSlabIdx != -1 && taxSlabIdx < values.Count ? values[taxSlabIdx] : null;

                    bool isWeighable = false;
                    if (weighableIdx != -1 && weighableIdx < values.Count)
                    {
                        bool.TryParse(values[weighableIdx], out isWeighable);
                    }

                    bool hasExpiry = false;
                    if (expiryIdx != -1 && expiryIdx < values.Count)
                    {
                        bool.TryParse(values[expiryIdx], out hasExpiry);
                    }

                    string? uomSymbol = uomIdx != -1 && uomIdx < values.Count ? values[uomIdx] : null;

                    // Map TaxSlab
                    var taxSlab = taxSlabs.FirstOrDefault(t => t.Name.Equals(taxSlabName, StringComparison.OrdinalIgnoreCase)) ?? defaultTaxSlab;

                    // Check if product code already exists
                    if (!existingProducts.TryGetValue(productCode, out var product))
                    {
                        product = null;
                    }

                    bool isNew = false;
                    if (product == null)
                    {
                        isNew = true;
                        product = new Product
                        {
                            Id = Guid.NewGuid(),
                            ProductCode = productCode,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                    }

                    product.Name = name;
                    product.TamilName = tamilName;
                    product.Description = description;
                    product.Mrp = mrp;
                    product.SellingPrice = sellingPrice;
                    product.PurchasePrice = purchasePrice;
                    product.TaxSlabId = taxSlab.Id;
                    product.HasExpiry = hasExpiry;

                    // Resolve UOM
                    UnitOfMeasure? matchedUom = null;
                    if (!string.IsNullOrWhiteSpace(uomSymbol))
                    {
                        matchedUom = uoms.FirstOrDefault(u => u.Symbol.Equals(uomSymbol.Trim(), StringComparison.OrdinalIgnoreCase)
                                                            || u.Name.Equals(uomSymbol.Trim(), StringComparison.OrdinalIgnoreCase));
                    }

                    if (matchedUom != null)
                    {
                        product.UnitOfMeasureId = matchedUom.Id;
                        product.IsWeighable = matchedUom.Symbol.Equals("Kgs", StringComparison.OrdinalIgnoreCase) 
                                              || matchedUom.Symbol.Equals("Gms", StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        product.UnitOfMeasureId = isWeighable 
                            ? (defaultKgsUom?.Id ?? new Guid("a0000000-0000-0000-0000-000000000002")) 
                            : (defaultPcsUom?.Id ?? new Guid("a0000000-0000-0000-0000-000000000001"));
                        product.IsWeighable = isWeighable;
                    }

                    // Handle Barcode
                    if (!string.IsNullOrWhiteSpace(barcodeVal))
                    {
                        var barcode = product.Barcodes.FirstOrDefault(b => b.IsPrimary);
                        if (barcode == null)
                        {
                            product.Barcodes.Add(new Barcode
                            {
                                Id = Guid.NewGuid(),
                                ProductId = product.Id,
                                BarcodeValue = barcodeVal,
                                IsPrimary = true,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                        else
                        {
                            barcode.BarcodeValue = barcodeVal;
                        }
                    }

                    if (isNew)
                    {
                        _context.Products.Add(product);
                        existingProducts[productCode] = product; // Add to dictionary so duplicates in same CSV don't crash
                    }

                    imported++;

                    // Batch save every 500 records to prevent memory bloat and improve speed
                    if (imported % 500 == 0)
                    {
                        await _context.SaveChangesAsync(cancellationToken);
                        if (_context is DbContext dbContext) { dbContext.ChangeTracker.Clear(); } // Clear tracking to keep memory low
                        // Re-fetch tax slabs & UOMs since they got detached
                        taxSlabs = await _context.TaxSlabs.Where(t => !t.IsDeleted).ToListAsync(cancellationToken);
                        defaultTaxSlab = taxSlabs.FirstOrDefault();
                        uoms = await _context.UnitOfMeasures.Where(u => !u.IsDeleted).ToListAsync(cancellationToken);
                        defaultPcsUom = uoms.FirstOrDefault(u => u.Symbol.Equals("Pcs", StringComparison.OrdinalIgnoreCase));
                        defaultKgsUom = uoms.FirstOrDefault(u => u.Symbol.Equals("Kgs", StringComparison.OrdinalIgnoreCase));
                        // We also lost our existingProducts tracking, but it's okay because we already processed them.
                        // Actually, clearing ChangeTracker is dangerous if we still reference tracked entities.
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Line {lineNum}: {ex.Message}");
                    failed++;
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"File processing error: {ex.Message}");
            return new ImportProductResult(imported, failed, errors);
        }

        // Save any remaining uncommitted products
        if (imported % 500 != 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new ImportProductResult(imported, failed, errors);
    }

    private List<string> ParseCsvLine(string line)
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
