using System;

namespace PosErp.Domain.Entities.Offers;

public class OfferUsageLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // The Offer that was applied
    public Guid OfferId { get; set; }
    public string OfferName { get; set; } = string.Empty;
    public int OfferVersion { get; set; } = 1;
    
    // Context of application
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    
    public Guid? CustomerId { get; set; }
    public Guid TerminalId { get; set; }
    public string TerminalName { get; set; } = string.Empty;
    public Guid CashierId { get; set; }
    public Guid? StoreId { get; set; }
    
    // Financial Impact
    public decimal OriginalCartValue { get; set; }
    public decimal FinalCartValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal RevenueInfluenced { get; set; } // The final total of the bill that used this offer
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
