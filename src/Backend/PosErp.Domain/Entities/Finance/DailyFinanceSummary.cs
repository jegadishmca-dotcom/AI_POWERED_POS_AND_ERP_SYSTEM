using System;

namespace PosErp.Domain.Entities.Finance;

public class DailyFinanceSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public DateTime BusinessDate { get; set; }
    
    public decimal TotalSales { get; set; } = 0;
    public decimal TotalPurchases { get; set; } = 0;
    public decimal TotalPayments { get; set; } = 0;
    public decimal TotalReceipts { get; set; } = 0;
    public decimal TotalExpenses { get; set; } = 0;
    public decimal NetCashFlow { get; set; } = 0;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
