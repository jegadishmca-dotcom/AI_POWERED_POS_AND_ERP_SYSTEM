using System;

namespace PosErp.Domain.Entities.Finance;

public class CostCenter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Budget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public Guid CostCenterId { get; set; }
    public Guid GlAccountId { get; set; }
    
    public string FinancialYear { get; set; } = string.Empty; // e.g. 2026-27
    public string Period { get; set; } = string.Empty; // MONTHLY, QUARTERLY, ANNUAL
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    
    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CostCenter CostCenter { get; set; } = null!;
    public Account GlAccount { get; set; } = null!;
}
