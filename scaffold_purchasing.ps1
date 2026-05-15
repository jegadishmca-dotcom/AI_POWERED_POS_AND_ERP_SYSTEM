$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Domain\Entities\Purchasing"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Purchasing\Commands\ConfirmGRN"

# 1. Purchasing Entities
@"
using System;
using System.Collections.Generic;

namespace PosErp.Domain.Entities.Purchasing;

public class PurchaseOrderHeader
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid SupplierId { get; set; }
    
    public string PoNumber { get; set; } = string.Empty;
    public DateTime PoDate { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }
    
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT, APPROVED, PARTIAL_GRN, FULL_GRN, CANCELLED
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}

public class PurchaseOrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseOrderHeaderId { get; set; }
    public Guid ProductId { get; set; }
    
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    
    public PurchaseOrderHeader PurchaseOrderHeader { get; set; } = null!;
}

public class GRNHeader
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid PurchaseOrderHeaderId { get; set; }
    public Guid SupplierId { get; set; }
    
    public string GrnNumber { get; set; } = string.Empty;
    public string SupplierInvoiceNumber { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT, CONFIRMED
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    
    public ICollection<GRNItem> Items { get; set; } = new List<GRNItem>();
}

public class GRNItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GRNHeaderId { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? BatchId { get; set; } // Filled when GRN confirmed
    
    // Batch Info collected during GRN
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public DateTime? MfgDate { get; set; }
    
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    
    public GRNHeader GRNHeader { get; set; } = null!;
}

public class PurchaseBillHeader
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid GRNHeaderId { get; set; }
    
    public string BillNumber { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "PENDING_PAYMENT"; 
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    
    public ICollection<PurchaseBillItem> Items { get; set; } = new List<PurchaseBillItem>();
}

public class PurchaseBillItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseBillHeaderId { get; set; }
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public PurchaseBillHeader PurchaseBillHeader { get; set; } = null!;
}
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\Entities\Purchasing\PurchasingEntities.cs" -Encoding utf8

# 2. Update DbContext (Remove old GRN, add new Purchasing Entities)
@"
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Purchasing;

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
    DbSet<StockAdjustment> StockAdjustments { get; }
    
    // Purchasing
    DbSet<PurchaseOrderHeader> PurchaseOrders { get; }
    DbSet<PurchaseOrderItem> PurchaseOrderItems { get; }
    DbSet<GRNHeader> GRNHeaders { get; }
    DbSet<GRNItem> GRNItems { get; }
    DbSet<PurchaseBillHeader> PurchaseBills { get; }
    DbSet<PurchaseBillItem> PurchaseBillItems { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Interfaces\IApplicationDbContext.cs" -Encoding utf8

# 3. ConfirmGRNCommand (3-Way Matching & Ledger Integration)
@"
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Purchasing;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Purchasing.Commands.ConfirmGRN;

public record ConfirmGRNCommand(Guid GrnId, Guid? UserId) : IRequest<bool>;

public class ConfirmGRNCommandHandler : IRequestHandler<ConfirmGRNCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IStockLedgerService _stockLedgerService;

    public ConfirmGRNCommandHandler(IApplicationDbContext context, IStockLedgerService stockLedgerService)
    {
        _context = context;
        _stockLedgerService = stockLedgerService;
    }

    public async Task<bool> Handle(ConfirmGRNCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        try 
        {
            var grn = await _context.GRNHeaders
                .Include(g => g.Items)
                .FirstOrDefaultAsync(g => g.Id == request.GrnId, cancellationToken);

            if (grn == null || grn.Status != "DRAFT") throw new Exception("Invalid GRN or not in DRAFT status");

            var po = await _context.PurchaseOrders
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == grn.PurchaseOrderHeaderId, cancellationToken);
                
            if (po == null) throw new Exception("Purchase Order not found");

            foreach (var item in grn.Items)
            {
                if (item.AcceptedQuantity <= 0) continue;

                // 1. Auto-generate internal batch number if missing (loose items)
                string batchNo = string.IsNullOrWhiteSpace(item.BatchNumber) 
                    ? $"INT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0,4).ToUpper()}" 
                    : item.BatchNumber;

                // 2. Create or find ProductBatch
                var batch = new ProductBatch
                {
                    StoreId = grn.StoreId,
                    ProductId = item.ProductId,
                    BatchNumber = batchNo,
                    ExpiryDate = item.ExpiryDate,
                    MfgDate = item.MfgDate,
                    CostPrice = item.UnitCost,
                    Mrp = item.UnitCost * 1.3m, // Default 30% margin fallback
                    IsActive = true
                };
                _context.ProductBatches.Add(batch);
                item.BatchId = batch.Id; // Link GRN line to this batch

                // 3. Increment PO Received Quantity (3-Way Matching logic)
                var poItem = po.Items.FirstOrDefault(p => p.Id == item.PurchaseOrderItemId);
                if (poItem != null) poItem.ReceivedQuantity += item.AcceptedQuantity;

                // 4. Securely record stock movement via Immutable Ledger
                await _stockLedgerService.RecordMovementAsync(
                    storeId: grn.StoreId ?? Guid.Empty,
                    warehouseId: null,
                    terminalId: null,
                    businessDate: grn.ReceivedDate,
                    productId: item.ProductId,
                    batchId: batch.Id,
                    movementType: "GRN",
                    quantity: item.AcceptedQuantity, // Positive for IN
                    unitCost: item.UnitCost,
                    expiryDate: item.ExpiryDate,
                    referenceDocId: grn.Id,
                    referenceNumber: grn.GrnNumber,
                    userId: request.UserId,
                    cancellationToken: cancellationToken
                );
            }

            grn.Status = "CONFIRMED";
            
            // Auto-update PO status
            bool isFullReceipt = po.Items.All(p => p.ReceivedQuantity >= p.OrderedQuantity);
            po.Status = isFullReceipt ? "FULL_GRN" : "PARTIAL_GRN";

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new Exception("Concurrency conflict during GRN confirmation. Please retry.", ex);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Purchasing\Commands\ConfirmGRN\ConfirmGRNCommand.cs" -Encoding utf8

# 4. Purchasing Schema Migration Script
@"
-- ==============================================================================
-- PHASE 2: PURCHASING (PO -> GRN -> BILL) SCHEMA
-- ==============================================================================

-- To resolve conflict with previous foundational GRN tables, we drop them if they exist
DROP TABLE IF EXISTS goods_receipt_note_items CASCADE;
DROP TABLE IF EXISTS goods_receipt_notes CASCADE;

CREATE TABLE purchase_order_headers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    supplier_id UUID NOT NULL,
    po_number VARCHAR(100) UNIQUE NOT NULL,
    po_date DATE NOT NULL,
    expected_delivery_date DATE NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL,
    status VARCHAR(50) DEFAULT 'DRAFT',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);

CREATE TABLE purchase_order_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    purchase_order_header_id UUID NOT NULL REFERENCES purchase_order_headers(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    ordered_quantity DECIMAL(18,4) NOT NULL,
    received_quantity DECIMAL(18,4) NOT NULL DEFAULT 0,
    unit_cost DECIMAL(18,4) NOT NULL,
    total_cost DECIMAL(18,4) NOT NULL
);

CREATE TABLE grn_headers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    purchase_order_header_id UUID REFERENCES purchase_order_headers(id),
    supplier_id UUID NOT NULL,
    grn_number VARCHAR(100) UNIQUE NOT NULL,
    supplier_invoice_number VARCHAR(100),
    received_date DATE NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL,
    status VARCHAR(50) DEFAULT 'DRAFT',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);

CREATE TABLE grn_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    grn_header_id UUID NOT NULL REFERENCES grn_headers(id) ON DELETE CASCADE,
    purchase_order_item_id UUID NOT NULL REFERENCES purchase_order_items(id),
    product_id UUID NOT NULL REFERENCES products(id),
    batch_id UUID REFERENCES product_batches(id),
    batch_number VARCHAR(100),
    expiry_date DATE,
    mfg_date DATE,
    received_quantity DECIMAL(18,4) NOT NULL,
    accepted_quantity DECIMAL(18,4) NOT NULL,
    rejected_quantity DECIMAL(18,4) NOT NULL,
    unit_cost DECIMAL(18,4) NOT NULL,
    total_cost DECIMAL(18,4) NOT NULL
);

CREATE TABLE purchase_bill_headers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    store_id UUID,
    supplier_id UUID NOT NULL,
    grn_header_id UUID REFERENCES grn_headers(id),
    bill_number VARCHAR(100) UNIQUE NOT NULL,
    bill_date DATE NOT NULL,
    sub_total DECIMAL(18,4) NOT NULL,
    tax_amount DECIMAL(18,4) NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL,
    status VARCHAR(50) DEFAULT 'PENDING_PAYMENT',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);

CREATE TABLE purchase_bill_items (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    purchase_bill_header_id UUID NOT NULL REFERENCES purchase_bill_headers(id) ON DELETE CASCADE,
    product_id UUID NOT NULL REFERENCES products(id),
    quantity DECIMAL(18,4) NOT NULL,
    unit_cost DECIMAL(18,4) NOT NULL,
    tax_amount DECIMAL(18,4) NOT NULL,
    total_amount DECIMAL(18,4) NOT NULL
);
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Persistence\Migrations\04_PurchasingSchema.sql" -Encoding utf8

Write-Host "Purchasing Workflow Scaffolded"
