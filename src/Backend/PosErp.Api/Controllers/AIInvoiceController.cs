using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using System.Text.Json;
using System.Text;
using System.Net.Http;
using Microsoft.Extensions.Configuration;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize(Roles = "Admin,Manager,Owner")]
public class AIInvoiceController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IStockLedgerService _stockLedgerService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AIInvoiceController(
        IApplicationDbContext context, 
        IStockLedgerService stockLedgerService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _context = context;
        _stockLedgerService = stockLedgerService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public class ExtractedInvoiceItem
    {
        public string Barcode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal CostPrice { get; set; }
        public decimal Mrp { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal TaxRate { get; set; }
        public string Uom { get; set; } = "PCS";
    }

    [HttpPost("ai-extract")]
    public async Task<IActionResult> ExtractInvoice(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        var items = new List<ExtractedInvoiceItem>();
        string extension = Path.GetExtension(file.FileName).ToLower();
        bool isImage = extension == ".jpg" || extension == ".jpeg" || extension == ".png";

        // Save uploaded file to temp file for processing
        string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream, cancellationToken);
        }

        try
        {
            // Try extracting using offline Ollama model first
            try
            {
                items = await ExtractWithOllamaAsync(tempFilePath, isImage, cancellationToken);
            }
            catch (Exception ollamaEx)
            {
                Console.WriteLine($"Ollama extraction failed: {ollamaEx.Message}. Falling back to local PDF parser.");
            }

            // Local fallback for PDFs if Ollama is unreachable/failed
            if (items.Count == 0 && !isImage)
            {
                using (var stream = file.OpenReadStream())
                {
                    items = ExtractItemsFromPdfSpatially(stream);
                }

                if (items.Count == 0)
                {
                    string text = ExtractTextFromPdf(tempFilePath);
                    items = ParseInvoiceText(text);
                }
            }
        }
        finally
        {
            if (System.IO.File.Exists(tempFilePath))
            {
                System.IO.File.Delete(tempFilePath);
            }
        }

        if (items.Count == 0)
        {
            if (isImage)
            {
                return BadRequest(new { message = "Ollama is offline or does not have a vision model pulled. Image extraction requires a running Ollama server with a vision model (e.g. llava)." });
            }
            items = GetMockExtractedItems();
        }

        var resultItems = new List<object>();
        var invoiceRef = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        foreach (var item in items)
        {
            // Lookup product by barcode
            var product = await _context.Products
                .Include(p => p.Barcodes)
                .Include(p => p.TaxSlab)
                .FirstOrDefaultAsync(p => p.Barcodes.Any(b => b.BarcodeValue == item.Barcode), cancellationToken);

            string existingUomSymbol = "";
            if (product != null)
            {
                var uomEntity = await _context.UnitOfMeasures
                    .FirstOrDefaultAsync(u => u.Id == product.UnitOfMeasureId, cancellationToken);
                existingUomSymbol = uomEntity?.Symbol ?? "";
            }

            if (product != null)
            {
                bool costDiffers = product.PurchasePrice != item.CostPrice;
                string status = costDiffers ? "DISCREPANCY" : "MATCH";
                string remarks = costDiffers 
                    ? $"Cost changed from ₹{product.PurchasePrice:0.00} to ₹{item.CostPrice:0.00}"
                    : "Matches existing catalog master.";

                resultItems.Add(new
                {
                    item.Barcode,
                    item.ProductName,
                    ProductCode = product.ProductCode,
                    Quantity = item.Quantity,
                    CostPrice = item.CostPrice, // Extracted cost price
                    ExistingCostPrice = product.PurchasePrice,
                    ExistingSellingPrice = product.SellingPrice,
                    ExistingMrp = product.Mrp,
                    Mrp = product.Mrp, // Pre-fill with system MRP
                    SellingPrice = product.SellingPrice, // Pre-fill with system Selling Price
                    BatchNumber = "",
                    ExpiryDate = (DateTime?)null,
                    Status = status,
                    HasExpiry = product.HasExpiry,
                    Remarks = remarks,
                    TaxRate = item.TaxRate,
                    ExistingTaxRate = product.TaxSlab?.IgstRate ?? 0.0m,
                    Uom = item.Uom,
                    ExistingUom = existingUomSymbol
                });
            }
            else
            {
                resultItems.Add(new
                {
                    item.Barcode,
                    item.ProductName,
                    ProductCode = "",
                    Quantity = item.Quantity,
                    CostPrice = item.CostPrice,
                    ExistingCostPrice = (decimal?)null,
                    ExistingSellingPrice = (decimal?)null,
                    ExistingMrp = (decimal?)null,
                    Mrp = item.Mrp > 0 ? item.Mrp : item.CostPrice * 1.2m, // Suggested fallback MRP
                    SellingPrice = item.SellingPrice > 0 ? item.SellingPrice : item.CostPrice * 1.15m, // Suggested Selling Price
                    BatchNumber = "",
                    ExpiryDate = (DateTime?)null,
                    Status = "NEW",
                    HasExpiry = IsPerishable(item.ProductName),
                    Remarks = "New Product - setup name & pricing details",
                    TaxRate = item.TaxRate,
                    ExistingTaxRate = (decimal?)null,
                    Uom = item.Uom,
                    ExistingUom = (string?)null
                });
            }
        }

        return Ok(new
        {
            InvoiceReference = invoiceRef,
            Items = resultItems
        });
    }

    private async Task<string> ResolveOllamaUrlAsync()
    {
        var configUrl = _configuration["Ollama:BaseUrl"] ?? _configuration["Ollama__BaseUrl"];
        if (!string.IsNullOrEmpty(configUrl))
        {
            return configUrl.TrimEnd('/');
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(1);

        try
        {
            var response = await client.GetAsync("http://pos_ollama:11434");
            if (response.IsSuccessStatusCode) return "http://pos_ollama:11434";
        }
        catch { }

        try
        {
            var response = await client.GetAsync("http://localhost:11434");
            if (response.IsSuccessStatusCode) return "http://localhost:11434";
        }
        catch { }

        return "http://localhost:11434"; // Default fallback
    }

    private async Task<string> GetOllamaModelAsync(string ollamaUrl, bool requiresVision)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            var response = await client.GetAsync($"{ollamaUrl}/api/tags");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("models", out var modelsArr) && modelsArr.ValueKind == JsonValueKind.Array)
                {
                    var models = new List<string>();
                    foreach (var m in modelsArr.EnumerateArray())
                    {
                        if (m.TryGetProperty("name", out var nameProp))
                        {
                            var name = nameProp.GetString();
                            if (!string.IsNullOrEmpty(name)) models.Add(name);
                        }
                    }

                    if (models.Count > 0)
                    {
                        if (requiresVision)
                        {
                            // Look for vision models (llava, vl, vision, minicpm)
                            var visionModel = models.FirstOrDefault(m => 
                                m.Contains("llava", StringComparison.OrdinalIgnoreCase) || 
                                m.Contains("vl", StringComparison.OrdinalIgnoreCase) || 
                                m.Contains("vision", StringComparison.OrdinalIgnoreCase) || 
                                m.Contains("minicpm", StringComparison.OrdinalIgnoreCase));
                            
                            if (visionModel != null) return visionModel;
                        }

                        // Try standard text models (qwen, llama)
                        var textModel = models.FirstOrDefault(m => 
                            m.Contains("qwen", StringComparison.OrdinalIgnoreCase) || 
                            m.Contains("llama", StringComparison.OrdinalIgnoreCase));
                        
                        if (textModel != null) return textModel;

                        return models[0];
                    }
                }
            }
        }
        catch { }

        return requiresVision ? "llava" : "llama2";
    }

    private async Task<List<ExtractedInvoiceItem>> ExtractWithOllamaAsync(
        string filePath, bool isImage, CancellationToken cancellationToken)
    {
        var items = new List<ExtractedInvoiceItem>();
        var ollamaUrl = await ResolveOllamaUrlAsync();
        var model = await GetOllamaModelAsync(ollamaUrl, isImage);

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(90); // 90 second timeout for offline LLM parsing

        object requestBody;
        string prompt = @"You are an expert invoice parser. Extract the line items from this invoice.
For each item, extract:
- barcode (use the HSN code if barcode is missing, e.g. 'HSN-12345678-001' where the last part is the item index, or generate a unique placeholder like 'ITEM-001')
- productName (full product description including any continuation lines)
- quantity (numerical value, e.g. 10)
- uom (unit of measure as a short string, e.g. 'PCS', 'SET', 'DOZ', 'KG'. Default to 'PCS' if not found)
- costPrice (unit cost price excluding tax)
- mrp (suggested or printed MRP)
- sellingPrice (suggested or printed selling price)
- taxRate (the GST rate percentage as a number, e.g., 5.0, 18.0, 12.0, 0.0)
- batchNumber (if printed)
- expiryDate (if printed, format as YYYY-MM-DD or null)

Return ONLY a JSON array of objects with the exact keys: 'barcode', 'productName', 'quantity', 'uom', 'costPrice', 'mrp', 'sellingPrice', 'taxRate', 'batchNumber', 'expiryDate'. Do not include markdown formatting or extra text.";

        if (isImage)
        {
            var bytes = await System.IO.File.ReadAllBytesAsync(filePath, cancellationToken);
            var base64Image = Convert.ToBase64String(bytes);

            requestBody = new
            {
                model = model,
                prompt = prompt,
                stream = false,
                format = "json",
                images = new[] { base64Image }
            };
        }
        else
        {
            // PDF: extract text
            string textContent = ExtractTextFromPdf(filePath);

            requestBody = new
            {
                model = model,
                prompt = $"{prompt}\n\nInvoice text:\n{textContent}",
                stream = false,
                format = "json"
            };
        }

        var jsonPayload = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await client.PostAsync($"{ollamaUrl}/api/generate", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Ollama request failed with status: {response.StatusCode}");
        }

        var resContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(resContent);
        if (doc.RootElement.TryGetProperty("response", out var resProp))
        {
            var responseText = resProp.GetString();
            if (!string.IsNullOrEmpty(responseText))
            {
                responseText = responseText.Trim();
                if (responseText.StartsWith("```json"))
                {
                    responseText = responseText.Substring(7);
                }
                if (responseText.EndsWith("```"))
                {
                    responseText = responseText.Substring(0, responseText.Length - 3);
                }
                responseText = responseText.Trim();

                var parsed = JsonSerializer.Deserialize<List<ExtractedInvoiceItem>>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed != null)
                {
                    items = parsed;
                }
            }
        }

        return items;
    }

    private string ExtractTextFromPdf(string filePath)
    {
        var linesList = new List<string>();
        using (var document = PdfDocument.Open(filePath))
        {
            foreach (var page in document.GetPages())
            {
                var words = page.GetWords().ToList();
                if (!words.Any()) continue;

                var sortedWords = words.OrderByDescending(w => w.BoundingBox.Bottom).ToList();
                var currentLineWords = new List<UglyToad.PdfPig.Content.Word>();
                double currentY = sortedWords[0].BoundingBox.Bottom;
                const double threshold = 5.0;

                foreach (var word in sortedWords)
                {
                    if (Math.Abs(word.BoundingBox.Bottom - currentY) > threshold)
                    {
                        linesList.Add(string.Join(" ", currentLineWords
                            .OrderBy(w => w.BoundingBox.Left)
                            .Select(w => w.Text)));
                        currentLineWords.Clear();
                        currentY = word.BoundingBox.Bottom;
                    }
                    currentLineWords.Add(word);
                }

                if (currentLineWords.Any())
                    linesList.Add(string.Join(" ", currentLineWords
                        .OrderBy(w => w.BoundingBox.Left)
                        .Select(w => w.Text)));
            }
        }
        return string.Join("\n", linesList);
    }

    public class AiImportRequestItem
    {
        public string Barcode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal CostPrice { get; set; }
        public decimal Mrp { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal Quantity { get; set; }
        public string? BatchNumber { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool HasExpiry { get; set; }
        public decimal TaxRate { get; set; }
        public string Uom { get; set; } = "PCS";
    }

    public class AiImportRequest
    {
        public string InvoiceReference { get; set; } = string.Empty;
        public List<AiImportRequestItem> Items { get; set; } = new();
    }

    [HttpPost("ai-import")]
    public async Task<IActionResult> ImportInvoice([FromBody] AiImportRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.Items == null || request.Items.Count == 0)
        {
            return BadRequest("No items to import.");
        }

        // H6 FIX: Retrieve caller User ID from claims principal
        var callerIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        Guid? callerId = null;
        if (Guid.TryParse(callerIdStr, out var parsedId))
        {
            callerId = parsedId;
        }

        var rules = InventoryRulesManager.GetRules();

        // 1. Initial Validation Checks
        foreach (var item in request.Items)
        {
            var product = await _context.Products
                .Include(p => p.Barcodes)
                .FirstOrDefaultAsync(p => p.Barcodes.Any(b => b.BarcodeValue == item.Barcode), cancellationToken);

            bool itemHasExpiry = product?.HasExpiry ?? item.HasExpiry;

            if (itemHasExpiry && rules.MandatoryBatchTracking)
            {
                if (string.IsNullOrWhiteSpace(item.BatchNumber) || !item.ExpiryDate.HasValue)
                {
                    return BadRequest(new { message = $"BATCH_VALIDATION_FAILED: Product '{item.ProductName}' is perishable and requires a valid Batch Number and Expiry Date." });
                }
            }
        }

        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var taxSlabs = await _context.TaxSlabs.Where(t => !t.IsDeleted).ToListAsync(cancellationToken);
            var defaultTaxSlab = taxSlabs.FirstOrDefault(s => s.IgstRate == 18.0m) ?? taxSlabs.FirstOrDefault();
            if (defaultTaxSlab == null)
            {
                throw new Exception("No Tax Slabs found in the system to assign to new products.");
            }

            // Pre-load all UOMs (we may need to add new ones dynamically)
            var allUoms = await _context.UnitOfMeasures.Where(u => !u.IsDeleted).ToListAsync(cancellationToken);
            var defaultUom = allUoms.FirstOrDefault();
            Guid uomId = defaultUom?.Id ?? Guid.NewGuid();

            var generalCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "General", cancellationToken);
            if (generalCategory == null)
            {
                generalCategory = new Category
                {
                    Id = Guid.NewGuid(),
                    Name = "General",
                    IsDeleted = false
                };
                _context.Categories.Add(generalCategory);
                await _context.SaveChangesAsync(cancellationToken);
            }

            int newProductOffset = 0;
            var baseProductCount = await _context.Products.CountAsync(cancellationToken);

            // Keep track of the batch IDs mapped by barcode for use in Pass 2
            var itemBatchIds = new Dictionary<string, Guid?>();

            // --- PASS 1: Create/Update Products and Batches ---
            foreach (var item in request.Items)
            {
                var product = await _context.Products
                    .Include(p => p.Barcodes)
                    .FirstOrDefaultAsync(p => p.Barcodes.Any(b => b.BarcodeValue == item.Barcode), cancellationToken);

                // Look up tax slab by rate
                var selectedTaxSlab = taxSlabs.FirstOrDefault(s => Math.Abs(s.IgstRate - item.TaxRate) < 0.1m) ?? defaultTaxSlab;

                // Resolve UOM: find by symbol (case-insensitive), or insert a new one
                string uomSymbol = string.IsNullOrWhiteSpace(item.Uom) ? "PCS" : item.Uom.Trim().ToUpper();
                var resolvedUom = allUoms.FirstOrDefault(u => u.Symbol.Equals(uomSymbol, StringComparison.OrdinalIgnoreCase));
                if (resolvedUom == null)
                {
                    resolvedUom = new UnitOfMeasure
                    {
                        Id = Guid.NewGuid(),
                        Name = uomSymbol,
                        Symbol = uomSymbol,
                        IsDeleted = false,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.UnitOfMeasures.Add(resolvedUom);
                    allUoms.Add(resolvedUom); // prevent duplicate inserts in same batch
                }
                Guid resolvedUomId = resolvedUom.Id;

                if (product == null)
                {
                    newProductOffset++;
                    var nextProdNumber = baseProductCount + newProductOffset;

                    string? hsnCode = null;
                    if (item.Barcode.StartsWith("HSN-"))
                    {
                        var parts = item.Barcode.Split('-');
                        if (parts.Length > 1) hsnCode = parts[1];
                    }

                    product = new Product
                    {
                        Id = Guid.NewGuid(),
                        ProductCode = $"PROD-EXT-{nextProdNumber:D3}",
                        Name = item.ProductName,
                        PurchasePrice = item.CostPrice,
                        Mrp = item.Mrp,
                        SellingPrice = item.SellingPrice,
                        TaxSlabId = selectedTaxSlab.Id,
                        CategoryId = generalCategory.Id,
                        UnitOfMeasureId = resolvedUomId,
                        HsnCode = hsnCode,
                        IsActive = true,
                        IsWeighable = false,
                        HasExpiry = item.HasExpiry
                    };

                    product.Barcodes.Add(new Barcode
                    {
                        Id = Guid.NewGuid(),
                        BarcodeValue = item.Barcode,
                        IsPrimary = true
                    });

                    _context.Products.Add(product);
                }
                else
                {
                    // Update catalog details and prices in the product master
                    product.Name = item.ProductName;
                    product.PurchasePrice = item.CostPrice;
                    product.Mrp = item.Mrp;
                    product.SellingPrice = item.SellingPrice;
                    product.TaxSlabId = selectedTaxSlab.Id; // Update tax slab if changed
                    product.UnitOfMeasureId = resolvedUomId;  // Update UOM if changed

                    if (string.IsNullOrWhiteSpace(product.HsnCode) && item.Barcode.StartsWith("HSN-"))
                    {
                        var parts = item.Barcode.Split('-');
                        if (parts.Length > 1) product.HsnCode = parts[1];
                    }
                }

                // Batch Association Handling
                Guid? selectedBatchId = null;

                bool isBatchTracked = product.HasExpiry || !string.IsNullOrWhiteSpace(item.BatchNumber);
                if (isBatchTracked)
                {
                    var batchNum = string.IsNullOrWhiteSpace(item.BatchNumber) ? "MKT-DEFAULT" : item.BatchNumber.Trim();
                    
                    var batch = await _context.ProductBatches
                        .FirstOrDefaultAsync(b => b.ProductId == product.Id && b.BatchNumber == batchNum, cancellationToken);

                    if (batch != null)
                    {
                        batch.CostPrice = item.CostPrice;
                        batch.Mrp = item.Mrp;
                        if (item.ExpiryDate.HasValue) batch.ExpiryDate = item.ExpiryDate;
                        selectedBatchId = batch.Id;
                    }
                    else
                    {
                        var newBatch = new ProductBatch
                        {
                            Id = Guid.NewGuid(),
                            ProductId = product.Id,
                            BatchNumber = batchNum,
                            ExpiryDate = item.ExpiryDate,
                            CostPrice = item.CostPrice,
                            Mrp = item.Mrp,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.ProductBatches.Add(newBatch);
                        selectedBatchId = newBatch.Id;
                    }
                }

                itemBatchIds[item.Barcode] = selectedBatchId;
            }

            // Save Products, Barcodes, and ProductBatches first so they exist in the DB.
            await _context.SaveChangesAsync(cancellationToken);

            // --- PASS 2: Create StockAdjustment, StockAdjustmentItems, and Record Stock Movements ---
            var adjustment = new StockAdjustment
            {
                Id = Guid.NewGuid(),
                AdjustmentNumber = $"ADJ-MKT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                Reason = "MARKET_PURCHASE",
                Status = "APPROVED",
                CreatedAt = DateTime.UtcNow
            };

            _context.StockAdjustments.Add(adjustment);

            foreach (var item in request.Items)
            {
                var product = await _context.Products
                    .Include(p => p.Barcodes)
                    .FirstOrDefaultAsync(p => p.Barcodes.Any(b => b.BarcodeValue == item.Barcode), cancellationToken);

                if (product == null)
                {
                    throw new Exception($"Product with barcode {item.Barcode} was not found after saving.");
                }

                var selectedBatchId = itemBatchIds[item.Barcode];
                DateTime? expiryDate = item.ExpiryDate;

                var adjItem = new StockAdjustmentItem
                {
                    Id = Guid.NewGuid(),
                    StockAdjustmentId = adjustment.Id,
                    ProductId = product.Id,
                    BatchId = selectedBatchId,
                    AdjustedQuantity = item.Quantity,
                    UnitCost = item.CostPrice
                };
                adjustment.Items.Add(adjItem);

                await _stockLedgerService.RecordMovementAsync(
                    storeId: Guid.Empty,
                    warehouseId: null,
                    terminalId: null,
                    businessDate: DateTime.UtcNow,
                    productId: product.Id,
                    batchId: selectedBatchId,
                    movementType: "ADJ",
                    quantity: item.Quantity,
                    unitCost: item.CostPrice,
                    expiryDate: expiryDate,
                    referenceDocId: adjustment.Id,
                    referenceNumber: adjustment.AdjustmentNumber,
                    userId: callerId,
                    cancellationToken: cancellationToken
                );
            }

            // Save StockAdjustments, StockAdjustmentItems, and StockLedgerEntries
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new { success = true, adjustmentId = adjustment.Id, adjustmentNumber = adjustment.AdjustmentNumber });
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            var detailsList = new List<string>();
            foreach (var entry in ex.Entries)
            {
                var modifiedProps = entry.Properties
                    .Where(p => p.IsModified)
                    .Select(p => $"{p.Metadata.Name} (Original: {p.OriginalValue}, Current: {p.CurrentValue})");
                detailsList.Add($"{entry.Entity.GetType().Name} (State: {entry.State}) [Modified fields: {string.Join(", ", modifiedProps)}]");
            }
            var details = string.Join("; ", detailsList);
            return BadRequest(new { message = $"CONCURRENCY_ERROR: {ex.Message} Details: [{details}]" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return BadRequest(new { message = ex.Message });
        }
    }


    // ─── Spatial coordinate-based extraction (PRIMARY) ─────────────────────────
    // Handles column-format invoices (e.g. Tally) where each column is a separate
    // text element at a fixed X position but aligns with other columns on the same Y row.
    // Uses PdfPig's per-word bounding boxes for accurate spatial grouping.
    private List<ExtractedInvoiceItem> ExtractItemsFromPdfSpatially(Stream pdfStream)
    {
        var items = new List<ExtractedInvoiceItem>();

        using var document = PdfDocument.Open(pdfStream);

        const double ROW_TOLERANCE = 5.0;  // Points tolerance for grouping words into rows

        foreach (var page in document.GetPages())
        {
            var allWords = page.GetWords().ToList();
            if (!allWords.Any()) continue;

            // ── Step 1: Group words into rows by Y coordinate ─────────────────
            // PdfPig gives per-word bounding boxes — use Bottom Y for grouping
            var rowDict = new SortedDictionary<double, List<UglyToad.PdfPig.Content.Word>>(
                Comparer<double>.Create((a, b) => b.CompareTo(a))); // descending (top-first)

            foreach (var word in allWords)
            {
                double wordY = word.BoundingBox.Bottom;
                double matchedKey = double.NaN;

                foreach (var key in rowDict.Keys)
                {
                    if (Math.Abs(key - wordY) <= ROW_TOLERANCE)
                    {
                        matchedKey = key;
                        break;
                    }
                }

                if (double.IsNaN(matchedKey))
                    rowDict[wordY] = new List<UglyToad.PdfPig.Content.Word> { word };
                else
                    rowDict[matchedKey].Add(word);
            }

            var rows = rowDict.ToList(); // ordered top-to-bottom (descending Y)

            // ── Step 2: Locate the table header row ───────────────────────────
            // Header row contains "Quantity" / "Qty" and "Rate" / "Price" as column labels
            int headerRowIdx = -1;
            for (int i = 0; i < rows.Count; i++)
            {
                var rowText = string.Join(" ", rows[i].Value.Select(w => w.Text)).ToLower();
                if ((rowText.Contains("quantity") || rowText.Contains("qty")) && 
                    (rowText.Contains("rate") || rowText.Contains("price") || rowText.Contains("amount") || rowText.Contains("amt")))
                {
                    headerRowIdx = i;
                    break;
                }
            }

            if (headerRowIdx < 0) continue; // No table header found on this page

            // ── Step 2b: Detect Column Boundaries Dynamically ──────────────────
            // Tuned default fallbacks for standard Tally A4 invoice
            double pageDescMin = 30;   // Widened left edge to capture Sl.No + Description together
            double pageDescMax = 275;
            double pageHsnMin  = 276;
            double pageHsnMax  = 325;
            double pageGstMin  = 326;  // GST Rate column (contains "18 %" or "5 %")
            double pageGstMax  = 385;
            double pageQtyMin  = 386;
            double pageQtyMax  = 445;
            double pageRateMin = 446;
            double pageRateMax = 510;
            double pageAmtMin  = 511;

            try
            {
                var headerWords = rows[headerRowIdx].Value.OrderBy(w => w.BoundingBox.Left).ToList();

                var descWords = new List<UglyToad.PdfPig.Content.Word>();
                var hsnWords = new List<UglyToad.PdfPig.Content.Word>();
                var gstWords = new List<UglyToad.PdfPig.Content.Word>();
                var qtyWords = new List<UglyToad.PdfPig.Content.Word>();
                var rateWords = new List<UglyToad.PdfPig.Content.Word>();
                var amtWords = new List<UglyToad.PdfPig.Content.Word>();

                foreach (var word in headerWords)
                {
                    var text = word.Text.ToLower();
                    if (text.Contains("desc") || text.Contains("particular") || text.Contains("product") || text.Contains("item") || text.Contains("good"))
                        descWords.Add(word);
                    else if (text.Contains("hsn") || text.Contains("sac"))
                        hsnWords.Add(word);
                    else if (text.Contains("gst") || (text.Contains("rate") && word.BoundingBox.Left < 400))
                        gstWords.Add(word);
                    else if (text.Contains("qty") || text.Contains("quant"))
                        qtyWords.Add(word);
                    else if (text.Contains("rate") || text.Contains("price") || text.Contains("cost"))
                        rateWords.Add(word);
                    else if (text.Contains("amt") || text.Contains("amount") || text.Contains("total") || text.Contains("value"))
                        amtWords.Add(word);
                }

                // Establish the sequence of found columns
                var colsFound = new List<(string Name, double Left, double Right)>();
                if (descWords.Any()) 
                    colsFound.Add(("DESC", descWords.Min(w => w.BoundingBox.Left), descWords.Max(w => w.BoundingBox.Right)));
                if (hsnWords.Any()) 
                    colsFound.Add(("HSN", hsnWords.Min(w => w.BoundingBox.Left), hsnWords.Max(w => w.BoundingBox.Right)));
                if (gstWords.Any())
                    colsFound.Add(("GST", gstWords.Min(w => w.BoundingBox.Left), gstWords.Max(w => w.BoundingBox.Right)));
                if (qtyWords.Any()) 
                    colsFound.Add(("QTY", qtyWords.Min(w => w.BoundingBox.Left), qtyWords.Max(w => w.BoundingBox.Right)));
                if (rateWords.Any()) 
                    colsFound.Add(("RATE", rateWords.Min(w => w.BoundingBox.Left), rateWords.Max(w => w.BoundingBox.Right)));
                if (amtWords.Any()) 
                    colsFound.Add(("AMT", amtWords.Min(w => w.BoundingBox.Left), amtWords.Max(w => w.BoundingBox.Right)));

                colsFound = colsFound.OrderBy(c => c.Left).ToList();

                // Build bounds if enough columns exist to order
                if (colsFound.Count >= 3)
                {
                    // 1. Description column bounds — always start from page left edge to capture index
                    var descCol = colsFound.FirstOrDefault(c => c.Name == "DESC");
                    if (descCol.Name != null)
                    {
                        pageDescMin = 20; // Always start from leftmost edge to capture serial+desc
                        var idx = colsFound.IndexOf(descCol);
                        if (idx < colsFound.Count - 1)
                            pageDescMax = colsFound[idx + 1].Left - 5;
                        else
                            pageDescMax = descCol.Right + 150;
                    }

                    // 2. HSN column bounds
                    var hsnCol = colsFound.FirstOrDefault(c => c.Name == "HSN");
                    if (hsnCol.Name != null)
                    {
                        pageHsnMin = hsnCol.Left - 5;
                        var idx = colsFound.IndexOf(hsnCol);
                        if (idx < colsFound.Count - 1)
                            pageHsnMax = colsFound[idx + 1].Left - 5;
                        else
                            pageHsnMax = hsnCol.Right + 15;
                    }
                    else
                    {
                        pageHsnMin = 999;
                        pageHsnMax = 999;
                    }

                    // 3. GST Rate column bounds
                    var gstCol = colsFound.FirstOrDefault(c => c.Name == "GST");
                    if (gstCol.Name != null)
                    {
                        pageGstMin = gstCol.Left - 5;
                        var idx = colsFound.IndexOf(gstCol);
                        if (idx < colsFound.Count - 1)
                            pageGstMax = colsFound[idx + 1].Left - 2;
                        else
                            pageGstMax = gstCol.Right + 20;
                    }

                    // 4. Qty column bounds
                    var qtyCol = colsFound.FirstOrDefault(c => c.Name == "QTY");
                    if (qtyCol.Name != null)
                    {
                        pageQtyMin = qtyCol.Left - 10;
                        var idx = colsFound.IndexOf(qtyCol);
                        if (idx < colsFound.Count - 1)
                            pageQtyMax = colsFound[idx + 1].Left - 2;
                        else
                            pageQtyMax = qtyCol.Right + 15;
                    }

                    // 5. Rate column bounds
                    var rateCol = colsFound.FirstOrDefault(c => c.Name == "RATE");
                    if (rateCol.Name != null)
                    {
                        pageRateMin = rateCol.Left - 10;
                        var idx = colsFound.IndexOf(rateCol);
                        if (idx < colsFound.Count - 1)
                            pageRateMax = colsFound[idx + 1].Left - 2;
                        else
                            pageRateMax = rateCol.Right + 15;
                    }

                    // 6. Amount column bounds
                    var amtCol = colsFound.FirstOrDefault(c => c.Name == "AMT");
                    if (amtCol.Name != null)
                    {
                        pageAmtMin = amtCol.Left - 15;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting column boundaries dynamically: {ex.Message}. Using default boundaries.");
            }

            // ── Step 3: Locate the table footer row ───────────────────────────
            // Footer starts when we see subtotal/tax labels like "OUTPUT CGST", "ROUND OFF"
            int footerRowIdx = rows.Count;
            for (int i = headerRowIdx + 1; i < rows.Count; i++)
            {
                var rowText = string.Join(" ", rows[i].Value.Select(w => w.Text)).ToLower();
                if (rowText.Contains("output") || rowText.Contains("round off") ||
                    rowText.Contains("chargeable") || rowText.Contains("tax invoice"))
                {
                    footerRowIdx = i;
                    break;
                }
            }

            // ── Step 4: Extract product data from each data row ───────────────
            // A "product row" has a rate value in the Rate column.
            // A "continuation row" (wrapped description, no qty/rate) gets appended to last item.
            for (int i = headerRowIdx + 1; i < footerRowIdx; i++)
            {
                var rowWords = rows[i].Value;

                // Classify words by column
                string descText = string.Join(" ", rowWords
                    .Where(w => w.BoundingBox.Left >= pageDescMin && w.BoundingBox.Right <= pageDescMax)
                    .OrderBy(w => w.BoundingBox.Left)
                    .Select(w => w.Text)).Trim();

                string hsnText = string.Join("", rowWords
                    .Where(w => w.BoundingBox.Left >= pageHsnMin && w.BoundingBox.Right <= pageHsnMax)
                    .Select(w => w.Text)).Trim();

                // GST Rate: look for pattern like "18" or "18 %" or "5" in the GST column
                string gstText = string.Join(" ", rowWords
                    .Where(w => w.BoundingBox.Left >= pageGstMin && w.BoundingBox.Right <= pageGstMax)
                    .OrderBy(w => w.BoundingBox.Left)
                    .Select(w => w.Text)).Trim();

                string qtyText = string.Join(" ", rowWords
                    .Where(w => w.BoundingBox.Left >= pageQtyMin && w.BoundingBox.Right <= pageQtyMax)
                    .Select(w => w.Text)).Trim();

                string rateText = string.Join("", rowWords
                    .Where(w => w.BoundingBox.Left >= pageRateMin && w.BoundingBox.Right <= pageRateMax
                                && Regex.IsMatch(w.Text, @"^\d"))
                    .Select(w => w.Text)).Trim();

                string amtText = string.Join("", rowWords
                    .Where(w => w.BoundingBox.Left >= pageAmtMin
                                && Regex.IsMatch(w.Text, @"[\d,]"))
                    .Select(w => w.Text)).Replace(",", "").Trim();

                // ── Continuation rows: description wraps to next line, no qty/rate ──
                // If we have desc text but no rate, append to the last extracted item's name.
                bool hasRate = !string.IsNullOrWhiteSpace(rateText) && Regex.IsMatch(rateText, @"\d");
                bool hasDesc = !string.IsNullOrWhiteSpace(descText);

                if (!hasRate && hasDesc && items.Count > 0)
                {
                    // Skip batch sub-lines
                    if (!descText.Contains("Batch", StringComparison.OrdinalIgnoreCase) &&
                        !descText.Contains("Primary", StringComparison.OrdinalIgnoreCase) &&
                        !descText.Contains("continued", StringComparison.OrdinalIgnoreCase))
                    {
                        // Append continuation description to the previous item
                        items[^1].ProductName = CleanProductName(items[^1].ProductName + " " + descText);
                    }
                    continue;
                }

                if (!hasRate) continue;

                // Also skip if description is a batch sub-line
                if (hasDesc &&
                    (descText.Contains("Batch", StringComparison.OrdinalIgnoreCase) ||
                     descText.Contains("Primary", StringComparison.OrdinalIgnoreCase)))
                    continue;

                // ── Parse rate (cost price) ───────────────────────────────────
                if (!decimal.TryParse(rateText, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal rate) || rate <= 0)
                    continue;

                // ── Parse GST tax rate ────────────────────────────────────────
                decimal taxRate = 0;
                var gstMatch = Regex.Match(gstText, @"(\d+(?:\.\d+)?)");
                if (gstMatch.Success)
                    decimal.TryParse(gstMatch.Groups[1].Value, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out taxRate);

                // ── Parse line amount ─────────────────────────────────────────
                decimal.TryParse(amtText, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal lineAmount);

                // ── Parse quantity and UOM ────────────────────────────────────
                // qtyText examples: "4 set", "12 pcs", "1.000 doz"
                decimal qty = 0;
                string uom = "PCS";
                var qtyMatch = Regex.Match(qtyText, @"(\d+(?:\.\d+)?)\s*([a-zA-Z]*)", RegexOptions.IgnoreCase);
                if (qtyMatch.Success)
                {
                    decimal.TryParse(qtyMatch.Groups[1].Value, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out qty);
                    string uomRaw = qtyMatch.Groups[2].Value.Trim().ToUpper();
                    if (!string.IsNullOrWhiteSpace(uomRaw)) uom = uomRaw;
                }

                // Fallback: derive qty = amount / rate (Tally always prints both)
                if (qty <= 0 && lineAmount > 0 && rate > 0)
                {
                    decimal computed = Math.Round(lineAmount / rate, 3);
                    decimal rounded = Math.Round(computed);
                    qty = (Math.Abs(computed - rounded) < 0.02m) ? rounded : computed;
                }

                if (qty <= 0) qty = 1;

                // ── Build product name ────────────────────────────────────────
                // Strip the leading Tally row index from the description.
                // e.g. "1Ruby Container No.2" → "Ruby Container No.2"
                //       "2332-Royal Touch" → "332-Royal Touch" (preserve product code after index)
                string cleanDesc = descText;
                int expectedIdx = items.Count + 1;
                // Try stripping leading digits that match the expected 1-based item index
                var idxPrefixMatch = Regex.Match(cleanDesc, @"^(\d{1,2})(.+)");
                if (idxPrefixMatch.Success)
                {
                    if (int.TryParse(idxPrefixMatch.Groups[1].Value, out int foundIdx) && foundIdx == expectedIdx)
                    {
                        cleanDesc = idxPrefixMatch.Groups[2].Value.TrimStart();
                    }
                }

                string productName = CleanProductName(cleanDesc);
                if (string.IsNullOrWhiteSpace(productName))
                    productName = $"Item {expectedIdx}";

                // ── Build unique barcode key ──────────────────────────────────
                // This invoice has no EAN barcode; use HSN + sequential index as placeholder
                string barcodeKey = !string.IsNullOrWhiteSpace(hsnText)
                    ? $"HSN-{hsnText}-{expectedIdx:D3}"
                    : $"ITEM-{expectedIdx:D3}";

                // ── Suggested retail prices ───────────────────────────────────
                // Rate IS the purchase price (excl. GST). Suggest MRP = rate + 18% margin.
                // Rounded to nearest whole number (e.g. 43.86 → 44, 37.04 → 37) per policy.
                // User can edit in the draft grid before approving.
                decimal suggestedMrp  = Math.Round(rate * 1.18m, 0, MidpointRounding.AwayFromZero);
                decimal suggestedSell = Math.Round(rate * 1.15m, 0, MidpointRounding.AwayFromZero);

                items.Add(new ExtractedInvoiceItem
                {
                    Barcode      = barcodeKey,
                    ProductName  = productName,
                    Quantity     = qty,
                    CostPrice    = rate,
                    Mrp          = suggestedMrp,
                    SellingPrice = suggestedSell,
                    TaxRate      = taxRate,
                    Uom          = uom
                });
            }
        }

        return items;
    }

    private static string CleanProductName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        // Remove stray control / non-printable characters that pdfpig sometimes includes
        var cleaned = Regex.Replace(raw, @"[^\x20-\x7E\u00A0-\uFFFF]", " ");
        // Collapse multiple spaces
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
        return cleaned;
    }

    // ─── Fallback line-based parser (kept for non-columnar invoice formats) ────
    private List<ExtractedInvoiceItem> ParseInvoiceText(string text)
    {
        var items = new List<ExtractedInvoiceItem>();
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Try to detect a structured table line: has at least one numeric amount and a name
        // Pattern: optional Sr.No, product text, optional HSN, qty, rate, amount on same line
        foreach (var line in lines)
        {
            // Skip header/footer/label lines
            if (Regex.IsMatch(line, @"^\s*(Sr|Description|Quantity|Rate|HSN|SAC|Amount|Total|CGST|SGST|Round|Chargeable|Tax|Invoice|Batch|Signatory|Declaration|Rupee)\b", RegexOptions.IgnoreCase))
                continue;

            // Look for lines containing amounts like "1,271.25" or "953.5"
            var amountMatches = Regex.Matches(line, @"\b\d{1,3}(?:,\d{3})*(?:\.\d{1,2})?\b");
            if (amountMatches.Count < 2) continue;

            // Extract all decimal numbers from the line
            var numbers = Regex.Matches(line, @"\b\d+(?:\.\d{1,2})?\b")
                .Cast<Match>()
                .Select(m => m.Value)
                .ToList();

            if (numbers.Count < 2) continue;

            // Extract GST rate (e.g. "18 %" or "5%")
            decimal taxRate = 0;
            var gstMatch = Regex.Match(line, @"(\d+)\s*%");
            if (gstMatch.Success)
                decimal.TryParse(gstMatch.Groups[1].Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out taxRate);

            // Extract UOM (e.g. "pcs", "set", "doz", "kg")
            string uom = "PCS";
            var uomMatch = Regex.Match(line, @"\b(\d+(?:\.\d+)?)\s+(pcs|set|doz|kg|nos|box|ltr|ml|gm|gms|unit)\b", RegexOptions.IgnoreCase);
            if (uomMatch.Success)
                uom = uomMatch.Groups[2].Value.ToUpper();

            // Remove serial number at start if present
            string name = Regex.Replace(line, @"^\s*\d{1,2}\s+", "");
            // Remove numeric sequences and % signs
            foreach (var num in numbers.OrderByDescending(n => n.Length))
                name = name.Replace(num, " ");
            name = Regex.Replace(name, @"\d+\s*%", " "); // remove GST % text
            name = Regex.Replace(name, @"\b(pcs|set|doz|kg|nos|box|ltr|ml|gm|gms|unit)\b", " ", RegexOptions.IgnoreCase); // remove UOM
            name = Regex.Replace(name, @"\s+", " ").Trim(" ,|.-\t".ToCharArray());
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (!decimal.TryParse(numbers[numbers.Count - 2], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal rate) || rate <= 0) continue;
            if (!decimal.TryParse(numbers[0], System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal qty) || qty <= 0) qty = 1;

            items.Add(new ExtractedInvoiceItem
            {
                Barcode      = $"ITEM-{items.Count + 1:D3}",
                ProductName  = CleanProductName(name),
                Quantity     = qty,
                CostPrice    = rate,
                Mrp          = Math.Round(rate * 1.18m, 0, MidpointRounding.AwayFromZero),
                SellingPrice = Math.Round(rate * 1.15m, 0, MidpointRounding.AwayFromZero),
                TaxRate      = taxRate,
                Uom          = uom
            });
        }

        return items;
    }


    private List<ExtractedInvoiceItem> GetMockExtractedItems()
    {
        return new List<ExtractedInvoiceItem>
        {
            new() { Barcode = "8901058002313", ProductName = "Tata Salt 1kg", CostPrice = 22.00m, Mrp = 28.00m, SellingPrice = 28.00m, Quantity = 50 },
            new() { Barcode = "8901063012345", ProductName = "Britannia Bourbon 150g", CostPrice = 26.50m, Mrp = 30.00m, SellingPrice = 30.00m, Quantity = 30 },
            new() { Barcode = "8901030753448", ProductName = "Surf Excel Easy Wash 1kg", CostPrice = 115.00m, Mrp = 140.00m, SellingPrice = 140.00m, Quantity = 25 },
            new() { Barcode = "8901030753888", ProductName = "Maggi 2-Minute Noodles 70g", CostPrice = 11.50m, Mrp = 14.00m, SellingPrice = 14.00m, Quantity = 100 },
            new() { Barcode = "8901725185550", ProductName = "Fortune Mustard Oil 1L", CostPrice = 145.00m, Mrp = 175.00m, SellingPrice = 175.00m, Quantity = 40 }
        };
    }

    private static bool IsPerishable(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName)) return false;

        var name = productName.ToLower();

        // Items that are typically non-perishable in a supermarket context
        var nonPerishableKeywords = new[]
        {
            "salt", "surf excel", "detergent", "soap", "shampoo", "cleaner", "lizol", "harpic", "colin", 
            "battery", "eveready", "duracell", "clay", "pencil", "pen", "box", "notebook", "stationery",
            "toy", "plastic", "container", "bottle", "plate", "spoon", "fork", "knife", "bulb", "led",
            "wire", "plug", "clipper", "scissors", "comb", "brush", "hanger", "bucket", "mug"
        };

        foreach (var keyword in nonPerishableKeywords)
        {
            if (name.Contains(keyword)) return false;
        }

        // Perishable items that require batch tracking/expiry dates (food, beverage, medicine, cosmetics)
        var perishableKeywords = new[]
        {
            "bread", "milk", "butter", "cheese", "paneer", "curd", "dahi", "yoghurt", "yogurt", "ghee",
            "egg", "meat", "chicken", "fish", "mutton", "fruit", "vegetable", "juice", "beverage",
            "noodle", "pasta", "maggi", "biscuit", "cookie", "rusk", "chocolate", "sweet", "candy",
            "oil", "mustard", "refined", "dal", "pulse", "atta", "flour", "rice", "wheat", "sugar",
            "sauce", "ketchup", "jam", "honey", "pickle", "tea", "coffee", "bourbon", "silk", "cadbury",
            "medicine", "tablet", "syrup", "ointment", "cream", "lotion"
        };

        foreach (var keyword in perishableKeywords)
        {
            if (name.Contains(keyword)) return true;
        }

        return false;
    }
}
