using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Pos;
using PosErp.Domain.Entities.Inventory;
using PosErp.Domain.Entities.Purchasing;
using PosErp.Domain.Entities.Crm;
using PosErp.Domain.Entities.Offers;
using PosErp.Domain.Entities.Finance;

namespace PosErp.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Role> Roles { get; }
    DbSet<Terminal> Terminals { get; }
    
    DbSet<Product> Products { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<Barcode> Barcodes { get; }
    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<TaxSlab> TaxSlabs { get; }
    DbSet<UnitOfMeasure> UnitOfMeasures { get; }
    DbSet<GstHsnMasterIndia> GstHsnMaster { get; }

    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceItem> InvoiceItems { get; }
    DbSet<PosSession> PosSessions { get; }
    DbSet<StoreBusinessDate> StoreBusinessDates { get; }
    
    // Inventory
    DbSet<ProductBatch> ProductBatches { get; }
    DbSet<StockLedgerEntry> StockLedger { get; }
    DbSet<StockAdjustment> StockAdjustments { get; }
    DbSet<StockTakeHeader> StockTakeHeaders { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<PendingPriceApproval> PendingPriceApprovals { get; }
    
    // Purchasing
    DbSet<PurchaseOrderHeader> PurchaseOrders { get; }
    DbSet<PurchaseOrderItem> PurchaseOrderItems { get; }
    DbSet<GRNHeader> GRNHeaders { get; }
    DbSet<GRNItem> GRNItems { get; }
    DbSet<PurchaseBillHeader> PurchaseBills { get; }
    DbSet<PurchaseBillItem> PurchaseBillItems { get; }
    DbSet<Supplier> Suppliers { get; }
    
    // CRM & Offers
    DbSet<Customer> Customers { get; }
    DbSet<CustomerTier> CustomerTiers { get; }
    DbSet<WalletLedgerEntry> WalletLedger { get; }
    DbSet<LoyaltyLedgerEntry> LoyaltyLedger { get; }
    DbSet<Offer> Offers { get; }
    
    // Finance
    DbSet<Account> Accounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalEntryLine> JournalEntryLines { get; }
    DbSet<TaxTransaction> TaxTransactions { get; }
    DbSet<Store> Stores { get; }
    DbSet<SupplierLedgerEntry> SupplierLedger { get; }
    DbSet<CustomerLedgerEntry> CustomerLedger { get; }
    DbSet<SupplierPayment> SupplierPayments { get; }
    DbSet<SupplierPaymentAllocation> SupplierPaymentAllocations { get; }
    DbSet<CustomerReceipt> CustomerReceipts { get; }
    DbSet<CustomerReceiptAllocation> CustomerReceiptAllocations { get; }
    DbSet<BankAccount> BankAccounts { get; }
    DbSet<BankTransaction> BankTransactions { get; }
    DbSet<PettyCashLedgerEntry> PettyCashLedger { get; }
    DbSet<DocumentSequence> DocumentSequences { get; }
    DbSet<FixedAsset> FixedAssets { get; }
    DbSet<AssetDepreciationHistory> AssetDepreciationHistories { get; }
    DbSet<CostCenter> CostCenters { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<FinancialYear> FinancialYears { get; }
    DbSet<FinancialPeriodLock> FinancialPeriodLocks { get; }
    DbSet<InventoryValuationHistory> InventoryValuationHistory { get; }
    DbSet<InterStoreTransfer> InterStoreTransfers { get; }
    DbSet<InterStoreTransferItem> InterStoreTransferItems { get; }
    DbSet<PurchaseReturn> PurchaseReturns { get; }
    DbSet<PurchaseReturnItem> PurchaseReturnItems { get; }
    DbSet<SalesReturn> SalesReturns { get; }
    DbSet<SalesReturnItem> SalesReturnItems { get; }
    DbSet<EInvoiceMetadata> EInvoiceMetadata { get; }
    DbSet<EWayBillMetadata> EWayBillMetadata { get; }
    DbSet<ApprovalLimit> ApprovalLimits { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<ApprovalRequestStep> ApprovalRequestSteps { get; }
    DbSet<DailyFinanceSummary> DailyFinanceSummaries { get; }
    DbSet<SupplierRebate> SupplierRebates { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

