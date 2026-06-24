using System;
using System.ComponentModel.DataAnnotations.Schema;
using PosErp.Domain.Entities.Auth;
using PosErp.Domain.Entities.Purchasing;
using PosErp.Domain.Entities.Catalog;
using PosErp.Domain.Entities.Inventory;

namespace PosErp.Domain.Entities.Finance;

public class AiKpiResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string KpiType { get; set; } = string.Empty; // FINANCIAL, INVENTORY, STORE
    public string KpiName { get; set; } = string.Empty;
    public decimal KpiValue { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("StoreId")]
    public Store? Store { get; set; }
}

public class AiKpiHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string KpiType { get; set; } = string.Empty; // FINANCIAL, INVENTORY, STORE
    public string KpiName { get; set; } = string.Empty;
    public decimal KpiValue { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("StoreId")]
    public Store? Store { get; set; }
}

public class AiCashFlowForecast
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public DateTime ForecastDate { get; set; }
    public decimal ProjectedInflow { get; set; }
    public decimal ProjectedOutflow { get; set; }
    public decimal ProjectedBalance { get; set; }
    public string ConfidenceLevel { get; set; } = "HIGH"; // HIGH, MEDIUM, LOW
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("StoreId")]
    public Store? Store { get; set; }
}

public class AiSupplierPaymentRecommendation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid PurchaseBillId { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public decimal DiscountAvailable { get; set; }
    public DateTime? DiscountExpiryDate { get; set; }
    public int PriorityScore { get; set; }
    public string RecommendationReason { get; set; } = string.Empty;
    
    // Feedback columns
    public string FeedbackStatus { get; set; } = "PENDING"; // PENDING, ACCEPTED, REJECTED
    public string? FeedbackNotes { get; set; }
    public DateTime? ActionedAt { get; set; }
    public Guid? ActionedBy { get; set; }
    
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("SupplierId")]
    public Supplier? Supplier { get; set; }

    [ForeignKey("PurchaseBillId")]
    public PurchaseBillHeader? PurchaseBill { get; set; }

    [ForeignKey("ActionedBy")]
    public User? ActionedByUser { get; set; }
}

public class AiFinancialAnomaly
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AnomalyType { get; set; } = string.Empty; // DUPLICATE_PAYMENT, UNUSUAL_JOURNAL, CASHIER_SHORTAGE
    public string Severity { get; set; } = string.Empty; // CRITICAL, WARNING, INFO
    public string Description { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public Guid? ReferenceId { get; set; } // journal_entry_id or cashier session id
    
    public bool IsResolved { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }

    [ForeignKey("ResolvedBy")]
    public User? ResolvedByUser { get; set; }
}

public class AiInventoryShrinkageAnalytic
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal ShrinkageQuantity { get; set; }
    public decimal ShrinkageCost { get; set; }
    public decimal ShrinkageRatePct { get; set; }
    public string RiskLevel { get; set; } = "LOW"; // HIGH, MEDIUM, LOW
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("StoreId")]
    public Store? Store { get; set; }

    [ForeignKey("ProductId")]
    public Product? Product { get; set; }
}

public class AiExpiryRiskPrediction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime ExpiryDate { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal CostPrice { get; set; }
    public decimal PotentialLoss { get; set; }
    public decimal AverageDailySalesQty { get; set; }
    public decimal ProjectedSoldQty { get; set; }
    public decimal ExpiryRiskPct { get; set; }
    public string RiskCategory { get; set; } = "LOW"; // CRITICAL, HIGH, MEDIUM, LOW
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("StoreId")]
    public Store? Store { get; set; }

    [ForeignKey("ProductId")]
    public Product? Product { get; set; }

    [ForeignKey("BatchId")]
    public ProductBatch? Batch { get; set; }
}

public class AiAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? StoreId { get; set; }
    public string AlertType { get; set; } = string.Empty; // SHRINKAGE, EXPIRY, ANOMALY, BUDGET_OVERRUN
    public string AlertSeverity { get; set; } = string.Empty; // CRITICAL, HIGH, MEDIUM, LOW
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }

    [ForeignKey("StoreId")]
    public Store? Store { get; set; }

    [ForeignKey("ResolvedBy")]
    public User? ResolvedByUser { get; set; }
}

public class ExecutiveKpiSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime SnapshotDate { get; set; }
    public decimal DailySales { get; set; }
    public decimal DailyProfit { get; set; }
    public decimal GrossMarginPct { get; set; }
    public decimal TotalInventoryValue { get; set; }
    public decimal DeadStockValue { get; set; }
    public int ActiveLoyaltyMembers { get; set; }
    public int ActiveCustomers { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ForecastAccuracySnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime SnapshotDate { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    public decimal MeanAbsolutePercentageError { get; set; } // MAPE
    public decimal MeanAbsoluteError { get; set; } // MAE
    public decimal RootMeanSquareError { get; set; } // RMSE
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AiBusinessInsight
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string InsightCategory { get; set; } = string.Empty; // Risk, Opportunity, Observation
    public string BusinessArea { get; set; } = string.Empty; // Sales, Inventory, Procurement, Loyalty, Finance
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public int ImpactScore { get; set; } // 0-100
    public int ConfidenceScore { get; set; } // 0-100
    public decimal EstimatedFinancialImpact { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
    
    // Explainability
    public string GenerationReasoning { get; set; } = string.Empty;
    
    // References
    public string? ReferenceType { get; set; } // Product, Supplier, Customer
    public Guid? ReferenceId { get; set; }
    
    // Lifecycle Management
    public string Status { get; set; } = "NEW"; // NEW, ACKNOWLEDGED, IN_PROGRESS, RESOLVED, IGNORED
    public Guid? AssignedTo { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? ResolutionNotes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AiDemandForecast
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ForecastType { get; set; } = "PRODUCT"; // PRODUCT, CATEGORY, STORE, SUPPLIER
    public Guid? ReferenceId { get; set; }
    public DateTime ForecastDate { get; set; }
    
    public int ForecastHorizonDays { get; set; }
    public string ForecastMethod { get; set; } = string.Empty;
    
    public decimal ForecastQuantity { get; set; }
    public decimal? ActualQuantity { get; set; }
    public decimal? ForecastError { get; set; }
    
    public decimal ConfidenceLevel { get; set; }
    public string ModelVersion { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AiCustomerIntelligence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string SegmentType { get; set; } = string.Empty; // VIP, High Value, At Risk, Dormant
    public decimal ChurnRiskPct { get; set; }
    public decimal LtvPrediction { get; set; }
    public string LifetimeValueCategory { get; set; } = string.Empty;
    
    public DateTime? PredictedNextPurchaseDate { get; set; }
    public string ChurnCategory { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    
    public DateTime LastCalculatedAt { get; set; } = DateTime.UtcNow;
}

public class AiStorePerformance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public decimal MetricValue { get; set; }
    public decimal BenchmarkValue { get; set; }
    public decimal Variance { get; set; }
    
    public int Rank { get; set; }
    public string BenchmarkGroup { get; set; } = string.Empty; // Store, Region, Category
    public decimal Percentile { get; set; }
    
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}
