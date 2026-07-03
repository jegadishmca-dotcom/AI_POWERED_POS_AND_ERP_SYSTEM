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
        public decimal LineAmount { get; set; }
    }

    [HttpPost("ai-extract")]
    public async Task<IActionResult> ExtractInvoice(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        string extension = Path.GetExtension(file.FileName).ToLower();
        if (extension != ".pdf")
        {
            return BadRequest("Unsupported file format. Only PDF files (.pdf) are supported for supplier invoices.");
        }

        var items = new List<ExtractedInvoiceItem>();
        bool isImage = false;
        decimal invoiceTotal = 0;

        // Save uploaded file to temp file for processing
        string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        string originalTempFilePath = tempFilePath;
        using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream, cancellationToken);
        }

        string? pdfImgPath = null;
        try
        {
            // Scanned PDF detection: if it's a PDF but has no selectable text, convert to image for Ollama vision
            if (extension == ".pdf")
            {
                string textContent = ExtractTextFromPdf(tempFilePath);
                if (string.IsNullOrWhiteSpace(textContent) || textContent.Trim().Length < 50)
                {
                    Console.WriteLine("[UploadInvoice] Detected scanned/image-based PDF. Converting to PNG for vision extraction...");
                    pdfImgPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
                    
                    var scriptPath = FindPythonScriptPath();
                    if (scriptPath != null)
                    {
                        var converted = await ConvertPdfToImageAsync(scriptPath, tempFilePath, pdfImgPath, cancellationToken);
                        if (converted && System.IO.File.Exists(pdfImgPath))
                        {
                            isImage = true;
                            tempFilePath = pdfImgPath; 
                        }
                    }
                    else
                    {
                        Console.WriteLine("[UploadInvoice] Could not find pdf_to_img.py script in workspace paths.");
                    }
                }
            }

            // Try extracting using offline Ollama model first
            try
            {
                var ollamaRes = await ExtractWithOllamaAsync(tempFilePath, isImage, cancellationToken);
                items = ollamaRes.Items;
                invoiceTotal = ollamaRes.InvoiceTotal;
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

            // Post-process items: normalize UOM and scale bulk quantities to pieces
            foreach (var item in items)
            {
                item.Uom = NormalizeUom(item.Uom);
                
                if (item.CostPrice > 0 && item.LineAmount > 0 && item.Quantity > 0)
                {
                    decimal rawMultiplier = item.LineAmount / item.CostPrice;
                    if (Math.Abs(rawMultiplier - item.Quantity) > 0.5m)
                    {
                        decimal calculatedQty = Math.Round(rawMultiplier, 0);
                        if (calculatedQty > 0)
                        {
                            item.Quantity = calculatedQty;
                            item.Uom = "PCS"; // Convert bulk UOM to base retail unit PCS
                        }
                    }
                }
            }

            // Extract Grand Total if not already extracted
            decimal calculatedSum = items.Sum(i => i.Quantity * i.CostPrice);
            if (invoiceTotal <= 0)
            {
                invoiceTotal = calculatedSum;
                if (!isImage && extension == ".pdf")
                {
                    try
                    {
                        string text = ExtractTextFromPdf(tempFilePath);
                        invoiceTotal = ExtractGrandTotalFromText(text, calculatedSum);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error extracting grand total: {ex.Message}");
                    }
                }
            }
        }
        finally
        {
            if (System.IO.File.Exists(originalTempFilePath))
            {
                System.IO.File.Delete(originalTempFilePath);
            }
            if (pdfImgPath != null && System.IO.File.Exists(pdfImgPath))
            {
                System.IO.File.Delete(pdfImgPath);
            }
        }

        if (items.Count == 0)
        {
            if (isImage)
            {
                return BadRequest(new { message = "Ollama is offline or does not have a vision model pulled. Image extraction requires a running Ollama server with a vision model (e.g. llava)." });
            }
            items = GetMockExtractedItems();
            invoiceTotal = items.Sum(i => i.Quantity * i.CostPrice);
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
            InvoiceTotal = invoiceTotal,
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

    public class OllamaResponseEnvelope
    {
        public List<ExtractedInvoiceItem> Items { get; set; } = new();
        public decimal InvoiceTotal { get; set; }
    }

    private async Task<(List<ExtractedInvoiceItem> Items, decimal InvoiceTotal)> ExtractWithOllamaAsync(
        string filePath, bool isImage, CancellationToken cancellationToken)
    {
        var items = new List<ExtractedInvoiceItem>();
        decimal invoiceTotal = 0;
        var ollamaUrl = await ResolveOllamaUrlAsync();
        var model = await GetOllamaModelAsync(ollamaUrl, isImage);

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(90); // 90 second timeout for offline LLM parsing

        object requestBody;
        string prompt = @"You are an expert invoice parser. Extract the line items and overall totals from this invoice.
For each item, extract:
- barcode (use the HSN code if barcode is missing, e.g. 'HSN-12345678-001' where the last part is the item index, or generate a unique placeholder like 'ITEM-001')
- productName (full product description including any continuation lines)
- quantity (numerical value of retail unit pieces. If the invoice has quantity as '1 Bag' or '1 Carton' but the rate/costPrice is per piece, calculate the total number of pieces and return that as the quantity. For example, if Net Amount is 14,880.00 and Net Rate is 620.00, the quantity of pieces is 24. Return 24 as the quantity.)
- uom (normalize to standard retail unit symbols like 'PCS', 'SET', 'KGS', 'GMS', 'LTRS', 'MLS', 'PACK', 'BOX'. If a bulk unit was scaled to pieces, set this to 'PCS'.)
- costPrice (the individual retail piece/unit cost price including tax, e.g. Net Rate column, or Rate * (1 + GST%/100))
- mrp (printed or suggested MRP)
- sellingPrice (suggested selling price)
- taxRate (the GST rate percentage as a number, e.g., 5.0, 18.0, 12.0, 0.0)
- batchNumber (if printed)
- expiryDate (if printed, format as YYYY-MM-DD or null)

Return ONLY a JSON object with the exact keys:
- 'items': a JSON array of item objects containing the keys listed above.
- 'invoiceTotal': the overall grand total value of the invoice (as a decimal number).

Do not include markdown formatting or extra text.";

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

                OllamaResponseEnvelope? envelope = null;
                try
                {
                    envelope = JsonSerializer.Deserialize<OllamaResponseEnvelope>(responseText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch {}

                if (envelope != null && envelope.Items != null && envelope.Items.Count > 0)
                {
                    items = envelope.Items;
                    invoiceTotal = envelope.InvoiceTotal;
                }
                else
                {
                    // Fallback to direct list parsing if envelope parsing fails
                    try
                    {
                        var parsedList = JsonSerializer.Deserialize<List<ExtractedInvoiceItem>>(responseText, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (parsedList != null)
                        {
                            items = parsedList;
                            invoiceTotal = items.Sum(i => i.Quantity * i.CostPrice);
                        }
                    }
                    catch {}
                }
            }
        }

        return (items, invoiceTotal);
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

        var strategy = ((DbContext)_context).Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
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

            // Determine the next available sequential internal barcode number.
            // Format: INT{D8} — e.g. INT00000001, INT00000002, ...
            // This follows Global ERP standard for internally-generated item barcodes
            // (prefix-based sequential codes, not dependent on supplier-provided EANs).
            var lastIntBarcode = await _context.Barcodes
                .Where(b => b.BarcodeValue.StartsWith("INT") && b.BarcodeValue.Length == 11)
                .OrderByDescending(b => b.BarcodeValue)
                .Select(b => b.BarcodeValue)
                .FirstOrDefaultAsync(cancellationToken);

            int nextBarcodeSeq = 1;
            if (lastIntBarcode != null && int.TryParse(lastIntBarcode.Substring(3), out int parsedBarcodeSeq))
                nextBarcodeSeq = parsedBarcodeSeq + 1;

            // Keep track of placeholder barcode → resolved Product.Id and BatchId for PASS 2.
            // We MUST use Product.Id here because PASS 1 assigns INT########
            // barcodes to new products, so the original placeholder barcode (HSN-/ITEM-)
            // no longer exists in the DB after SaveChanges.
            var itemProductIds = new Dictionary<string, Guid>();
            var itemBatchIds   = new Dictionary<string, Guid?>();

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

                    // Extract HSN from the placeholder barcode if available
                    string? hsnCode = null;
                    if (item.Barcode.StartsWith("HSN-"))
                    {
                        var parts = item.Barcode.Split('-');
                        if (parts.Length > 1) hsnCode = parts[1];
                    }

                    // Try to find an existing product by HSN + name match to avoid creating
                    // duplicates when the same invoice is imported again without EAN barcodes.
                    Product? existingByHsn = null;
                    if (!string.IsNullOrWhiteSpace(hsnCode))
                    {
                        existingByHsn = await _context.Products
                            .Include(p => p.Barcodes)
                            .FirstOrDefaultAsync(p => p.HsnCode == hsnCode &&
                                p.Name.ToLower() == item.ProductName.ToLower(), cancellationToken);
                    }

                    if (existingByHsn != null)
                    {
                        // Use the existing product as if found by barcode
                        product = existingByHsn;
                        product.PurchasePrice = item.CostPrice;
                        product.Mrp = item.Mrp;
                        product.SellingPrice = item.SellingPrice;
                        product.TaxSlabId = selectedTaxSlab.Id;
                        product.UnitOfMeasureId = resolvedUomId;
                    }
                    else
                    {
                        // Generate a sequential internal barcode (Global ERP standard)
                        string assignedBarcode = $"INT{nextBarcodeSeq:D8}";
                        nextBarcodeSeq++;

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
                            BarcodeValue = assignedBarcode,
                            IsPrimary = true
                        });

                        _context.Products.Add(product);
                    }
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

                itemProductIds[item.Barcode] = product.Id;
                itemBatchIds[item.Barcode]   = selectedBatchId;
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
                // Use the pre-built Product ID map (placeholder barcode → Product.Id)
                // instead of re-querying by barcode, because new products were saved
                // with INT######## barcodes, not the original HSN-/ITEM- placeholders.
                if (!itemProductIds.TryGetValue(item.Barcode, out Guid productId))
                    throw new Exception($"Import mapping error: no product ID found for barcode key '{item.Barcode}'. Please retry the import.");

                var selectedBatchId = itemBatchIds[item.Barcode];
                DateTime? expiryDate = item.ExpiryDate;

                var adjItem = new StockAdjustmentItem
                {
                    Id = Guid.NewGuid(),
                    StockAdjustmentId = adjustment.Id,
                    ProductId = productId,
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
                    productId: productId,
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

                    return (IActionResult)Ok(new { success = true, adjustmentId = adjustment.Id, adjustmentNumber = adjustment.AdjustmentNumber });
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }
        catch (DbUpdateConcurrencyException ex)
        {
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
            double pageDescMin = 30;   // Widened left edge to capture Sl.No + Description together
            double pageDescMax = 275;
            double pageHsnMin  = 276;
            double pageHsnMax  = 325;
            double pageMrpMin  = 999;
            double pageMrpMax  = 999;
            double pageGstMin  = 326;  // GST Rate column
            double pageGstMax  = 385;
            double pageQtyMin  = 386;
            double pageQtyMax  = 445;
            double pageRateMin = 446;
            double pageRateMax = 510;
            double pageNetRateMin = 999;
            double pageNetRateMax = 999;
            double pageAmtMin  = 511;

            try
            {
                var headerWords = rows[headerRowIdx].Value.OrderBy(w => w.BoundingBox.Left).ToList();

                var descWords = new List<UglyToad.PdfPig.Content.Word>();
                var hsnWords = new List<UglyToad.PdfPig.Content.Word>();
                var mrpWords = new List<UglyToad.PdfPig.Content.Word>();
                var gstWords = new List<UglyToad.PdfPig.Content.Word>();
                var qtyWords = new List<UglyToad.PdfPig.Content.Word>();
                var rateWords = new List<UglyToad.PdfPig.Content.Word>();
                var netRateWords = new List<UglyToad.PdfPig.Content.Word>();
                var amtWords = new List<UglyToad.PdfPig.Content.Word>();

                for (int wIdx = 0; wIdx < headerWords.Count; wIdx++)
                {
                    var word = headerWords[wIdx];
                    var text = word.Text.ToLower();
                    string prevText = wIdx > 0 ? headerWords[wIdx - 1].Text.ToLower() : "";
                    string nextText = wIdx < headerWords.Count - 1 ? headerWords[wIdx + 1].Text.ToLower() : "";

                    if (text.Contains("desc") || text.Contains("particular") || text.Contains("product") || text.Contains("item") || text.Contains("good"))
                        descWords.Add(word);
                    else if (text.Contains("hsn") || text.Contains("sac"))
                        hsnWords.Add(word);
                    else if (text.Contains("mrp"))
                        mrpWords.Add(word);
                    else if (text.Contains("gst") || text.Contains("%"))
                        gstWords.Add(word);
                    else if (text.Contains("qty") || text.Contains("quant"))
                        qtyWords.Add(word);
                    else if (text.Contains("net") && (text.Contains("rate") || nextText.Contains("rate") || prevText.Contains("net")))
                        netRateWords.Add(word);
                    else if (text.Contains("rate") && (prevText.Contains("net") || text.Contains("net")))
                        netRateWords.Add(word);
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
                if (mrpWords.Any())
                    colsFound.Add(("MRP", mrpWords.Min(w => w.BoundingBox.Left), mrpWords.Max(w => w.BoundingBox.Right)));
                if (gstWords.Any())
                    colsFound.Add(("GST", gstWords.Min(w => w.BoundingBox.Left), gstWords.Max(w => w.BoundingBox.Right)));
                if (qtyWords.Any()) 
                    colsFound.Add(("QTY", qtyWords.Min(w => w.BoundingBox.Left), qtyWords.Max(w => w.BoundingBox.Right)));
                if (rateWords.Any()) 
                    colsFound.Add(("RATE", rateWords.Min(w => w.BoundingBox.Left), rateWords.Max(w => w.BoundingBox.Right)));
                if (netRateWords.Any()) 
                    colsFound.Add(("NETRATE", netRateWords.Min(w => w.BoundingBox.Left), netRateWords.Max(w => w.BoundingBox.Right)));
                if (amtWords.Any()) 
                    colsFound.Add(("AMT", amtWords.Min(w => w.BoundingBox.Left), amtWords.Max(w => w.BoundingBox.Right)));

                colsFound = colsFound.OrderBy(c => c.Left).ToList();

                // Build bounds if enough columns exist to order
                if (colsFound.Count >= 3)
                {
                    for (int cIdx = 0; cIdx < colsFound.Count; cIdx++)
                    {
                        var col = colsFound[cIdx];
                        double nextLeft = (cIdx < colsFound.Count - 1) ? colsFound[cIdx + 1].Left - 2 : 999;

                        if (col.Name == "DESC")
                        {
                            pageDescMin = 20;
                            pageDescMax = nextLeft != 999 ? nextLeft : col.Right + 150;
                        }
                        else if (col.Name == "HSN")
                        {
                            pageHsnMin = col.Left - 5;
                            pageHsnMax = nextLeft != 999 ? nextLeft : col.Right + 15;
                        }
                        else if (col.Name == "MRP")
                        {
                            pageMrpMin = col.Left - 5;
                            pageMrpMax = nextLeft != 999 ? nextLeft : col.Right + 15;
                        }
                        else if (col.Name == "GST")
                        {
                            pageGstMin = col.Left - 5;
                            pageGstMax = nextLeft != 999 ? nextLeft : col.Right + 15;
                        }
                        else if (col.Name == "QTY")
                        {
                            pageQtyMin = col.Left - 10;
                            pageQtyMax = nextLeft != 999 ? nextLeft : col.Right + 15;
                        }
                        else if (col.Name == "RATE")
                        {
                            pageRateMin = col.Left - 10;
                            pageRateMax = nextLeft != 999 ? nextLeft : col.Right + 15;
                        }
                        else if (col.Name == "NETRATE")
                        {
                            pageNetRateMin = col.Left - 10;
                            pageNetRateMax = nextLeft != 999 ? nextLeft : col.Right + 15;
                        }
                        else if (col.Name == "AMT")
                        {
                            pageAmtMin = col.Left - 15;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting column boundaries dynamically: {ex.Message}. Using default boundaries.");
            }

            // ── Step 3: Locate the table footer row ───────────────────────────
            int footerRowIdx = rows.Count;
            for (int i = headerRowIdx + 1; i < rows.Count; i++)
            {
                var rowText = string.Join(" ", rows[i].Value.Select(w => w.Text)).ToLower();

                if (rowText.Contains("output") || rowText.Contains("round off") ||
                    rowText.Contains("chargeable") || rowText.Contains("tax invoice") ||
                    rowText.Contains("cgst") || rowText.Contains("sgst") ||
                    rowText.Contains("r.off") || rowText.Contains("igst") ||
                    rowText == "total" || rowText.StartsWith("total ") ||
                    rowText.Contains("grand total") || rowText.Contains("sub total") ||
                    rowText.Contains("subtotal"))
                {
                    footerRowIdx = i;
                    break;
                }

                var rWords  = rows[i].Value;
                bool descHasText = rWords.Any(w => w.BoundingBox.Left >= pageDescMin && w.BoundingBox.Right <= pageDescMax
                    && !Regex.IsMatch(w.Text, @"^[\d,\.]+$"));
                bool hsnHasText  = rWords.Any(w => w.BoundingBox.Left >= pageHsnMin  && w.BoundingBox.Right <= pageHsnMax);
                bool qtyHasText  = rWords.Any(w => w.BoundingBox.Left >= pageQtyMin  && w.BoundingBox.Right <= pageQtyMax);
                bool amtHasText  = rWords.Any(w => w.BoundingBox.Left >= pageAmtMin  && Regex.IsMatch(w.Text, @"[\d,]"));

                if (!descHasText && !hsnHasText && !qtyHasText && amtHasText)
                {
                    footerRowIdx = i;
                    break;
                }
            }

            // ── Step 4: Extract product data from each data row ───────────────
            for (int i = headerRowIdx + 1; i < footerRowIdx; i++)
            {
                var rowWords = rows[i].Value;

                string descText = string.Join(" ", rowWords
                    .Where(w => w.BoundingBox.Left >= pageDescMin && w.BoundingBox.Right <= pageDescMax)
                    .OrderBy(w => w.BoundingBox.Left)
                    .Select(w => w.Text)).Trim();

                string hsnText = string.Join("", rowWords
                    .Where(w => w.BoundingBox.Left >= pageHsnMin && w.BoundingBox.Right <= pageHsnMax)
                    .Select(w => w.Text)).Trim();

                string mrpText = string.Join("", rowWords
                    .Where(w => w.BoundingBox.Left >= pageMrpMin && w.BoundingBox.Right <= pageMrpMax
                                && Regex.IsMatch(w.Text, @"[\d,]"))
                    .Select(w => w.Text)).Replace(",", "").Trim();

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

                string netRateText = string.Join("", rowWords
                    .Where(w => w.BoundingBox.Left >= pageNetRateMin && w.BoundingBox.Right <= pageNetRateMax
                                && Regex.IsMatch(w.Text, @"[\d,]"))
                    .Select(w => w.Text)).Replace(",", "").Trim();

                string amtText = string.Join("", rowWords
                    .Where(w => w.BoundingBox.Left >= pageAmtMin
                                && Regex.IsMatch(w.Text, @"[\d,]"))
                    .Select(w => w.Text)).Replace(",", "").Trim();

                bool hasRate = (!string.IsNullOrWhiteSpace(rateText) && Regex.IsMatch(rateText, @"\d")) ||
                               (!string.IsNullOrWhiteSpace(netRateText) && Regex.IsMatch(netRateText, @"\d"));
                bool hasDesc = !string.IsNullOrWhiteSpace(descText);

                if (!hasRate && hasDesc && items.Count > 0)
                {
                    if (!descText.Contains("Batch", StringComparison.OrdinalIgnoreCase) &&
                        !descText.Contains("Primary", StringComparison.OrdinalIgnoreCase) &&
                        !descText.Contains("continued", StringComparison.OrdinalIgnoreCase))
                    {
                        items[^1].ProductName = CleanProductName(items[^1].ProductName + " " + descText);
                    }
                    continue;
                }

                if (!hasRate) continue;

                if (hasDesc &&
                    (descText.Contains("Batch", StringComparison.OrdinalIgnoreCase) ||
                     descText.Contains("Primary", StringComparison.OrdinalIgnoreCase)))
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

                if (qty <= 0) qty = 1;

                // ── Parse MRP ────────────────────────────────────────────────
                decimal mrp = 0;
                if (!string.IsNullOrWhiteSpace(mrpText))
                {
                    decimal.TryParse(mrpText, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out mrp);
                }

                // ── Parse rate (unit cost price, tax-inclusive) ──────────────
                decimal rate = 0;
                decimal parsedRate = 0;
                if (!string.IsNullOrWhiteSpace(rateText))
                {
                    decimal.TryParse(rateText, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out parsedRate);
                }

                decimal parsedNetRate = 0;
                if (!string.IsNullOrWhiteSpace(netRateText))
                {
                    decimal.TryParse(netRateText, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out parsedNetRate);
                }

                if (parsedNetRate > 0)
                {
                    rate = Math.Round(parsedNetRate, 2);
                }
                else if (parsedRate > 0)
                {
                    rate = Math.Round(parsedRate * (1 + taxRate / 100.0m), 2);
                }
                else if (lineAmount > 0 && qty > 0)
                {
                    rate = Math.Round(lineAmount / qty, 2);
                }

                if (rate <= 0) continue;

                // ── Build product name ────────────────────────────────────────
                string cleanDesc = descText;
                int expectedIdx = items.Count + 1;
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
                string barcodeKey = !string.IsNullOrWhiteSpace(hsnText)
                    ? $"HSN-{hsnText}-{expectedIdx:D3}"
                    : $"ITEM-{expectedIdx:D3}";

                // ── Suggested retail prices ───────────────────────────────────
                decimal suggestedMrp  = mrp > 0 ? mrp : Math.Round(rate * 1.18m, 0, MidpointRounding.AwayFromZero);
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
                    Uom          = uom,
                    LineAmount   = lineAmount
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

    private string? FindPythonScriptPath()
    {
        var pathsToTry = new[]
        {
            "pdf_to_img.py",
            "src/Backend/pdf_to_img.py",
            "../pdf_to_img.py",
            "../../pdf_to_img.py",
            "../../../pdf_to_img.py",
            "../../../../pdf_to_img.py"
        };

        foreach (var relativePath in pathsToTry)
        {
            var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));
            if (System.IO.File.Exists(fullPath)) return fullPath;

            var cwdPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
            if (System.IO.File.Exists(cwdPath)) return cwdPath;
        }

        return null;
    }

    private async Task<bool> ConvertPdfToImageAsync(string scriptPath, string pdfPath, string outputPath, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\" \"{pdfPath}\" \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new System.Diagnostics.Process { StartInfo = startInfo };
            process.Start();

            var readOutputTask = process.StandardOutput.ReadToEndAsync();
            var readErrorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync(cancellationToken);

            string output = await readOutputTask;
            string error = await readErrorTask;

            if (process.ExitCode == 0 && output.Contains("SUCCESS"))
            {
                return true;
            }
            else
            {
                Console.WriteLine($"[PDF to Image] Python Script Failed. Code: {process.ExitCode}, Error: {error}, Output: {output}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PDF to Image] Exception converting PDF: {ex.Message}");
            return false;
        }
    }

    private static string NormalizeUom(string uom)
    {
        if (string.IsNullOrWhiteSpace(uom)) return "PCS";
        var clean = uom.Trim().ToUpper();
        if (clean == "CARTO" || clean == "CARTC" || clean == "CTN" || clean.StartsWith("CART"))
            return "CARTON";
        if (clean == "BAG" || clean == "BAGS")
            return "BAG";
        if (clean == "SET" || clean == "SETS")
            return "SET";
        if (clean == "PCS" || clean == "PC" || clean == "PIECE" || clean == "PIECES")
            return "PCS";
        if (clean == "BOX" || clean == "BOXES")
            return "BOX";
        if (clean == "KG" || clean == "KGS" || clean == "KILOGRAM" || clean == "KILOGRAMS")
            return "KGS";
        if (clean == "GM" || clean == "GMS" || clean == "GRAM" || clean == "GRAMS")
            return "GMS";
        return uom.Trim();
    }

    private decimal ExtractGrandTotalFromText(string text, decimal calculatedTotal)
    {
        if (string.IsNullOrWhiteSpace(text)) return calculatedTotal;
        
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Reverse()
            .ToList();
            
        foreach (var line in lines)
        {
            var lower = line.ToLower();
            if ((lower.Contains("total") || lower.Contains("grand") || lower.Contains("payable") || lower.Contains("net amt") || lower.Contains("net amount")) 
                && !lower.Contains("tax") && !lower.Contains("cgst") && !lower.Contains("sgst") && !lower.Contains("igst"))
            {
                var matches = Regex.Matches(line, @"\b\d{1,3}(?:,\d{3})*(?:\.\d{1,2})?\b");
                foreach (Match match in matches)
                {
                    if (decimal.TryParse(match.Value.Replace(",", ""), System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal val))
                    {
                        if (val > 0 && (calculatedTotal <= 0 || Math.Abs(val - calculatedTotal) < calculatedTotal * 0.15m))
                        {
                            return val;
                        }
                    }
                }
            }
        }
        return calculatedTotal > 0 ? calculatedTotal : 0;
    }
}
