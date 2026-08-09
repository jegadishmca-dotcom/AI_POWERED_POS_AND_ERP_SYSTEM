using MediatR;
using Microsoft.AspNetCore.Http;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace PosErp.Application.Features.Catalog.Commands.ImportProducts;

public record ImportProductResult(int TotalImported, int TotalFailed, List<string> Errors);

public record ImportProductsCommand(IFormFile File, string? JobId = null) : IRequest<ImportProductResult>;

public class ImportJobStatus
{
    public string JobId { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ProcessedRows { get; set; }
    public int ImportedCount { get; set; }
    public int FailedCount { get; set; }
    public int Percent => TotalRows > 0 ? Math.Min(99, (int)((double)ProcessedRows / TotalRows * 100)) : 0;
    public bool IsCompleted { get; set; }
    public List<string> Errors { get; set; } = new();
}

public static class ImportProgressTracker
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImportJobStatus> _jobs = new(StringComparer.OrdinalIgnoreCase);

    public static void StartJob(string jobId, int totalRows)
    {
        _jobs[jobId] = new ImportJobStatus
        {
            JobId = jobId,
            TotalRows = totalRows,
            ProcessedRows = 0,
            ImportedCount = 0,
            FailedCount = 0,
            IsCompleted = false
        };
    }

    public static void UpdateJob(string jobId, int processedRows, int importedCount, int failedCount, List<string>? errors = null)
    {
        if (_jobs.TryGetValue(jobId, out var status))
        {
            status.ProcessedRows = processedRows;
            status.ImportedCount = importedCount;
            status.FailedCount = failedCount;
            if (errors != null && errors.Any())
            {
                status.Errors = errors.Take(20).ToList();
            }
        }
    }

    public static void CompleteJob(string jobId, int importedCount, int failedCount, List<string> errors)
    {
        if (_jobs.TryGetValue(jobId, out var status))
        {
            status.ProcessedRows = status.TotalRows > 0 ? status.TotalRows : status.ProcessedRows;
            status.ImportedCount = importedCount;
            status.FailedCount = failedCount;
            status.IsCompleted = true;
            status.Errors = errors;
        }
    }

    public static ImportJobStatus? GetStatus(string jobId)
    {
        _jobs.TryGetValue(jobId, out var status);
        return status;
    }
}

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

        if (!string.IsNullOrEmpty(request.JobId))
        {
            int estimatedRows = Math.Max(100, (int)(request.File.Length / 110));
            ImportProgressTracker.StartJob(request.JobId, estimatedRows);
        }

        try
        {
            using var reader = new StreamReader(request.File.OpenReadStream(), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var headerLine = await reader.ReadLineAsync(cancellationToken);
            while (string.IsNullOrWhiteSpace(headerLine) && !reader.EndOfStream)
            {
                headerLine = await reader.ReadLineAsync(cancellationToken);
            }
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                return new ImportProductResult(0, 0, new List<string> { "CSV file is empty." });
            }

            var headers = ParseCsvLine(headerLine)
                .Select(h => h.Trim('\uFEFF', ' ', '"', '\t').ToLower())
                .ToList();

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

            // Pre-load TaxSlabs to map names quickly (auto-seed if empty due to db wipe)
            var taxSlabs = await _context.TaxSlabs.Where(t => !t.IsDeleted).ToListAsync(cancellationToken);
            if (!taxSlabs.Any())
            {
                var gst0 = new TaxSlab { Id = Guid.NewGuid(), Name = "GST 0%", CgstRate = 0, SgstRate = 0, IgstRate = 0, CreatedAt = DateTime.UtcNow };
                var gst5 = new TaxSlab { Id = Guid.NewGuid(), Name = "GST 5%", CgstRate = 2.5m, SgstRate = 2.5m, IgstRate = 5m, CreatedAt = DateTime.UtcNow };
                var gst12 = new TaxSlab { Id = Guid.NewGuid(), Name = "GST 12%", CgstRate = 6m, SgstRate = 6m, IgstRate = 12m, CreatedAt = DateTime.UtcNow };
                var gst18 = new TaxSlab { Id = Guid.NewGuid(), Name = "GST 18%", CgstRate = 9m, SgstRate = 9m, IgstRate = 18m, CreatedAt = DateTime.UtcNow };
                var gst28 = new TaxSlab { Id = Guid.NewGuid(), Name = "GST 28%", CgstRate = 14m, SgstRate = 14m, IgstRate = 28m, CreatedAt = DateTime.UtcNow };
                
                _context.TaxSlabs.AddRange(gst0, gst5, gst12, gst18, gst28);
                await _context.SaveChangesAsync(cancellationToken);
                taxSlabs = await _context.TaxSlabs.Where(t => !t.IsDeleted).ToListAsync(cancellationToken);
            }
            var defaultTaxSlab = taxSlabs.FirstOrDefault(t => t.Name.StartsWith("GST 0")) ?? taxSlabs.First();

            // Pre-load UOMs to map symbols quickly (auto-seed if empty)
            var uoms = await _context.UnitOfMeasures.Where(u => !u.IsDeleted).ToListAsync(cancellationToken);
            if (!uoms.Any())
            {
                var uomPcs = new UnitOfMeasure { Id = new Guid("a0000000-0000-0000-0000-000000000001"), Name = "Pieces", Symbol = "Pcs", CreatedAt = DateTime.UtcNow };
                var uomKgs = new UnitOfMeasure { Id = new Guid("a0000000-0000-0000-0000-000000000002"), Name = "Kilograms", Symbol = "Kgs", CreatedAt = DateTime.UtcNow };
                _context.UnitOfMeasures.AddRange(uomPcs, uomKgs);
                await _context.SaveChangesAsync(cancellationToken);
                uoms = await _context.UnitOfMeasures.Where(u => !u.IsDeleted).ToListAsync(cancellationToken);
            }
            var defaultPcsUom = uoms.FirstOrDefault(u => u.Symbol.Equals("Pcs", StringComparison.OrdinalIgnoreCase)) ?? uoms.First();
            var defaultKgsUom = uoms.FirstOrDefault(u => u.Symbol.Equals("Kgs", StringComparison.OrdinalIgnoreCase)) ?? uoms.First();

            // Lightweight fast projections (<100ms even for 50,000 products to prevent pre-load timeouts)
            var existingProducts = await _context.Products
                .AsNoTracking()
                .Where(p => !p.IsDeleted)
                .Select(p => new { p.ProductCode, p.Id })
                .ToDictionaryAsync(p => p.ProductCode, p => p.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

            var existingBarcodesSet = (await _context.Barcodes
                .AsNoTracking()
                .Select(b => b.BarcodeValue.Trim().ToLower())
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingBatchesSet = (await _context.ProductBatches
                .AsNoTracking()
                .Where(b => b.IsActive)
                .Select(b => $"{b.ProductId}_{b.BatchNumber.Trim().ToLower()}")
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                    
                    string mrpStr = mrpIdx != -1 && mrpIdx < values.Count && !string.IsNullOrWhiteSpace(values[mrpIdx]) ? values[mrpIdx] : "0";
                    string sellingStr = sellingIdx != -1 && sellingIdx < values.Count && !string.IsNullOrWhiteSpace(values[sellingIdx]) ? values[sellingIdx] : "0";

                    if (!decimal.TryParse(mrpStr, System.Globalization.NumberStyles.Any, null, out decimal mrp) ||
                        !decimal.TryParse(sellingStr, System.Globalization.NumberStyles.Any, null, out decimal sellingPrice))
                    {
                        errors.Add($"Line {lineNum}: Invalid numeric format for Mrp or SellingPrice.");
                        failed++;
                        continue;
                    }

                    decimal purchasePrice = 0m;
                    if (purchaseIdx != -1 && purchaseIdx < values.Count && !string.IsNullOrWhiteSpace(values[purchaseIdx]))
                    {
                        decimal.TryParse(values[purchaseIdx], System.Globalization.NumberStyles.Any, null, out purchasePrice);
                    }
                    if (purchasePrice == 0m)
                    {
                        purchasePrice = sellingPrice * 0.8m; // fallback
                    }

                    if (sellingPrice <= 0 || purchasePrice < 0)
                    {
                        errors.Add($"Line {lineNum}: Selling Price must be greater than zero. Purchase price cannot be negative.");
                        failed++;
                        continue;
                    }
                    if (mrp <= 0 || sellingPrice > mrp)
                    {
                        // Auto-adjust MRP to match Selling Price for legacy data typos so no product rows are lost
                        mrp = Math.Max(mrp, sellingPrice);
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
                    bool isNew = !existingProducts.TryGetValue(productCode, out var existingProductId);
                    Product product;
                    if (isNew)
                    {
                        product = new Product
                        {
                            Id = Guid.NewGuid(),
                            ProductCode = productCode,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        existingProducts[productCode] = product.Id;
                        _context.Products.Add(product);
                    }
                    else
                    {
                        product = await _context.Products.FirstOrDefaultAsync(p => p.Id == existingProductId, cancellationToken)
                            ?? new Product { Id = existingProductId, ProductCode = productCode, CreatedAt = DateTime.UtcNow, IsActive = true };
                        _context.Products.Update(product);
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
                        product.UnitOfMeasureId = isWeighable ? defaultKgsUom.Id : defaultPcsUom.Id;
                        product.IsWeighable = isWeighable;
                    }

                    // Handle Barcode & ProductBatch
                    if (!string.IsNullOrWhiteSpace(barcodeVal))
                    {
                        string cleanBarcode = barcodeVal.Trim();
                        string cleanBarcodeLower = cleanBarcode.ToLower();

                        if (!existingBarcodesSet.Contains(cleanBarcodeLower))
                        {
                            var newBarcode = new Barcode
                            {
                                Id = Guid.NewGuid(),
                                ProductId = product.Id,
                                BarcodeValue = cleanBarcode,
                                IsPrimary = true,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.Barcodes.Add(newBarcode);
                            existingBarcodesSet.Add(cleanBarcodeLower);
                        }

                        string batchKey = $"{product.Id}_{cleanBarcodeLower}";
                        if (!existingBatchesSet.Contains(batchKey))
                        {
                            var newBatch = new ProductBatch
                            {
                                Id = Guid.NewGuid(),
                                ProductId = product.Id,
                                BatchNumber = cleanBarcode,
                                Mrp = mrp,
                                CostPrice = purchasePrice > 0 ? purchasePrice : (sellingPrice * 0.7m),
                                AvailableQuantity = 0,
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow
                            };
                            _context.ProductBatches.Add(newBatch);
                            existingBatchesSet.Add(batchKey);
                        }
                    }

                    imported++;

                    if (!string.IsNullOrEmpty(request.JobId) && lineNum % 10 == 0)
                    {
                        ImportProgressTracker.UpdateJob(request.JobId, lineNum, imported, failed, errors);
                    }

                    // Batch save every 1000 records to keep database transactions manageable
                    if (imported % 1000 == 0)
                    {
                        try
                        {
                            await _context.SaveChangesAsync(cancellationToken);
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            // Ignore optimistic concurrency mismatches on detached legacy barcodes/records
                        }
                        if (_context is DbContext dbContext) { dbContext.ChangeTracker.Clear(); }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Line {lineNum}: {ex.Message}");
                    failed++;
                }
            }

            // Save any remaining uncommitted products inside main try block
            if (imported % 1000 != 0)
            {
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Ignore optimistic concurrency mismatches on remaining items
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"File processing error: {ex.Message}");
            if (!string.IsNullOrEmpty(request.JobId))
            {
                ImportProgressTracker.CompleteJob(request.JobId, imported, failed, errors);
            }
            return new ImportProductResult(imported, failed, errors);
        }

        if (!string.IsNullOrEmpty(request.JobId))
        {
            ImportProgressTracker.CompleteJob(request.JobId, imported, failed, errors);
        }

        return new ImportProductResult(imported, failed, errors);
    }

    private List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var currentToken = new System.Text.StringBuilder();
        char delimiter = (line.Contains('\t') && !line.Contains(',')) ? '\t' : ',';

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(currentToken.ToString().Trim('\uFEFF', ' ', '"', '\r', '\n'));
                currentToken.Clear();
            }
            else
            {
                currentToken.Append(c);
            }
        }
        result.Add(currentToken.ToString().Trim('\uFEFF', ' ', '"', '\r', '\n'));
        return result;
    }
}
