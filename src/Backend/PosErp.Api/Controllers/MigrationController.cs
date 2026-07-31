using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Crm;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Purchasing;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class MigrationController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public MigrationController(IApplicationDbContext context)
    {
        _context = context;
    }

    private async Task EnsureProductColumnsExistAsync()
    {
        if (_context is DbContext dbContext)
        {
            await dbContext.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE products ADD COLUMN IF NOT EXISTS preferred_supplier_id UUID NULL;
                ALTER TABLE products ADD COLUMN IF NOT EXISTS is_weighable BOOLEAN NOT NULL DEFAULT FALSE;
            ");
        }
    }

    [HttpPost("execute-sigma21-migration")]
    public async Task<IActionResult> ExecuteSigma21Migration(
        [FromQuery] string server = "192.168.1.10",
        [FromQuery] string database = "APPLE26-27",
        [FromQuery] string username = "sa",
        [FromQuery] string password = "Q7!mX#92Lp@Tz4Ks")
    {
        await EnsureProductColumnsExistAsync();

        var connStr = $"Server={server};Database={database};User Id={username};Password={password};TrustServerCertificate=True;Connect Timeout=30;";
        
        int customersMigrated = 0;
        int suppliersMigrated = 0;
        int productsMigrated = 0;
        int stockBatchesMigrated = 0;

        using (var sqlConn = new SqlConnection(connStr))
        {
            await sqlConn.OpenAsync();

            // 1. MIGRATE CUSTOMERS FROM BOTH Master_CRM_PointsCustomer (14,000+ records) AND Master_Accounts
            var custCmd = sqlConn.CreateCommand();
            custCmd.CommandTimeout = 300;
            custCmd.CommandText = @"
                SELECT 
                    CustomerCode,
                    Name,
                    TamilName,
                    Phone,
                    Email,
                    Address,
                    MembershipCardNumber,
                    LoyaltyPoints,
                    LedgerBalance
                FROM (
                    SELECT 
                        ID AS CustomerCode,
                        LTRIM(RTRIM(Name)) AS Name,
                        ISNULL(PetName, N'') AS TamilName,
                        ISNULL(Mobile1, ISNULL(Mobile2, ISNULL(Phone1, N''))) AS Phone,
                        ISNULL(Email, N'') AS Email,
                        ISNULL(Address1, N'') + N' ' + ISNULL(Address2, N'') AS Address,
                        ISNULL(CustomerID, ID) AS MembershipCardNumber,
                        CAST(ISNULL(BalancePoint, 0) AS DECIMAL(18,2)) AS LoyaltyPoints,
                        CAST(ISNULL(Balance, 0) AS DECIMAL(18,2)) AS LedgerBalance,
                        ROW_NUMBER() OVER (PARTITION BY LTRIM(RTRIM(Name)) ORDER BY ID DESC) AS rnk
                    FROM Master_CRM_PointsCustomer
                    WHERE Name IS NOT NULL AND LEN(LTRIM(RTRIM(Name))) > 0

                    UNION ALL

                    SELECT 
                        ID AS CustomerCode,
                        LTRIM(RTRIM(Name)) AS Name,
                        ISNULL(PetName, N'') AS TamilName,
                        ISNULL(Mobile1, ISNULL(Phone1, N'')) AS Phone,
                        ISNULL(Email, N'') AS Email,
                        ISNULL(Address1, N'') + N' ' + ISNULL(Address2, N'') AS Address,
                        ID AS MembershipCardNumber,
                        0.00 AS LoyaltyPoints,
                        0.00 AS LedgerBalance,
                        1 AS rnk
                    FROM Master_Accounts
                    WHERE FormName = 'Customer' OR AccountType = 'Sundry Debtors' OR AccountType LIKE '%Debtor%'
                ) combined
                WHERE rnk = 1";

            var existingCustomers = await _context.Customers.ToListAsync();
            var existingPhones = new HashSet<string>(existingCustomers.Select(c => c.Phone).Where(p => !string.IsNullOrWhiteSpace(p) && p != "0000000000"));
            var existingNames = new HashSet<string>(existingCustomers.Select(c => c.Name.ToLower()));

            var customersToInsert = new List<Customer>();

            using (var reader = await custCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var name = reader["Name"].ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var phone = reader["Phone"].ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(phone)) phone = "0000000000";

                    if (!existingNames.Contains(name.ToLower()) && (phone == "0000000000" || !existingPhones.Contains(phone)))
                    {
                        var cust = new Customer
                        {
                            Id = Guid.NewGuid(),
                            Name = name,
                            TamilName = reader["TamilName"].ToString(),
                            Phone = phone,
                            Email = reader["Email"].ToString(),
                            Address = reader["Address"].ToString(),
                            MembershipCardNumber = reader["MembershipCardNumber"].ToString() ?? "",
                            RunningLoyaltyPoints = Convert.ToDecimal(reader["LoyaltyPoints"]),
                            RunningWalletBalance = Convert.ToDecimal(reader["LedgerBalance"]),
                            MembershipStatus = "Active"
                        };

                        customersToInsert.Add(cust);
                        existingNames.Add(name.ToLower());
                        if (phone != "0000000000") existingPhones.Add(phone);
                        customersMigrated++;

                        if (customersToInsert.Count >= 1000)
                        {
                            _context.Customers.AddRange(customersToInsert);
                            await _context.SaveChangesAsync(default);
                            customersToInsert.Clear();
                        }
                    }
                }
            }

            if (customersToInsert.Count > 0)
            {
                _context.Customers.AddRange(customersToInsert);
                await _context.SaveChangesAsync(default);
                customersToInsert.Clear();
            }

            // 2. MIGRATE SUPPLIERS & BUILD SUPPLIER MAP
            var supplierCodeMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            var suppCmd = sqlConn.CreateCommand();
            suppCmd.CommandText = @"
                SELECT 
                    ID AS SupplierCode,
                    Name,
                    ISNULL(Mobile1, ISNULL(Phone1, N'')) AS Phone,
                    ISNULL(GSTNO, N'') AS Gstin,
                    N'NET30' AS PaymentTerms
                FROM Master_Accounts
                WHERE FormName = 'Supplier' OR AccountType = 'Sundry Creditors' OR AccountType LIKE '%Creditor%'";

            var existingSuppliersDict = await _context.Suppliers.ToDictionaryAsync(s => s.Name.ToLower(), s => s.Id);

            using (var reader = await suppCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var code = reader["SupplierCode"].ToString()?.Trim();
                    var name = reader["Name"].ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    Guid suppId;
                    if (!existingSuppliersDict.TryGetValue(name.ToLower(), out suppId))
                    {
                        suppId = Guid.NewGuid();
                        var supp = new Supplier
                        {
                            Id = suppId,
                            Name = name,
                            Phone = reader["Phone"].ToString(),
                            Gstin = reader["Gstin"].ToString(),
                            PaymentTerms = "NET30",
                            IsActive = true
                        };
                        _context.Suppliers.Add(supp);
                        existingSuppliersDict[name.ToLower()] = suppId;
                        suppliersMigrated++;
                    }

                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        supplierCodeMap[code] = suppId;
                    }
                }
            }
            await _context.SaveChangesAsync(default);

            // 3. GET OR CREATE DEFAULT TAX SLABS, UOMS & CATEGORY
            var taxSlabs = await _context.TaxSlabs.ToListAsync();
            var tax0 = taxSlabs.FirstOrDefault(t => (t.CgstRate + t.SgstRate) == 0) ?? taxSlabs.FirstOrDefault();
            var tax5 = taxSlabs.FirstOrDefault(t => (t.CgstRate + t.SgstRate) == 5) ?? tax0;
            var tax12 = taxSlabs.FirstOrDefault(t => (t.CgstRate + t.SgstRate) == 12) ?? tax0;
            var tax18 = taxSlabs.FirstOrDefault(t => (t.CgstRate + t.SgstRate) == 18) ?? tax0;
            var tax28 = taxSlabs.FirstOrDefault(t => (t.CgstRate + t.SgstRate) == 28) ?? tax0;

            var allUoms = await _context.UnitOfMeasures.ToListAsync();
            var uomPcs = allUoms.FirstOrDefault(u => u.Symbol.Equals("Pcs", StringComparison.OrdinalIgnoreCase) || u.Name.Equals("Pieces", StringComparison.OrdinalIgnoreCase));
            if (uomPcs == null)
            {
                uomPcs = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Pieces", Symbol = "Pcs" };
                _context.UnitOfMeasures.Add(uomPcs);
            }

            var uomKgs = allUoms.FirstOrDefault(u => u.Symbol.Equals("Kgs", StringComparison.OrdinalIgnoreCase) || u.Name.Equals("Kilograms", StringComparison.OrdinalIgnoreCase));
            if (uomKgs == null)
            {
                uomKgs = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Kilograms", Symbol = "Kgs" };
                _context.UnitOfMeasures.Add(uomKgs);
            }

            var uomBox = allUoms.FirstOrDefault(u => u.Symbol.Equals("Box", StringComparison.OrdinalIgnoreCase));
            if (uomBox == null)
            {
                uomBox = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Box", Symbol = "Box" };
                _context.UnitOfMeasures.Add(uomBox);
            }

            var defaultCategory = await _context.Categories.FirstOrDefaultAsync();
            if (defaultCategory == null)
            {
                defaultCategory = new Category { Id = Guid.NewGuid(), Name = "General" };
                _context.Categories.Add(defaultCategory);
            }
            await _context.SaveChangesAsync(default);

            // 4. MIGRATE PRODUCTS WITH UOM & PREFERRED SUPPLIER ID (39,000+ items)
            var prodCmd = sqlConn.CreateCommand();
            prodCmd.CommandTimeout = 600;
            prodCmd.CommandText = @"
                WITH RecentPurchase AS (
                    SELECT 
                        ProductName AS ProductCode,
                        Account AS SupplierCode,
                        ROW_NUMBER() OVER (PARTITION BY ProductName ORDER BY Date DESC, VNO DESC) AS rnk
                    FROM Trans_Inventory_SOM
                    WHERE FormName = 'Purchase' 
                      AND Account IS NOT NULL AND Account <> ''
                ),
                BatchSupplier AS (
                    SELECT 
                        b.ProductName AS ProductCode,
                        b.SupplierName AS SupplierCode,
                        ROW_NUMBER() OVER (PARTITION BY b.ProductName ORDER BY b.ID DESC) AS rnk
                    FROM Master_Batch b
                    WHERE b.SupplierName IS NOT NULL AND b.SupplierName <> ''
                )
                SELECT 
                    p.ID AS ProductCode,
                    p.Name,
                    ISNULL(p.TamilName, N'') AS TamilName,
                    ISNULL(p.Category, N'General') AS Category,
                    COALESCE(rp.SupplierCode, bs.SupplierCode, N'') AS MappedSupplierCode,
                    CASE 
                        WHEN ISNULL(b.MRP, 0) > 0 THEN CAST(b.MRP AS DECIMAL(18,2))
                        WHEN ISNULL(p.PMRP, 0) > 0 THEN CAST(p.PMRP AS DECIMAL(18,2))
                        ELSE 1.00 
                    END AS Mrp,
                    CASE 
                        WHEN ISNULL(b.SalesRate1, 0) > 0 THEN CAST(b.SalesRate1 AS DECIMAL(18,2))
                        WHEN ISNULL(p.Rate1, 0) > 0 THEN CAST(p.Rate1 AS DECIMAL(18,2))
                        WHEN ISNULL(b.MRP, 0) > 0 THEN CAST(b.MRP AS DECIMAL(18,2))
                        ELSE 1.00 
                    END AS SellingPrice,
                    CASE 
                        WHEN ISNULL(b.PurchaseRate, 0) > 0 THEN CAST(b.PurchaseRate AS DECIMAL(18,2))
                        WHEN ISNULL(p.PPurchaseRate, 0) > 0 THEN CAST(p.PPurchaseRate AS DECIMAL(18,2))
                        ELSE 0.00 
                    END AS PurchasePrice,
                    CASE 
                        WHEN b.BatchNo IS NOT NULL AND LEN(LTRIM(RTRIM(b.BatchNo))) >= 3 THEN LTRIM(RTRIM(b.BatchNo))
                        WHEN p.ShortName IS NOT NULL AND LEN(LTRIM(RTRIM(p.ShortName))) >= 3 THEN LTRIM(RTRIM(p.ShortName))
                        ELSE N'' 
                    END AS Barcode,
                    ISNULL(g.Percentage, 0) AS GstPercentage,
                    CASE 
                        WHEN p.Weight > 0 
                          OR p.Name LIKE '%VELLAM%' OR p.Name LIKE '%RICE%' OR p.Name LIKE '%PARUPPU%' OR p.Name LIKE '%SUGAR%'
                          OR p.Name LIKE '%DHAL%' OR p.Name LIKE '%DAL%' OR p.Name LIKE '%ATTA%' OR p.Name LIKE '%MAIDA%' OR p.Name LIKE '%RAVA%'
                          OR p.Name LIKE '%KG%' OR p.Name LIKE '%1K%' OR p.Name LIKE '%2K%' OR p.Name LIKE '%5K%' OR p.Name LIKE '%10K%' OR p.Name LIKE '%25K%'
                          OR p.Name LIKE '%500G%' OR p.Name LIKE '%250G%' OR p.Name LIKE '%100G%' OR p.Name LIKE '%50G%' OR p.Name LIKE '%GRAM%' OR p.Name LIKE '%GRM%'
                          OR p.Name LIKE '%KILO%' OR p.Name LIKE '%LOOSE%' OR p.Name LIKE '%OIL%' OR p.Name LIKE '%GHEE%' OR p.Name LIKE '%SALT%'
                          OR p.TamilName LIKE N'%கி%' OR p.TamilName LIKE N'%கிலோ%' OR p.TamilName LIKE N'%வெல்லம்%' OR p.TamilName LIKE N'%அரிசி%' OR p.TamilName LIKE N'%பருப்பு%'
                        THEN 1
                        ELSE 0
                    END AS IsWeighable,
                    CASE 
                        WHEN p.Weight > 0 
                          OR p.Name LIKE '%VELLAM%' OR p.Name LIKE '%RICE%' OR p.Name LIKE '%PARUPPU%' OR p.Name LIKE '%SUGAR%'
                          OR p.Name LIKE '%DHAL%' OR p.Name LIKE '%DAL%' OR p.Name LIKE '%ATTA%' OR p.Name LIKE '%MAIDA%' OR p.Name LIKE '%RAVA%'
                          OR p.Name LIKE '%KG%' OR p.Name LIKE '%1K%' OR p.Name LIKE '%2K%' OR p.Name LIKE '%5K%' OR p.Name LIKE '%10K%' OR p.Name LIKE '%25K%'
                          OR p.Name LIKE '%500G%' OR p.Name LIKE '%250G%' OR p.Name LIKE '%100G%' OR p.Name LIKE '%50G%' OR p.Name LIKE '%GRAM%' OR p.Name LIKE '%GRM%'
                          OR p.Name LIKE '%KILO%' OR p.Name LIKE '%LOOSE%' OR p.Name LIKE '%OIL%' OR p.Name LIKE '%GHEE%' OR p.Name LIKE '%SALT%'
                          OR p.TamilName LIKE N'%கி%' OR p.TamilName LIKE N'%கிலோ%' OR p.TamilName LIKE N'%வெல்லம்%' OR p.TamilName LIKE N'%அரிசி%' OR p.TamilName LIKE N'%பருப்பு%'
                        THEN N'Kgs'
                        WHEN p.Box = 1 THEN N'Box'
                        ELSE N'Pcs'
                    END AS Uom
                FROM Master_Inventory_Product p
                LEFT JOIN Master_Batch b ON b.ProductName = p.ID AND b.Status = 1
                LEFT JOIN Master_Base_GST g ON p.GSTInterStateOutput = g.ID
                LEFT JOIN RecentPurchase rp ON p.ID = rp.ProductCode AND rp.rnk = 1
                LEFT JOIN BatchSupplier bs ON p.ID = bs.ProductCode AND bs.rnk = 1
                WHERE p.Status = 1";

            var existingCodes = new HashSet<string>(await _context.Products.Select(p => p.ProductCode).ToListAsync());
            var productListToInsert = new List<Product>();

            using (var reader = await prodCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var code = reader["ProductCode"].ToString()?.Trim();
                    var name = reader["Name"].ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) continue;

                    if (!existingCodes.Contains(code))
                    {
                        var gstPct = Convert.ToDecimal(reader["GstPercentage"]);
                        var taxSlabId = (gstPct == 5 ? tax5?.Id : gstPct == 12 ? tax12?.Id : gstPct == 18 ? tax18?.Id : gstPct == 28 ? tax28?.Id : tax0?.Id) ?? tax0?.Id ?? Guid.NewGuid();

                        var mappedSuppCode = reader["MappedSupplierCode"].ToString()?.Trim();
                        Guid? preferredSupplierId = null;
                        if (!string.IsNullOrWhiteSpace(mappedSuppCode) && supplierCodeMap.TryGetValue(mappedSuppCode, out var suppId))
                        {
                            preferredSupplierId = suppId;
                        }

                        var uomStr = reader["Uom"].ToString()?.Trim();
                        var isWeighable = Convert.ToInt32(reader["IsWeighable"]) == 1;
                        var targetUomId = uomStr == "Kgs" ? uomKgs.Id : uomStr == "Box" ? uomBox.Id : uomPcs.Id;

                        var p = new Product
                        {
                            Id = Guid.NewGuid(),
                            ProductCode = code,
                            Name = name,
                            TamilName = reader["TamilName"].ToString(),
                            Description = reader["Category"].ToString(),
                            Mrp = Convert.ToDecimal(reader["Mrp"]),
                            SellingPrice = Convert.ToDecimal(reader["SellingPrice"]),
                            PurchasePrice = Convert.ToDecimal(reader["PurchasePrice"]),
                            TaxSlabId = taxSlabId,
                            CategoryId = defaultCategory.Id,
                            UnitOfMeasureId = targetUomId,
                            PreferredSupplierId = preferredSupplierId,
                            IsWeighable = isWeighable,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        var barcodeVal = reader["Barcode"].ToString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(barcodeVal))
                        {
                            p.Barcodes.Add(new Barcode
                            {
                                Id = Guid.NewGuid(),
                                ProductId = p.Id,
                                BarcodeValue = barcodeVal,
                                IsPrimary = true
                            });
                        }

                        productListToInsert.Add(p);
                        existingCodes.Add(code);
                        productsMigrated++;

                        if (productListToInsert.Count >= 1000)
                        {
                            _context.Products.AddRange(productListToInsert);
                            await _context.SaveChangesAsync(default);
                            productListToInsert.Clear();
                        }
                    }
                }
            }

            if (productListToInsert.Count > 0)
            {
                _context.Products.AddRange(productListToInsert);
                await _context.SaveChangesAsync(default);
                productListToInsert.Clear();
            }

            // 5. MIGRATE STOCK BATCHES
            var stockCmd = sqlConn.CreateCommand();
            stockCmd.CommandTimeout = 600;
            stockCmd.CommandText = @"
                SELECT 
                    b.ID AS BatchId,
                    b.ProductName AS ProductCode,
                    ISNULL(b.BatchNo, N'DEFAULT') AS BatchNumber,
                    b.EXPDate AS ExpiryDate,
                    CAST(ISNULL(b.Stock, 0) AS DECIMAL(18,3)) AS CurrentStock,
                    CAST(ISNULL(b.MRP, 0) AS DECIMAL(18,2)) AS Mrp,
                    CAST(ISNULL(b.SalesRate1, 0) AS DECIMAL(18,2)) AS SellingPrice,
                    CAST(ISNULL(b.PurchaseRate, 0) AS DECIMAL(18,2)) AS PurchasePrice
                FROM Master_Batch b
                INNER JOIN Master_Inventory_Product p ON b.ProductName = p.ID
                WHERE b.Status = 1 AND b.Stock > 0";

            var productsDict = await _context.Products.ToDictionaryAsync(p => p.ProductCode, p => p.Id);
            var batchesToInsert = new List<ProductBatch>();

            using (var reader = await stockCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var pCode = reader["ProductCode"].ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(pCode) && productsDict.TryGetValue(pCode, out var productId))
                    {
                        var expDateObj = reader["ExpiryDate"];
                        DateTime? expDate = expDateObj != DBNull.Value ? Convert.ToDateTime(expDateObj) : null;

                        var batch = new ProductBatch
                        {
                            Id = Guid.NewGuid(),
                            ProductId = productId,
                            BatchNumber = reader["BatchNumber"].ToString() ?? "DEFAULT",
                            ExpiryDate = expDate,
                            Mrp = Convert.ToDecimal(reader["Mrp"]),
                            CostPrice = Convert.ToDecimal(reader["PurchasePrice"]),
                            AvailableQuantity = Convert.ToDecimal(reader["CurrentStock"]),
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        batchesToInsert.Add(batch);
                        stockBatchesMigrated++;

                        if (batchesToInsert.Count >= 1000)
                        {
                            _context.ProductBatches.AddRange(batchesToInsert);
                            await _context.SaveChangesAsync(default);
                            batchesToInsert.Clear();
                        }
                    }
                }
            }

            if (batchesToInsert.Count > 0)
            {
                _context.ProductBatches.AddRange(batchesToInsert);
                await _context.SaveChangesAsync(default);
                batchesToInsert.Clear();
            }
        }

        return Ok(new
        {
            Status = "SUCCESS",
            Message = "Sigma 21 Master Data Migration Completed Successfully!",
            CustomersMigrated = customersMigrated,
            SuppliersMigrated = suppliersMigrated,
            ProductsMigrated = productsMigrated,
            StockBatchesMigrated = stockBatchesMigrated
        });
    }

    [HttpPost("backfill-customer-mappings")]
    public async Task<IActionResult> BackfillCustomerMappings(
        [FromQuery] string server = "192.168.1.10",
        [FromQuery] string database = "APPLE26-27",
        [FromQuery] string username = "sa",
        [FromQuery] string password = "Q7!mX#92Lp@Tz4Ks")
    {
        var connStr = $"Server={server};Database={database};User Id={username};Password={password};TrustServerCertificate=True;Connect Timeout=30;";
        int newCustomersMigrated = 0;
        int existingCustomersUpdated = 0;

        using (var sqlConn = new SqlConnection(connStr))
        {
            await sqlConn.OpenAsync();

            var custCmd = sqlConn.CreateCommand();
            custCmd.CommandTimeout = 300;
            custCmd.CommandText = @"
                SELECT 
                    CustomerCode,
                    Name,
                    TamilName,
                    Phone,
                    Email,
                    Address,
                    MembershipCardNumber,
                    LoyaltyPoints,
                    LedgerBalance
                FROM (
                    SELECT 
                        ID AS CustomerCode,
                        LTRIM(RTRIM(Name)) AS Name,
                        ISNULL(PetName, N'') AS TamilName,
                        ISNULL(Mobile1, ISNULL(Mobile2, ISNULL(Phone1, N''))) AS Phone,
                        ISNULL(Email, N'') AS Email,
                        ISNULL(Address1, N'') + N' ' + ISNULL(Address2, N'') AS Address,
                        ISNULL(CustomerID, ID) AS MembershipCardNumber,
                        CAST(ISNULL(BalancePoint, 0) AS DECIMAL(18,2)) AS LoyaltyPoints,
                        CAST(ISNULL(Balance, 0) AS DECIMAL(18,2)) AS LedgerBalance,
                        ROW_NUMBER() OVER (PARTITION BY LTRIM(RTRIM(Name)) ORDER BY ID DESC) AS rnk
                    FROM Master_CRM_PointsCustomer
                    WHERE Name IS NOT NULL AND LEN(LTRIM(RTRIM(Name))) > 0

                    UNION ALL

                    SELECT 
                        ID AS CustomerCode,
                        LTRIM(RTRIM(Name)) AS Name,
                        ISNULL(PetName, N'') AS TamilName,
                        ISNULL(Mobile1, ISNULL(Phone1, N'')) AS Phone,
                        ISNULL(Email, N'') AS Email,
                        ISNULL(Address1, N'') + N' ' + ISNULL(Address2, N'') AS Address,
                        ID AS MembershipCardNumber,
                        0.00 AS LoyaltyPoints,
                        0.00 AS LedgerBalance,
                        1 AS rnk
                    FROM Master_Accounts
                    WHERE FormName = 'Customer' OR AccountType = 'Sundry Debtors' OR AccountType LIKE '%Debtor%'
                ) combined
                WHERE rnk = 1";

            var existingCustomers = await _context.Customers.ToListAsync();
            var customerNameDict = existingCustomers.ToDictionary(c => c.Name.Trim().ToLower(), c => c);
            var customersToInsert = new List<Customer>();

            using (var reader = await custCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var name = reader["Name"].ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    var phone = reader["Phone"].ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(phone)) phone = "0000000000";

                    var points = Convert.ToDecimal(reader["LoyaltyPoints"]);
                    var balance = Convert.ToDecimal(reader["LedgerBalance"]);
                    var cardNo = reader["MembershipCardNumber"].ToString() ?? "";

                    if (customerNameDict.TryGetValue(name.ToLower(), out var existingCust))
                    {
                        bool updated = false;
                        if (existingCust.RunningLoyaltyPoints != points) { existingCust.RunningLoyaltyPoints = points; updated = true; }
                        if (existingCust.RunningWalletBalance != balance) { existingCust.RunningWalletBalance = balance; updated = true; }
                        if (string.IsNullOrWhiteSpace(existingCust.MembershipCardNumber) && !string.IsNullOrWhiteSpace(cardNo)) { existingCust.MembershipCardNumber = cardNo; updated = true; }
                        if (existingCust.Phone == "0000000000" && phone != "0000000000") { existingCust.Phone = phone; updated = true; }
                        if (updated) existingCustomersUpdated++;
                    }
                    else
                    {
                        var newCust = new Customer
                        {
                            Id = Guid.NewGuid(),
                            Name = name,
                            TamilName = reader["TamilName"].ToString(),
                            Phone = phone,
                            Email = reader["Email"].ToString(),
                            Address = reader["Address"].ToString(),
                            MembershipCardNumber = cardNo,
                            RunningLoyaltyPoints = points,
                            RunningWalletBalance = balance,
                            MembershipStatus = "Active"
                        };

                        customersToInsert.Add(newCust);
                        customerNameDict[name.ToLower()] = newCust;
                        newCustomersMigrated++;

                        if (customersToInsert.Count >= 1000)
                        {
                            _context.Customers.AddRange(customersToInsert);
                            await _context.SaveChangesAsync(default);
                            customersToInsert.Clear();
                        }
                    }
                }
            }

            if (customersToInsert.Count > 0)
            {
                _context.Customers.AddRange(customersToInsert);
                await _context.SaveChangesAsync(default);
                customersToInsert.Clear();
            }

            if (existingCustomersUpdated > 0)
            {
                await _context.SaveChangesAsync(default);
            }
        }

        return Ok(new
        {
            Status = "SUCCESS",
            NewCustomersMigrated = newCustomersMigrated,
            ExistingCustomersUpdated = existingCustomersUpdated,
            Message = $"Successfully migrated {newCustomersMigrated} new customer profiles and updated {existingCustomersUpdated} customer balances from Sigma 21!"
        });
    }

    [HttpPost("backfill-stock-mappings")]
    public async Task<IActionResult> BackfillStockMappings(
        [FromQuery] string server = "192.168.1.10",
        [FromQuery] string database = "APPLE26-27",
        [FromQuery] string username = "sa",
        [FromQuery] string password = "Q7!mX#92Lp@Tz4Ks")
    {
        var connStr = $"Server={server};Database={database};User Id={username};Password={password};TrustServerCertificate=True;Connect Timeout=30;";
        int stockBatchesMigrated = 0;
        decimal totalStockQtyMigrated = 0;

        using (var sqlConn = new SqlConnection(connStr))
        {
            await sqlConn.OpenAsync();

            var stockCmd = sqlConn.CreateCommand();
            stockCmd.CommandTimeout = 600;
            stockCmd.CommandText = @"
                SELECT 
                    b.ID AS BatchId,
                    b.ProductName AS ProductCode,
                    ISNULL(b.BatchNo, N'DEFAULT') AS BatchNumber,
                    b.EXPDate AS ExpiryDate,
                    CAST(ISNULL(b.Stock, 0) AS DECIMAL(18,3)) AS CurrentStock,
                    CAST(ISNULL(b.MRP, 0) AS DECIMAL(18,2)) AS Mrp,
                    CAST(ISNULL(b.SalesRate1, 0) AS DECIMAL(18,2)) AS SellingPrice,
                    CAST(ISNULL(b.PurchaseRate, 0) AS DECIMAL(18,2)) AS PurchasePrice
                FROM Master_Batch b
                INNER JOIN Master_Inventory_Product p ON b.ProductName = p.ID
                WHERE b.Status = 1 AND b.Stock > 0";

            var productsDict = await _context.Products.ToDictionaryAsync(p => p.ProductCode, p => p.Id);
            var existingBatches = await _context.ProductBatches.ToListAsync();
            var existingBatchMap = existingBatches.ToDictionary(b => $"{b.ProductId}_{b.BatchNumber}", b => b);

            var batchesToInsert = new List<ProductBatch>();

            using (var reader = await stockCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var pCode = reader["ProductCode"].ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(pCode) && productsDict.TryGetValue(pCode, out var productId))
                    {
                        var batchNo = reader["BatchNumber"].ToString() ?? "DEFAULT";
                        var stockQty = Convert.ToDecimal(reader["CurrentStock"]);
                        var costPrice = Convert.ToDecimal(reader["PurchasePrice"]);
                        var mrp = Convert.ToDecimal(reader["Mrp"]);

                        var expDateObj = reader["ExpiryDate"];
                        DateTime? expDate = expDateObj != DBNull.Value ? Convert.ToDateTime(expDateObj) : null;

                        var key = $"{productId}_{batchNo}";
                        if (existingBatchMap.TryGetValue(key, out var existingBatch))
                        {
                            existingBatch.AvailableQuantity = stockQty;
                            existingBatch.CostPrice = costPrice;
                            existingBatch.Mrp = mrp;
                            existingBatch.ExpiryDate = expDate;
                        }
                        else
                        {
                            var newBatch = new ProductBatch
                            {
                                Id = Guid.NewGuid(),
                                ProductId = productId,
                                BatchNumber = batchNo,
                                ExpiryDate = expDate,
                                Mrp = mrp,
                                CostPrice = costPrice,
                                AvailableQuantity = stockQty,
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow
                            };
                            batchesToInsert.Add(newBatch);
                            existingBatchMap[key] = newBatch;
                        }

                        stockBatchesMigrated++;
                        totalStockQtyMigrated += stockQty;

                        if (batchesToInsert.Count >= 1000)
                        {
                            _context.ProductBatches.AddRange(batchesToInsert);
                            await _context.SaveChangesAsync(default);
                            batchesToInsert.Clear();
                        }
                    }
                }
            }

            if (batchesToInsert.Count > 0)
            {
                _context.ProductBatches.AddRange(batchesToInsert);
                await _context.SaveChangesAsync(default);
                batchesToInsert.Clear();
            }

            await _context.SaveChangesAsync(default);
        }

        return Ok(new
        {
            Status = "SUCCESS",
            StockBatchesMigrated = stockBatchesMigrated,
            TotalStockQtyMigrated = totalStockQtyMigrated,
            Message = $"Successfully migrated {stockBatchesMigrated} stock batches ({totalStockQtyMigrated:N2} total physical stock qty) from Sigma 21!"
        });
    }

    [HttpPost("backfill-uom-mappings")]
    public async Task<IActionResult> BackfillUomMappings()
    {
        await EnsureProductColumnsExistAsync();

        var allUoms = await _context.UnitOfMeasures.ToListAsync();
        var uomPcs = allUoms.FirstOrDefault(u => u.Symbol.Equals("Pcs", StringComparison.OrdinalIgnoreCase) || u.Name.Equals("Pieces", StringComparison.OrdinalIgnoreCase));
        if (uomPcs == null)
        {
            uomPcs = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Pieces", Symbol = "Pcs" };
            _context.UnitOfMeasures.Add(uomPcs);
        }

        var uomKgs = allUoms.FirstOrDefault(u => u.Symbol.Equals("Kgs", StringComparison.OrdinalIgnoreCase) || u.Name.Equals("Kilograms", StringComparison.OrdinalIgnoreCase));
        if (uomKgs == null)
        {
            uomKgs = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Kilograms", Symbol = "Kgs" };
            _context.UnitOfMeasures.Add(uomKgs);
        }

        var uomBox = allUoms.FirstOrDefault(u => u.Symbol.Equals("Box", StringComparison.OrdinalIgnoreCase));
        if (uomBox == null)
        {
            uomBox = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Box", Symbol = "Box" };
            _context.UnitOfMeasures.Add(uomBox);
        }
        await _context.SaveChangesAsync(default);

        var products = await _context.Products.ToListAsync();
        int updatedCount = 0;

        foreach (var p in products)
        {
            var name = (p.Name ?? "").ToUpper();
            var tamilName = (p.TamilName ?? "");

            bool isWeighable = name.Contains("VELLAM") || name.Contains("RICE") || name.Contains("PARUPPU") || name.Contains("SUGAR")
                || name.Contains("DHAL") || name.Contains("DAL") || name.Contains("ATTA") || name.Contains("MAIDA") || name.Contains("RAVA")
                || name.Contains("KG") || name.Contains("1K") || name.Contains("2K") || name.Contains("5K") || name.Contains("10K") || name.Contains("25K")
                || name.Contains("500G") || name.Contains("250G") || name.Contains("100G") || name.Contains("50G") || name.Contains("GRAM") || name.Contains("GRM")
                || name.Contains("KILO") || name.Contains("LOOSE") || name.Contains("OIL") || name.Contains("GHEE") || name.Contains("SALT")
                || tamilName.Contains("கி") || tamilName.Contains("கிலோ") || tamilName.Contains("வெல்லம்") || tamilName.Contains("அரிசி") || tamilName.Contains("பருப்பு");

            var targetUomId = isWeighable ? uomKgs.Id : uomPcs.Id;

            if (p.UnitOfMeasureId != targetUomId || p.IsWeighable != isWeighable)
            {
                p.UnitOfMeasureId = targetUomId;
                p.IsWeighable = isWeighable;
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            await _context.SaveChangesAsync(default);
        }

        return Ok(new
        {
            Status = "SUCCESS",
            UpdatedProductsCount = updatedCount,
            Message = $"Successfully updated UOM & IsWeighable for {updatedCount} products in ERP system!"
        });
    }

    [HttpPost("backfill-supplier-mappings")]
    public async Task<IActionResult> BackfillSupplierMappings(
        [FromQuery] string server = "192.168.1.10",
        [FromQuery] string database = "APPLE26-27",
        [FromQuery] string username = "sa",
        [FromQuery] string password = "Q7!mX#92Lp@Tz4Ks")
    {
        await EnsureProductColumnsExistAsync();

        var connStr = $"Server={server};Database={database};User Id={username};Password={password};TrustServerCertificate=True;Connect Timeout=30;";
        int updatedProductsCount = 0;
        int newSuppliersCreated = 0;

        using (var sqlConn = new SqlConnection(connStr))
        {
            await sqlConn.OpenAsync();

            var suppCmd = sqlConn.CreateCommand();
            suppCmd.CommandText = @"
                SELECT 
                    ID AS SupplierCode, 
                    Name,
                    ISNULL(Mobile1, ISNULL(Phone1, N'')) AS Phone,
                    ISNULL(GSTNO, N'') AS Gstin
                FROM Master_Accounts 
                WHERE FormName = 'Supplier' OR AccountType = 'Sundry Creditors' OR AccountType LIKE '%Creditor%'";

            var dbSuppliers = await _context.Suppliers.ToListAsync();
            var supplierCodeMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            using (var reader = await suppCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var code = reader["SupplierCode"].ToString()?.Trim();
                    var name = reader["Name"].ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name))
                    {
                        var match = dbSuppliers.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                        if (match == null)
                        {
                            match = new Supplier
                            {
                                Id = Guid.NewGuid(),
                                Name = name,
                                Phone = reader["Phone"].ToString(),
                                Gstin = reader["Gstin"].ToString(),
                                PaymentTerms = "NET30",
                                IsActive = true
                            };
                            _context.Suppliers.Add(match);
                            dbSuppliers.Add(match);
                            newSuppliersCreated++;
                        }
                        supplierCodeMap[code] = match.Id;
                    }
                }
            }

            if (newSuppliersCreated > 0)
            {
                await _context.SaveChangesAsync(default);
            }

            var scanCmd = sqlConn.CreateCommand();
            scanCmd.CommandTimeout = 600;
            scanCmd.CommandText = @"
                WITH RecentPurchase AS (
                    SELECT 
                        ProductName AS ProductCode,
                        Account AS SupplierCode,
                        ROW_NUMBER() OVER (PARTITION BY ProductName ORDER BY Date DESC, VNO DESC) AS rnk
                    FROM Trans_Inventory_SOM
                    WHERE FormName = 'Purchase' AND Account IS NOT NULL AND Account <> ''
                ),
                BatchSupplier AS (
                    SELECT 
                        b.ProductName AS ProductCode,
                        b.SupplierName AS SupplierCode,
                        ROW_NUMBER() OVER (PARTITION BY b.ProductName ORDER BY b.ID DESC) AS rnk
                    FROM Master_Batch b
                    WHERE b.SupplierName IS NOT NULL AND b.SupplierName <> ''
                )
                SELECT 
                    p.ID AS ProductCode,
                    COALESCE(rp.SupplierCode, bs.SupplierCode, N'') AS MappedSupplierCode
                FROM Master_Inventory_Product p
                LEFT JOIN RecentPurchase rp ON p.ID = rp.ProductCode AND rp.rnk = 1
                LEFT JOIN BatchSupplier bs ON p.ID = bs.ProductCode AND bs.rnk = 1
                WHERE p.Status = 1 AND COALESCE(rp.SupplierCode, bs.SupplierCode, N'') <> N''";

            var productsDict = await _context.Products.ToDictionaryAsync(p => p.ProductCode, p => p);

            using (var reader = await scanCmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var pCode = reader["ProductCode"].ToString()?.Trim();
                    var suppCode = reader["MappedSupplierCode"].ToString()?.Trim();

                    if (!string.IsNullOrWhiteSpace(pCode) && 
                        !string.IsNullOrWhiteSpace(suppCode) &&
                        productsDict.TryGetValue(pCode, out var product) &&
                        supplierCodeMap.TryGetValue(suppCode, out var suppId))
                    {
                        if (product.PreferredSupplierId != suppId)
                        {
                            product.PreferredSupplierId = suppId;
                            updatedProductsCount++;
                        }
                    }
                }
            }

            if (updatedProductsCount > 0)
            {
                await _context.SaveChangesAsync(default);
            }
        }

        return Ok(new
        {
            Status = "SUCCESS",
            NewSuppliersCreated = newSuppliersCreated,
            UpdatedProductsCount = updatedProductsCount,
            Message = $"Successfully backfilled PreferredSupplierId for {updatedProductsCount} product master items ({newSuppliersCreated} suppliers created) from Sigma 21!"
        });
    }
}
