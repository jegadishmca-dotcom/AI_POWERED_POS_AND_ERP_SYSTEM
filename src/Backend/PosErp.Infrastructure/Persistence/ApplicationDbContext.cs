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
    
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Barcode> Barcodes => Set<Barcode>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<TaxSlab> TaxSlabs => Set<TaxSlab>();

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    
    // Inventory
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<StockLedgerEntry> StockLedger => Set<StockLedgerEntry>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<StockTakeHeader> StockTakeHeaders => Set<StockTakeHeader>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    
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
            
        modelBuilder.Entity<InvoiceItem>()
            .HasKey(ii => new { ii.Id, ii.BusinessDate });
            
        modelBuilder.Entity<InvoiceItem>()
            .HasOne(ii => ii.Invoice)
            .WithMany(i => i.Items)
            .HasForeignKey(ii => new { ii.InvoiceId, ii.BusinessDate });
    }
}
