using System;

namespace PosErp.Domain.Entities.Catalog;

/// <summary>
/// GST HSN/SAC Master Reference Table — India 2024-26
/// Source: CBIC GST Rate Schedule (as per notifications up to Finance Act 2024)
/// Reference: https://cbic-gst.gov.in/gst-goods-services-rates.html
/// 
/// This table maps HSN codes to official GST slabs for retail/supermarket products.
/// Used for: product creation guidance, auto-suggest slab, compliance audit.
/// </summary>
public class GstHsnMasterIndia
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>HSN (Harmonized System of Nomenclature) Code — 4 or 8 digit</summary>
    public string HsnCode { get; set; } = string.Empty;

    /// <summary>Official description as per GST schedule</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Common supermarket category: GROCERY, DAIRY, PERSONAL_CARE, HOUSEHOLD, BEVERAGE, BAKERY, etc.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Example products for quick reference</summary>
    public string ExampleProducts { get; set; } = string.Empty;

    /// <summary>Total GST rate (%) = CGST + SGST or IGST</summary>
    public decimal GstRatePercent { get; set; }

    /// <summary>CGST rate = GstRatePercent / 2 (for intra-state supply)</summary>
    public decimal CgstRate { get; set; }

    /// <summary>SGST rate = GstRatePercent / 2 (for intra-state supply)</summary>
    public decimal SgstRate { get; set; }

    /// <summary>IGST rate = GstRatePercent (for inter-state supply)</summary>
    public decimal IgstRate { get; set; }

    /// <summary>Additional cess rate (%) — for tobacco, pan masala, aerated drinks</summary>
    public decimal CessRate { get; set; }

    /// <summary>Whether the item is mandatorily exempt</summary>
    public bool IsExempt { get; set; }

    /// <summary>Notes on conditions or exceptions</summary>
    public string? Notes { get; set; }

    /// <summary>GST Notification reference</summary>
    public string? NotificationRef { get; set; }

    /// <summary>Link to the TaxSlab record to be applied</summary>
    public Guid? TaxSlabId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
}
