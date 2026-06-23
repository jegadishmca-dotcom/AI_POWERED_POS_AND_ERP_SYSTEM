using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Purchasing;
using PosErp.Domain.Entities.Crm;
using PosErp.Domain.Entities.Offers;
using PosErp.Domain.Entities.Finance;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Barcode> Barcodes => Set<Barcode>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<TaxSlab> TaxSlabs => Set<TaxSlab>();
    public DbSet<UnitOfMeasure> UnitOfMeasures => Set<UnitOfMeasure>();
    public DbSet<GstHsnMasterIndia> GstHsnMaster => Set<GstHsnMasterIndia>();

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<PosSession> PosSessions => Set<PosSession>();
    public DbSet<StoreBusinessDate> StoreBusinessDates => Set<StoreBusinessDate>();
    
    // Inventory
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<StockLedgerEntry> StockLedger => Set<StockLedgerEntry>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<StockTakeHeader> StockTakeHeaders => Set<StockTakeHeader>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<PendingPriceApproval> PendingPriceApprovals => Set<PendingPriceApproval>();
    
    // Purchasing
    public DbSet<PurchaseOrderHeader> PurchaseOrders => Set<PurchaseOrderHeader>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<GRNHeader> GRNHeaders => Set<GRNHeader>();
    public DbSet<GRNItem> GRNItems => Set<GRNItem>();
    public DbSet<PurchaseBillHeader> PurchaseBills => Set<PurchaseBillHeader>();
    public DbSet<PurchaseBillItem> PurchaseBillItems => Set<PurchaseBillItem>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    
    // CRM & Offers
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerTier> CustomerTiers => Set<CustomerTier>();
    public DbSet<WalletLedgerEntry> WalletLedger => Set<WalletLedgerEntry>();
    public DbSet<LoyaltyLedgerEntry> LoyaltyLedger => Set<LoyaltyLedgerEntry>();
    public DbSet<Offer> Offers => Set<Offer>();
    
    // Finance
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalEntryLine> JournalEntryLines => Set<JournalEntryLine>();
    public DbSet<TaxTransaction> TaxTransactions => Set<TaxTransaction>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<SupplierLedgerEntry> SupplierLedger => Set<SupplierLedgerEntry>();
    public DbSet<CustomerLedgerEntry> CustomerLedger => Set<CustomerLedgerEntry>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<SupplierPaymentAllocation> SupplierPaymentAllocations => Set<SupplierPaymentAllocation>();
    public DbSet<CustomerReceipt> CustomerReceipts => Set<CustomerReceipt>();
    public DbSet<CustomerReceiptAllocation> CustomerReceiptAllocations => Set<CustomerReceiptAllocation>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<PettyCashLedgerEntry> PettyCashLedger => Set<PettyCashLedgerEntry>();
    public DbSet<DocumentSequence> DocumentSequences => Set<DocumentSequence>();
    public DbSet<FixedAsset> FixedAssets => Set<FixedAsset>();
    public DbSet<AssetDepreciationHistory> AssetDepreciationHistories => Set<AssetDepreciationHistory>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<FinancialYear> FinancialYears => Set<FinancialYear>();
    public DbSet<FinancialPeriodLock> FinancialPeriodLocks => Set<FinancialPeriodLock>();
    public DbSet<InventoryValuationHistory> InventoryValuationHistory => Set<InventoryValuationHistory>();
    public DbSet<InterStoreTransfer> InterStoreTransfers => Set<InterStoreTransfer>();
    public DbSet<InterStoreTransferItem> InterStoreTransferItems => Set<InterStoreTransferItem>();
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnItem> PurchaseReturnItems => Set<PurchaseReturnItem>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnItem> SalesReturnItems => Set<SalesReturnItem>();
    public DbSet<EInvoiceMetadata> EInvoiceMetadata => Set<EInvoiceMetadata>();
    public DbSet<EWayBillMetadata> EWayBillMetadata => Set<EWayBillMetadata>();
    public DbSet<ApprovalLimit> ApprovalLimits => Set<ApprovalLimit>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalRequestStep> ApprovalRequestSteps => Set<ApprovalRequestStep>();
    public DbSet<DailyFinanceSummary> DailyFinanceSummaries => Set<DailyFinanceSummary>();
    public DbSet<SupplierRebate> SupplierRebates => Set<SupplierRebate>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Composite Key configurations for POS Invoices & Items
        modelBuilder.Entity<Invoice>()
            .HasKey(i => new { i.Id, i.BusinessDate });

        modelBuilder.Entity<Invoice>()
            .Property(i => i.BusinessDate)
            .HasColumnType("date");

        modelBuilder.Entity<Invoice>()
            .Ignore(i => i.TotalDiscount)
            .Ignore(i => i.TaxTotal)
            .Ignore(i => i.QrCodeUrl);
            
        modelBuilder.Entity<InvoiceItem>()
            .HasKey(ii => new { ii.Id, ii.BusinessDate });

        modelBuilder.Entity<InvoiceItem>()
            .Property(ii => ii.BusinessDate)
            .HasColumnType("date");

        modelBuilder.Entity<StoreBusinessDate>()
            .HasKey(sbd => new { sbd.StoreId, sbd.BusinessDate });

        modelBuilder.Entity<StoreBusinessDate>()
            .Property(sbd => sbd.BusinessDate)
            .HasColumnType("date");

        modelBuilder.Entity<InvoiceItem>()
            .Ignore(ii => ii.FinalTotal)
            .Ignore(ii => ii.Total);
            
        modelBuilder.Entity<InvoiceItem>()
            .HasOne(ii => ii.Invoice)
            .WithMany(i => i.Items)
            .HasForeignKey(ii => new { ii.InvoiceId, ii.BusinessDate });

        modelBuilder.Entity<Customer>()
            .Property(c => c.Dob)
            .HasColumnType("date");

        modelBuilder.Entity<Customer>()
            .Property(c => c.Anniversary)
            .HasColumnType("date");

        modelBuilder.Entity<LoyaltyLedgerEntry>()
            .Property(l => l.ExpiryDate)
            .HasColumnType("date");

        // Purchasing & GRN Date mappings to match date column types in PostgreSQL
        modelBuilder.Entity<PurchaseOrderHeader>()
            .Property(p => p.PoDate)
            .HasColumnType("date");

        modelBuilder.Entity<PurchaseOrderHeader>()
            .Property(p => p.ExpectedDeliveryDate)
            .HasColumnType("date");

        modelBuilder.Entity<GRNHeader>()
            .Property(g => g.ReceivedDate)
            .HasColumnType("date");

        modelBuilder.Entity<GRNItem>()
            .Property(gi => gi.MfgDate)
            .HasColumnType("date");

        modelBuilder.Entity<GRNItem>()
            .Property(gi => gi.ExpiryDate)
            .HasColumnType("date");

        // Explicit column name overrides for GRN-prefixed FK columns that the
        // generic ToSnakeCase algorithm mishandles (GRNHeaderId → grnheader_id vs grn_header_id)
        modelBuilder.Entity<GRNItem>()
            .Property(gi => gi.GRNHeaderId)
            .HasColumnName("grn_header_id");

        modelBuilder.Entity<PosErp.Domain.Entities.Inventory.ProductBatch>()
            .Property(pb => pb.MfgDate)
            .HasColumnType("date");

        modelBuilder.Entity<PosErp.Domain.Entities.Inventory.ProductBatch>()
            .Property(pb => pb.ExpiryDate)
            .HasColumnType("date");

        modelBuilder.Entity<PosErp.Domain.Entities.Inventory.StockLedgerEntry>()
            .Property(sl => sl.BusinessDate)
            .HasColumnType("date");

        modelBuilder.Entity<PosErp.Domain.Entities.Inventory.StockLedgerEntry>()
            .Property(sl => sl.ExpiryDate)
            .HasColumnType("date");

        modelBuilder.Entity<PosErp.Domain.Entities.Inventory.StockTakeHeader>()
            .Property(s => s.ScheduledDate)
            .HasColumnType("date");

        modelBuilder.Entity<PosErp.Domain.Entities.Inventory.StockTakeHeader>()
            .HasMany(t => t.Items)
            .WithOne(i => i.StockTakeHeader)
            .HasForeignKey(i => i.StockTakeHeaderId)
            .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<PurchaseBillHeader>()
            .Property(pb => pb.GRNHeaderId)
            .HasColumnName("grn_header_id");

        modelBuilder.Entity<PosErp.Domain.Entities.Catalog.Barcode>()
            .Property(b => b.BarcodeValue)
            .HasColumnName("barcode");

        modelBuilder.Entity<PosErp.Domain.Entities.Catalog.Product>()
            .Ignore(p => p.MinStockLevel)
            .Ignore(p => p.ReorderPoint)
            .Ignore(p => p.SearchVector);

        modelBuilder.Entity<PosErp.Domain.Entities.Catalog.Product>()
            .HasOne<UnitOfMeasure>()
            .WithMany()
            .HasForeignKey(p => p.UnitOfMeasureId)
            .OnDelete(DeleteBehavior.Restrict);

        // StockLedgerEntry.Version (uint) is a C#-side optimistic concurrency field
        // but the stock_ledger table has no such column — ignore it to prevent EF mapping error
        modelBuilder.Entity<PosErp.Domain.Entities.Inventory.StockLedgerEntry>()
            .Ignore(s => s.Version);

        // Composite Key configuration for CustomerReceiptAllocation referencing Invoices
        modelBuilder.Entity<PosErp.Domain.Entities.Finance.CustomerReceiptAllocation>()
            .HasOne<PosErp.Domain.Entities.Pos.Invoice>()
            .WithMany()
            .HasForeignKey(cra => new { cra.InvoiceId, cra.InvoiceBusinessDate })
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SupplierPaymentAllocation>()
            .HasOne(i => i.SupplierPayment)
            .WithMany(p => p.Allocations)
            .HasForeignKey(i => i.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CustomerReceiptAllocation>()
            .HasOne(i => i.CustomerReceipt)
            .WithMany(r => r.Allocations)
            .HasForeignKey(i => i.ReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InterStoreTransferItem>()
            .HasOne(i => i.ProductBatch)
            .WithMany()
            .HasForeignKey(i => i.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Key configuration for SalesReturn referencing Invoices
        modelBuilder.Entity<PosErp.Domain.Entities.Finance.SalesReturn>()
            .HasOne<PosErp.Domain.Entities.Pos.Invoice>()
            .WithMany()
            .HasForeignKey(sr => new { sr.InvoiceId, sr.BusinessDate })
            .OnDelete(DeleteBehavior.Restrict);

        // Composite Key configuration for EInvoiceMetadata referencing Invoices
        modelBuilder.Entity<PosErp.Domain.Entities.Finance.EInvoiceMetadata>()
            .HasOne<PosErp.Domain.Entities.Pos.Invoice>()
            .WithMany()
            .HasForeignKey(em => new { em.InvoiceId, em.BusinessDate })
            .OnDelete(DeleteBehavior.Restrict);

        // Optimistic concurrency protection using PG xmin system column
        modelBuilder.Entity<Account>()
            .Property<uint>("Version")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();

        modelBuilder.Entity<JournalEntry>()
            .Property<uint>("Version")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsRowVersion();

        // Foreign Key configurations for new entities
        modelBuilder.Entity<JournalEntryLine>()
            .HasOne<CostCenter>()
            .WithMany()
            .HasForeignKey(jl => jl.CostCenterId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ApprovalRequestStep>()
            .HasOne(s => s.ApprovalRequest)
            .WithMany(r => r.Steps)
            .HasForeignKey(s => s.ApprovalRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PurchaseReturnItem>()
            .HasOne(i => i.ProductBatch)
            .WithMany()
            .HasForeignKey(i => i.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SalesReturnItem>()
            .HasOne(i => i.ProductBatch)
            .WithMany()
            .HasForeignKey(i => i.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Map every entity and property to lowercase snake_case to match SQL Schema exactly
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName != null)
            {
                string snakeTableName = ToSnakeCase(tableName);
                if (snakeTableName == "purchase_orders") snakeTableName = "purchase_order_headers";
                else if (snakeTableName == "purchase_bills") snakeTableName = "purchase_bill_headers";
                else if (tableName == "GRNHeaders" || snakeTableName == "g_r_n_headers" || snakeTableName == "grnheaders") snakeTableName = "grn_headers";
                else if (tableName == "GRNItems" || snakeTableName == "g_r_n_items" || snakeTableName == "grnitems") snakeTableName = "grn_items";
                else if (snakeTableName == "refresh_tokens") snakeTableName = "refresh_tokens";
                else if (snakeTableName == "store_business_date" || snakeTableName == "store_business_dates") snakeTableName = "store_business_dates";
                else if (snakeTableName == "stock_ledger_entrys" || snakeTableName == "stock_ledger_entries") snakeTableName = "stock_ledger";
                else if (snakeTableName == "wallet_ledger_entrys" || snakeTableName == "wallet_ledger_entries") snakeTableName = "wallet_ledger";
                else if (snakeTableName == "loyalty_ledger_entrys" || snakeTableName == "loyalty_ledger_entries") snakeTableName = "loyalty_ledger";
                else if (snakeTableName == "stock_adjustment_item") snakeTableName = "stock_adjustment_items";
                else if (snakeTableName == "stock_take_item") snakeTableName = "stock_take_items";
                else if (snakeTableName == "bin") snakeTableName = "bins";
                else if (snakeTableName == "gst_hsn_master" || tableName == "GstHsnMaster") snakeTableName = "gst_hsn_master_india";
                else if (snakeTableName == "asset_depreciation_histories") snakeTableName = "asset_depreciation_history";
                else if (snakeTableName == "e_invoice_metadata" || snakeTableName == "einvoice_metadata") snakeTableName = "einvoice_metadata";
                else if (snakeTableName == "e_way_bill_metadata" || snakeTableName == "ewaybill_metadata") snakeTableName = "ewaybill_metadata";
                else if (snakeTableName == "daily_finance_summaries" || snakeTableName == "daily_finance_summary") snakeTableName = "daily_finance_summary";
                else if (snakeTableName == "approval_request_steps" || snakeTableName == "approval_request_step") snakeTableName = "approval_request_steps";
                else if (snakeTableName == "supplier_rebates" || snakeTableName == "supplier_rebate") snakeTableName = "supplier_rebates";
                
                entity.SetTableName(snakeTableName);
            }

            foreach (var property in entity.GetProperties())
            {
                if (entity.ClrType == typeof(PosErp.Domain.Entities.Catalog.Barcode) && property.Name == nameof(PosErp.Domain.Entities.Catalog.Barcode.BarcodeValue))
                {
                    property.SetColumnName("barcode");
                }
                else if (property.Name == "Version" && (entity.ClrType == typeof(Account) || entity.ClrType == typeof(JournalEntry)))
                {
                    property.SetColumnName("xmin");
                }
                else if (property.Name == "CreatedById")
                {
                    property.SetColumnName("created_by");
                }
                else
                {
                    entity.FindProperty(property.Name)?.SetColumnName(ToSnakeCase(property.Name));
                }
            }
        }
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
            {
                if (i > 0 && input[i - 1] != '_')
                {
                    // Insert underscore when transitioning from lowercase to uppercase
                    // e.g. "orderedQuantity" → "ordered_quantity"
                    if (!char.IsUpper(input[i - 1]))
                    {
                        sb.Append('_');
                    }
                    // Also insert underscore at end-of-acronym boundary:
                    // when previous is uppercase and NEXT is lowercase
                    // e.g. "GRNHeaderId" → "grn_header_id" (underscore before 'H')
                    else if (i + 1 < input.Length && char.IsLower(input[i + 1]))
                    {
                        sb.Append('_');
                    }
                }
                sb.Append(char.ToLower(input[i]));
            }
            else
            {
                sb.Append(input[i]);
            }
        }
        // Normalize abbreviations like GRN or UOM
        return sb.ToString()
            .Replace("g_r_n", "grn")
            .Replace("u_o_m", "uom")
            .Replace("c_o_a", "coa")
            .Replace("p_o_s", "pos");
    }
}
