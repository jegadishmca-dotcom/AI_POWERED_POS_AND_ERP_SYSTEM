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

            var headers = headerLine.Split(',').Select(h => h.Trim().ToLower()).ToList();

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

                    // Map TaxSlab
                    var taxSlab = taxSlabs.FirstOrDefault(t => t.Name.Equals(taxSlabName, StringComparison.OrdinalIgnoreCase)) ?? defaultTaxSlab;

                    // Check if product code already exists
                    var product = await _context.Products
                        .Include(p => p.Barcodes)
                        .FirstOrDefaultAsync(p => p.ProductCode == productCode && !p.IsDeleted, cancellationToken);

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
                    product.IsWeighable = isWeighable;
                    product.HasExpiry = hasExpiry;

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
                    }

                    await _context.SaveChangesAsync(cancellationToken);
                    imported++;
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
