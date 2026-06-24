using System;
using System.Collections.Generic;

namespace PosErp.Domain.Entities.Crm;

public class CustomerTier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty; // Silver, Gold, Platinum
    public int Level { get; set; } // 1, 2, 3
    public decimal MinimumSpend { get; set; } // Rolling 12-month threshold
    public decimal MinimumPoints { get; set; } // Points threshold
    public decimal PointsEarnMultiplier { get; set; } = 1.0m; // Alias for EarnMultiplier
    
    public string TierUpgradeRule { get; set; } = "Spend"; // "Spend", "Points", "Both"
    public string TierDowngradeRule { get; set; } = "Inactivity";
    public string BenefitsJson { get; set; } = "{}";
}

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Phone { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? TamilName { get; set; }
    
    public string? Email { get; set; }
    public string? Address { get; set; }
    
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
    public decimal CreditLimit { get; set; } = 0;
    
    // Loyalty and Segmentation
    public decimal LifetimePointsEarned { get; set; }
    public decimal LifetimeSpend { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    
    public string? PreferredCategory { get; set; }
    public decimal AverageBasketValue { get; set; }
    public int VisitFrequency { get; set; } // e.g., visits per month
    public string CustomerSegment { get; set; } = "New Customer"; // New, Regular, VIP, Dormant
    
    // Stage 2 Additions
    public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;
    public string MembershipStatus { get; set; } = "Active"; // Active, Suspended, Blocked, Inactive
    public DateTime? LastRedemptionDate { get; set; }
    public DateTime? LastPointsEarnedDate { get; set; }
    
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
    public Guid Id { get; set; } = Guid.NewGuid(); // Acts as TransactionId
    public Guid CustomerId { get; set; }
    public Guid? StoreId { get; set; }
    
    // TransactionType: Earn Points, Redeem Points, Manual Adjustment, Birthday Bonus, Anniversary Bonus, Tier Upgrade Bonus, Promotional Bonus, Expiration
    public string TransactionType { get; set; } = string.Empty; 
    
    public decimal PreviousBalance { get; set; } // Stage 2 requirement
    public decimal PointsEarned { get; set; }
    public decimal PointsRedeemed { get; set; }
    public decimal BalanceAfterTransaction { get; set; }
    
    public Guid? InvoiceId { get; set; }
    public string ReferenceDocument { get; set; } = string.Empty; // Invoice No
    public string Remarks { get; set; } = string.Empty;
    
    public DateTime? ExpiryDate { get; set; } // Null if burnt immediately, else usually +365 days
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Acts as CreatedDate
    public Guid? CreatedBy { get; set; }
}

public class LoyaltyProgramConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // Global active config should be singleton logically
    public bool IsActiveConfig { get; set; } = true;
    
    public decimal EarnRatioSpendAmount { get; set; } = 100; // e.g., spend 100
    public decimal EarnRatioPoints { get; set; } = 1; // get 1 point
    
    public decimal RedeemRatioPoints { get; set; } = 100; // e.g., 100 points
    public decimal RedeemRatioDiscountAmount { get; set; } = 10; // equals 10 Rs
    
    public decimal MaxRedemptionPercentagePerInvoice { get; set; } = 20; // 20% limit
    
    // Stage 2 Additions
    public decimal MaxRedemptionPerDay { get; set; } = 1000;
    public decimal MaxManualAdjustmentPerDay { get; set; } = 500;
    public decimal MaxBonusAllocationPerCustomer { get; set; } = 2000;
    
    public bool EnableAutoTierEvaluation { get; set; } = true;
    public bool EnablePointExpiry { get; set; } = true;
    public int ExpiryMonths { get; set; } = 12;
    public decimal BirthdayBonusPoints { get; set; } = 50;
    public decimal AnniversaryBonusPoints { get; set; } = 100;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
}
