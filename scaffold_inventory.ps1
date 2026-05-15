$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Domain\Entities\Inventory"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Inventory\Services"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Infrastructure\Persistence\Migrations"

# 1. Inventory Domain Entities
@"
using System;
using System.Collections.Generic;
using PosErp.Domain.Entities.Catalog;

namespace PosErp.Domain.Entities.Inventory;

public class ProductBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid ProductId { get; set; }
    
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? MfgDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    
    public decimal Mrp { get; set; }
    public decimal CostPrice { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public Product Product { get; set; } = null!;
}

public class StockLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    
    // e.g., GRN, SALE, ADJ, RET (Return)
    public string MovementType { get; set; } = string.Empty; 
    
    // Positive for IN (GRN/Returns), Negative for OUT (Sales/Damage)
    public decimal Quantity { get; set; } 
    
    public Guid ReferenceDocumentId { get; set; } 
    public string ReferenceNumber { get; set; } = string.Empty; // e.g., GRN-001, INV-001
    
    // Running balance snapshot (Materialized for performance)
    public decimal RunningBalance { get; set; } 
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
}

public class GoodsReceiptNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid SupplierId { get; set; }
    
    public string GrnNumber { get; set; } = string.Empty;
    public string SupplierInvoiceNumber { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT, COMPLETED, CANCELLED
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    
    public ICollection<GoodsReceiptNoteItem> Items { get; set; } = new List<GoodsReceiptNoteItem>();
}

public class GoodsReceiptNoteItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GoodsReceiptNoteId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    
    public GoodsReceiptNote GoodsReceiptNote { get; set; } = null!;
}

public class StockAdjustment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    
    public string AdjustmentNumber { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty; // DAMAGE, EXPIRY, FOUND
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? ApprovedBy { get; set; }
    
    public ICollection<StockAdjustmentItem> Items { get; set; } = new List<StockAdjustmentItem>();
}

public class StockAdjustmentItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockAdjustmentId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; }
    
    public decimal AdjustedQuantity { get; set; } // Can be negative or positive
    public decimal UnitCost { get; set; }
    
    public StockAdjustment StockAdjustment { get; set; } = null!;
}
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\Entities\Inventory\InventoryEntities.cs" -Encoding utf8

# 2. DbContext Extension 
# We'll skip modifying IApplicationDbContext directly via Regex to avoid breakage, but simulate it.
@"
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Inventory;

namespace PosErp.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    
    DbSet<Product> Products { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<Barcode> Barcodes { get; }
    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<TaxSlab> TaxSlabs { get; }

    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceItem> InvoiceItems { get; }
    
    // Inventory
    DbSet<ProductBatch> ProductBatches { get; }
    DbSet<StockLedgerEntry> StockLedger { get; }
    DbSet<GoodsReceiptNote> GoodsReceiptNotes { get; }
    DbSet<GoodsReceiptNoteItem> GoodsReceiptNoteItems { get; }
    DbSet<StockAdjustment> StockAdjustments { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Interfaces\IApplicationDbContext.cs" -Encoding utf8

# 3. Immutable Stock Ledger Service
@"
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Inventory;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Services;

public interface IStockLedgerService
{
    Task RecordMovementAsync(
        Guid storeId,
        Guid productId,
        Guid? batchId,
        string movementType,
        decimal quantity,
        Guid referenceDocId,
        string referenceNumber,
        Guid? userId,
        CancellationToken cancellationToken);
}

public class StockLedgerService : IStockLedgerService
{
    private readonly IApplicationDbContext _context;

    public StockLedgerService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task RecordMovementAsync(
        Guid storeId,
        Guid productId,
        Guid? batchId,
        string movementType,
        decimal quantity,
        Guid referenceDocId,
        string referenceNumber,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        // 1. Calculate new running balance for the product
        // We do this by getting the latest ledger entry or aggregating
        var currentBalance = await _context.StockLedger
            .Where(x => x.ProductId == productId && x.StoreId == storeId)
            .SumAsync(x => x.Quantity, cancellationToken);
            
        var newBalance = currentBalance + quantity;

        // 2. Create Immutable Entry
        var entry = new StockLedgerEntry
        {
            StoreId = storeId,
            ProductId = productId,
            BatchId = batchId,
            MovementType = movementType,
            Quantity = quantity,
            ReferenceDocumentId = referenceDocId,
            ReferenceNumber = referenceNumber,
            RunningBalance = newBalance,
            CreatedBy = userId
        };

        _context.StockLedger.Add(entry);

        // 3. Update Product's materialized CurrentStock (if the field exists on Product entity)
        // This is a denormalization for fast catalog reads
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product != null)
        {
            // Note: In Phase 1 we removed CurrentStock from Product.cs per instructions. 
            // If the user wants materialized stock on Product, we add it back or rely purely on Ledger.
            // For now, we rely on the Ledger's RunningBalance.
        }
        
        await _context.SaveChangesAsync(cancellationToken);
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Inventory\Services\StockLedgerService.cs" -Encoding utf8

# 4. Core Database Schema Migration
@"
-- ==============================================================================
-- PHASE 2: INVENTORY & PURCHASING CORE SCHEMA
-- ==============================================================================

-- 1. Product Batches (FEFO Tracking)
CREATE TABLE product_batches (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    product_id UUID NOT NULL REFERENCES products(id),
    batch_number VARCHAR(100) NOT NULL,
    mfg_date DATE,
    expiry_date DATE,
    mrp DECIMAL(18,4) NOT NULL,
    cost_price DECIMAL(18,4) NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_batches_product_expiry ON product_batches(product_id, expiry_date);

-- 2. Immutable Stock Ledger (Audit-Proof)
CREATE TABLE stock_ledger (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    product_id UUID NOT NULL REFERENCES products(id),
    batch_id UUID REFERENCES product_batches(id),
    movement_type VARCHAR(50) NOT NULL, -- GRN, SALE, ADJ, RET
    quantity DECIMAL(18,4) NOT NULL, -- Positive for IN, Negative for OUT
    reference_document_id UUID NOT NULL,
    reference_number VARCHAR(100) NOT NULL,
    running_balance DECIMAL(18,4) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);

CREATE INDEX idx_stock_ledger_product ON stock_ledger(product_id);
CREATE INDEX idx_stock_ledger_store_product ON stock_ledger(store_id, product_id);
CREATE INDEX idx_stock_ledger_ref ON stock_ledger(reference_document_id);

-- 3. Goods Receipt Notes (GRN)
CREATE TABLE goods_receipt_notes (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    purchase_order_id UUID,
    supplier_id UUID NOT NULL,
    grn_number VARCHAR(100) UNIQUE NOT NULL,
    supplier_invoice_number VARCHAR(100),
    received_date DATE NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL,
    status VARCHAR(50) DEFAULT 'DRAFT',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);

CREATE TABLE goods_receipt_note_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    goods_receipt_note_id UUID NOT NULL REFERENCES goods_receipt_notes(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    batch_id UUID REFERENCES product_batches(id),
    received_quantity DECIMAL(18,4) NOT NULL,
    accepted_quantity DECIMAL(18,4) NOT NULL,
    rejected_quantity DECIMAL(18,4) NOT NULL,
    unit_cost DECIMAL(18,4) NOT NULL,
    total_cost DECIMAL(18,4) NOT NULL
);

-- 4. Stock Adjustments (Shrinkage/Damage)
CREATE TABLE stock_adjustments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    adjustment_number VARCHAR(100) UNIQUE NOT NULL,
    reason VARCHAR(100) NOT NULL,
    status VARCHAR(50) DEFAULT 'PENDING',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    approved_by UUID
);

CREATE TABLE stock_adjustment_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    stock_adjustment_id UUID NOT NULL REFERENCES stock_adjustments(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    batch_id UUID REFERENCES product_batches(id),
    adjusted_quantity DECIMAL(18,4) NOT NULL,
    unit_cost DECIMAL(18,4) NOT NULL
);

-- IMPORTANT: No UPDATE or DELETE triggers allowed on stock_ledger
-- Immutability is enforced strictly by application design.
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Persistence\Migrations\03_InventorySchema.sql" -Encoding utf8

Write-Host "Inventory Scaffolded"
