using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
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

    [HttpPost("execute-sigma21-migration")]
    public async Task<IActionResult> ExecuteSigma21Migration(
        [FromQuery] string server = "192.168.1.10",
        [FromQuery] string database = "APPLE26-27",
        [FromQuery] string username = "sa",
        [FromQuery] string password = "Q7!mX#92Lp@Tz4Ks")
    {
        var connStr = $"Server={server};Database={database};User Id={username};Password={password};TrustServerCertificate=True;Connect Timeout=30;";
        
        int customersMigrated = 0;
        int suppliersMigrated = 0;
        int productsMigrated = 0;
        int stockBatchesMigrated = 0;

        using (var sqlConn = new SqlConnection(connStr))
        {
            await sqlConn.OpenAsync();

            // 1. MIGRATE CUSTOMERS
            var custCmd = sqlConn.CreateCommand();
            custCmd.CommandText = @"
                SELECT 
                    ID AS CustomerCode,
                    Name,
                    ISNULL(PetName, N'') AS TamilName,
                    ISNULL(Mobile1, ISNULL(Phone1, N'')) AS Phone,
                    ISNULL(Email, N'') AS Email,
                    ISNULL(Address1, N'') + N' ' + ISNULL(Address2, N'') AS Address
                FROM Master_Accounts
                WHERE FormName = 'Customer' OR AccountType = 'Sundry Debtors' OR AccountType LIKE '%Debtor%'";

            using (var reader = await custCmd.ExecuteReaderAsync())
            {
                var existingPhones = await _context.Customers.Select(c => c.Phone).ToListAsync();
                while (await reader.ReadAsync())
                {
                    var phone = reader["Phone"].ToString()?.Trim();
                    var name = reader["Name"].ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    if (string.IsNullOrWhiteSpace(phone)) phone = "0000000000";

                    if (!existingPhones.Contains(phone))
                    {
                        var cust = new Customer
                        {
                            Id = Guid.NewGuid(),
                            Name = name,
                            TamilName = reader["TamilName"].ToString(),
                            Phone = phone,
                            Email = reader["Email"].ToString(),
                            Address = reader["Address"].ToString(),
                            MembershipCardNumber = reader["CustomerCode"].ToString() ?? ""
                        };
                        _context.Customers.Add(cust);
                        existingPhones.Add(phone);
                        customersMigrated++;
                    }
                }
            }
            await _context.SaveChangesAsync(default);

            // 2. MIGRATE SUPPLIERS
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

            using (var reader = await suppCmd.ExecuteReaderAsync())
            {
                var existingSuppliers = await _context.Suppliers.Select(s => s.Name.ToLower()).ToListAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader["Name"].ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    if (!existingSuppliers.Contains(name.ToLower()))
                    {
                        var supp = new Supplier
                        {
                            Id = Guid.NewGuid(),
                            Name = name,
                            Phone = reader["Phone"].ToString(),
                            Gstin = reader["Gstin"].ToString(),
                            PaymentTerms = "NET30",
                            IsActive = true
                        };
                        _context.Suppliers.Add(supp);
                        existingSuppliers.Add(name.ToLower());
                        suppliersMigrated++;
                    }
                }
            }
            await _context.SaveChangesAsync(default);

            // 3. GET OR CREATE DEFAULT TAX SLABS & CATEGORY
            var taxSlabs = await _context.TaxSlabs.ToListAsync();
            var tax0 = taxSlabs.FirstOrDefault(t => t.Rate == 0) ?? taxSlabs.FirstOrDefault();
            var tax5 = taxSlabs.FirstOrDefault(t => t.Rate == 5) ?? tax0;
            var tax12 = taxSlabs.FirstOrDefault(t => t.Rate == 12) ?? tax0;
            var tax18 = taxSlabs.FirstOrDefault(t => t.Rate == 18) ?? tax0;
            var tax28 = taxSlabs.FirstOrDefault(t => t.Rate == 28) ?? tax0;

            var defaultCategory = await _context.Categories.FirstOrDefaultAsync();
            if (defaultCategory == null)
            {
                defaultCategory = new Category { Id = Guid.NewGuid(), Name = "General", Code = "GEN" };
                _context.Categories.Add(defaultCategory);
                await _context.SaveChangesAsync(default);
            }

            // 4. MIGRATE PRODUCTS (39,000+ items)
            var prodCmd = sqlConn.CreateCommand();
            prodCmd.CommandTimeout = 600;
            prodCmd.CommandText = @"
                SELECT 
                    p.ID AS ProductCode,
                    p.Name,
                    ISNULL(p.TamilName, N'') AS TamilName,
                    ISNULL(p.Category, N'General') AS Category,
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
                        WHEN p.Weight > 0 OR p.Name LIKE '%KG%' OR p.Name LIKE '%GRAM%' OR p.Name LIKE '%GRM%' THEN 1
                        ELSE 0
                    END AS IsWeighable,
                    CASE 
                        WHEN p.Weight > 0 OR p.Name LIKE '%KG%' OR p.Name LIKE '%GRAM%' OR p.Name LIKE '%GRM%' THEN N'Kgs'
                        WHEN p.Box = 1 THEN N'Box'
                        ELSE N'Pcs'
                    END AS Uom
                FROM Master_Inventory_Product p
                LEFT JOIN Master_Batch b ON b.ProductName = p.ID AND b.Status = 1
                LEFT JOIN Master_Base_GST g ON p.GSTInterStateOutput = g.ID
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
                            PrimaryBarcode = reader["Barcode"].ToString(),
                            SecondaryBarcode = code,
                            TaxSlabId = taxSlabId,
                            CategoryId = defaultCategory.Id,
                            IsWeighable = Convert.ToInt32(reader["IsWeighable"]) == 1,
                            UnitOfMeasure = reader["Uom"].ToString() ?? "Pcs",
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        };

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
                            SellingPrice = Convert.ToDecimal(reader["SellingPrice"]),
                            PurchasePrice = Convert.ToDecimal(reader["PurchasePrice"]),
                            CurrentStock = Convert.ToDecimal(reader["CurrentStock"]),
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
}
