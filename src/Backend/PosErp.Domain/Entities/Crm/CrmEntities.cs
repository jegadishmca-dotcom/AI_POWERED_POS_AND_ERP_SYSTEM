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
