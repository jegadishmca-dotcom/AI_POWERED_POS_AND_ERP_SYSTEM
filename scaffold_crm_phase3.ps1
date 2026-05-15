$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Domain\Entities\Crm"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Domain\Entities\Offers"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Crm\Commands\RegisterCustomer"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Crm\Queries\SearchCustomers"
New-Item -ItemType Directory -Force -Path "$frontendDir\src\features\crm\components"

# 1. CRM Domain Entities
@"
using System;
using System.Collections.Generic;

namespace PosErp.Domain.Entities.Crm;

public class CustomerTier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty; // Silver, Gold, Platinum
    public int Level { get; set; } // 1, 2, 3
    public decimal MinimumSpend { get; set; } // Rolling 12-month threshold
    public decimal PointsEarnMultiplier { get; set; } = 1.0m;
}

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Phone { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? TamilName { get; set; }
    
    public DateTime? Dob { get; set; }
    public DateTime? Anniversary { get; set; }
    
    // DPDP Consent flags
    public bool MarketingConsent { get; set; } = false;
    public bool AnalyticsConsent { get; set; } = false;
    public DateTime? ConsentRecordedAt { get; set; }
    
    public Guid? CustomerTierId { get; set; }
    public CustomerTier? Tier { get; set; }
    
    public string MembershipCardNumber { get; set; } = string.Empty;
    
    // Denormalized running balances for fast UI, actual truth is in Ledgers
    public decimal RunningWalletBalance { get; set; }
    public decimal RunningLoyaltyPoints { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class WalletLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid? StoreId { get; set; }
    
    public string TransactionType { get; set; } = string.Empty; // TOPUP, SPEND, REFUND
    public decimal Amount { get; set; } // +ve for Topup/Refund, -ve for Spend
    public string ReferenceDocument { get; set; } = string.Empty; // Invoice No or Payment Ref
    
    public decimal RunningBalance { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
}

public class LoyaltyLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid? StoreId { get; set; }
    
    public string TransactionType { get; set; } = string.Empty; // EARN, BURN, EXPIRED
    public decimal Points { get; set; } // +ve for Earn, -ve for Burn/Expired
    public string ReferenceDocument { get; set; } = string.Empty; // Invoice No
    
    public DateTime? ExpiryDate { get; set; } // Null if burnt immediately, else usually +365 days
    
    public decimal RunningPoints { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\Entities\Crm\CrmEntities.cs" -Encoding utf8

# 2. Offer Entity
@"
using System;

namespace PosErp.Domain.Entities.Offers;

public class Offer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public string OfferType { get; set; } = string.Empty; // PERCENTAGE, FLAT, BOGO, COMBO
    public string RulesJson { get; set; } = "{}"; // Complex rules engine configuration
    
    public int Priority { get; set; } = 0; // Higher runs first
    public bool IsStackable { get; set; } = false; // Can this combine with others?
    
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\Entities\Offers\Offer.cs" -Encoding utf8

# 3. Update DbContext
@"
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
    DbSet<StockTakeHeader> StockTakeHeaders { get; }
    DbSet<Warehouse> Warehouses { get; }
    
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
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Interfaces\IApplicationDbContext.cs" -Encoding utf8

# 4. Database Schema Migration
@"
-- ==============================================================================
-- PHASE 3: CRM, LOYALTY, WALLET & OFFERS
-- ==============================================================================

CREATE TABLE customer_tiers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(50) NOT NULL UNIQUE,
    level INT NOT NULL,
    minimum_spend DECIMAL(18,4) NOT NULL DEFAULT 0,
    points_earn_multiplier DECIMAL(18,4) NOT NULL DEFAULT 1.0
);

CREATE TABLE customers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    phone VARCHAR(20) UNIQUE NOT NULL,
    name VARCHAR(200) NOT NULL,
    tamil_name VARCHAR(200),
    dob DATE,
    anniversary DATE,
    marketing_consent BOOLEAN DEFAULT FALSE,
    analytics_consent BOOLEAN DEFAULT FALSE,
    consent_recorded_at TIMESTAMP WITH TIME ZONE,
    customer_tier_id UUID REFERENCES customer_tiers(id),
    membership_card_number VARCHAR(100) UNIQUE,
    running_wallet_balance DECIMAL(18,4) DEFAULT 0,
    running_loyalty_points DECIMAL(18,4) DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_customers_phone ON customers(phone);

CREATE TABLE wallet_ledger (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    store_id UUID,
    transaction_type VARCHAR(50) NOT NULL, -- TOPUP, SPEND, REFUND
    amount DECIMAL(18,4) NOT NULL,
    reference_document VARCHAR(100) NOT NULL,
    running_balance DECIMAL(18,4) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);

CREATE INDEX idx_wallet_ledger_customer ON wallet_ledger(customer_id, created_at DESC);

CREATE TABLE loyalty_ledger (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    store_id UUID,
    transaction_type VARCHAR(50) NOT NULL, -- EARN, BURN, EXPIRED
    points DECIMAL(18,4) NOT NULL,
    reference_document VARCHAR(100) NOT NULL,
    expiry_date DATE,
    running_points DECIMAL(18,4) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    created_by UUID
);

CREATE INDEX idx_loyalty_ledger_customer ON loyalty_ledger(customer_id, created_at DESC);

CREATE TABLE offers (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(200) NOT NULL,
    description TEXT,
    offer_type VARCHAR(50) NOT NULL,
    rules_json JSONB NOT NULL DEFAULT '{}',
    priority INT DEFAULT 0,
    is_stackable BOOLEAN DEFAULT FALSE,
    start_date TIMESTAMP WITH TIME ZONE NOT NULL,
    end_date TIMESTAMP WITH TIME ZONE NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_offers_active ON offers(start_date, end_date) WHERE is_active = TRUE;
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Persistence\Migrations\07_CrmAndOffersSchema.sql" -Encoding utf8

# 5. Commands and Queries
@"
using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Crm;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace PosErp.Application.Features.Crm.Commands.RegisterCustomer;

public record RegisterCustomerCommand(
    string Phone,
    string Name,
    string? TamilName,
    DateTime? Dob,
    bool MarketingConsent
) : IRequest<Guid>;

public class RegisterCustomerCommandHandler : IRequestHandler<RegisterCustomerCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public RegisterCustomerCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(RegisterCustomerCommand request, CancellationToken cancellationToken)
    {
        var existing = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == request.Phone, cancellationToken);
        if (existing != null) throw new Exception("Customer with this phone already exists.");

        var customer = new Customer
        {
            Phone = request.Phone,
            Name = request.Name,
            TamilName = request.TamilName,
            Dob = request.Dob,
            MarketingConsent = request.MarketingConsent,
            ConsentRecordedAt = DateTime.UtcNow,
            MembershipCardNumber = $"MEM-{DateTime.UtcNow:yyMMdd}-{Guid.NewGuid().ToString().Substring(0,4).ToUpper()}"
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        return customer.Id;
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Crm\Commands\RegisterCustomer\RegisterCustomerCommand.cs" -Encoding utf8

@"
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Crm.Queries.SearchCustomers;

public record SearchCustomersQuery(string Query) : IRequest<List<CustomerDto>>;

public record CustomerDto(Guid Id, string Phone, string Name, decimal WalletBalance, decimal LoyaltyPoints, string TierName);

public class SearchCustomersQueryHandler : IRequestHandler<SearchCustomersQuery, List<CustomerDto>>
{
    private readonly IApplicationDbContext _context;

    public SearchCustomersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<CustomerDto>> Handle(SearchCustomersQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length < 3) return new List<CustomerDto>();

        return await _context.Customers
            .Include(c => c.Tier)
            .Where(c => c.Phone.Contains(request.Query) || c.Name.ToLower().Contains(request.Query.ToLower()))
            .Select(c => new CustomerDto(
                c.Id, c.Phone, c.Name, c.RunningWalletBalance, c.RunningLoyaltyPoints, c.Tier != null ? c.Tier.Name : "Base"
            ))
            .Take(10)
            .ToListAsync(cancellationToken);
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Crm\Queries\SearchCustomers\SearchCustomersQuery.cs" -Encoding utf8

# 6. Frontend CRM Components
@"
import React, { useState } from 'react';
import { UserPlus, Save, ShieldCheck } from 'lucide-react';

export const CustomerRegistrationModal = ({ isOpen, onClose, onRegister }: any) => {
  const [phone, setPhone] = useState('');
  const [name, setName] = useState('');
  const [tamilName, setTamilName] = useState('');
  const [dob, setDob] = useState('');
  const [marketingConsent, setMarketingConsent] = useState(false);

  if (!isOpen) return null;

  const handleSave = () => {
    // In real app: mutate with React Query to RegisterCustomerCommand
    onRegister({ phone, name, tamilName, dob, marketingConsent });
    onClose();
  };

  return (
    <div className="fixed inset-0 bg-black/60 z-[60] flex items-center justify-center p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-lg overflow-hidden flex flex-col">
        <div className="bg-indigo-600 p-4 flex justify-between items-center text-white">
          <h2 className="text-xl font-bold flex items-center"><UserPlus className="mr-2" /> New Customer Registration</h2>
          <button onClick={onClose} className="font-bold text-xl">&times;</button>
        </div>
        
        <div className="p-6 space-y-4">
          <div>
            <label className="block text-sm font-bold text-gray-700 mb-1">Mobile Number *</label>
            <input 
              type="tel" 
              className="w-full p-2 border rounded focus:border-indigo-600 outline-none font-bold text-lg" 
              placeholder="10-digit number"
              value={phone}
              onChange={e => setPhone(e.target.value)}
              autoFocus
            />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-1">Full Name *</label>
              <input 
                type="text" 
                className="w-full p-2 border rounded focus:border-indigo-600 outline-none" 
                value={name}
                onChange={e => setName(e.target.value)}
              />
            </div>
            <div>
              <label className="block text-sm font-bold text-gray-700 mb-1">Tamil Name</label>
              <input 
                type="text" 
                className="w-full p-2 border rounded focus:border-indigo-600 outline-none" 
                value={tamilName}
                onChange={e => setTamilName(e.target.value)}
              />
            </div>
          </div>
          <div>
            <label className="block text-sm font-bold text-gray-700 mb-1">Date of Birth (For Offers)</label>
            <input 
              type="date" 
              className="w-full p-2 border rounded outline-none" 
              value={dob}
              onChange={e => setDob(e.target.value)}
            />
          </div>
          
          <div className="bg-slate-50 p-3 rounded border flex items-start gap-3">
            <input 
              type="checkbox" 
              id="dpdp" 
              className="mt-1 w-5 h-5"
              checked={marketingConsent}
              onChange={e => setMarketingConsent(e.target.checked)}
            />
            <label htmlFor="dpdp" className="text-sm text-gray-700">
              <strong className="flex items-center text-slate-800"><ShieldCheck className="w-4 h-4 mr-1 text-emerald-600" /> DPDP Consent</strong>
              I agree to receive promotional offers and understand my data is stored securely.
            </label>
          </div>
        </div>

        <div className="p-4 bg-gray-50 border-t flex justify-end">
          <button 
            onClick={handleSave}
            className="px-6 py-2 bg-indigo-600 text-white rounded font-bold shadow hover:bg-indigo-700 flex items-center"
          >
            <Save className="w-5 h-5 mr-2" /> Register Customer
          </button>
        </div>
      </div>
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\crm\components\CustomerRegistrationModal.tsx" -Encoding utf8

Write-Host "CRM Scaffolding Complete"
