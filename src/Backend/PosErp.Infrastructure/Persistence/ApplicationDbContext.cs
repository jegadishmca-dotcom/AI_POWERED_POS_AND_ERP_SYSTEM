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
        // Map every entity and property to lowercase snake_case to match SQL Schema exactly
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName != null)
            {
                // Pluralize/SnakeCase overrides for explicit table names
                string snakeTableName = ToSnakeCase(tableName);
                if (snakeTableName == "purchase_orders") snakeTableName = "purchase_order_headers";
                else if (snakeTableName == "purchase_bills") snakeTableName = "purchase_bill_headers";
                else if (snakeTableName == "g_r_n_headers") snakeTableName = "grn_headers";
                else if (snakeTableName == "g_r_n_items") snakeTableName = "grn_items";
                else if (snakeTableName == "refresh_tokens") snakeTableName = "refresh_tokens";
                else if (snakeTableName == "stock_ledger_entrys" || snakeTableName == "stock_ledger_entries") snakeTableName = "stock_ledger";
                else if (snakeTableName == "wallet_ledger_entrys" || snakeTableName == "wallet_ledger_entries") snakeTableName = "wallet_ledger";
                else if (snakeTableName == "loyalty_ledger_entrys" || snakeTableName == "loyalty_ledger_entries") snakeTableName = "loyalty_ledger";
                
                entity.SetTableName(snakeTableName);
            }

            foreach (var property in entity.GetProperties())
            {
                entity.FindProperty(property.Name)?.SetColumnName(ToSnakeCase(property.Name));
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
                if (i > 0 && input[i - 1] != '_' && !char.IsUpper(input[i - 1]))
                {
                    sb.Append('_');
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
