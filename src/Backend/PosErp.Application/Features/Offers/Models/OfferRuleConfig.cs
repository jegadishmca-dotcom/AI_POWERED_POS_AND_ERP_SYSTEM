using System;
using System.Collections.Generic;

namespace PosErp.Application.Features.Offers.Models;

public class OfferRuleConfig
{
    public OfferConditions Conditions { get; set; } = new();
    public OfferReward Reward { get; set; } = new();
}

public class OfferConditions
{
    public decimal? MinCartValue { get; set; }
    public List<Guid>? RequiredProductIds { get; set; }
    public List<Guid>? RequiredCategoryIds { get; set; }
    public string? RequiredCustomerTier { get; set; }
    public decimal? MinQuantity { get; set; } // For slab/BOGO
}

public class OfferReward
{
    public string DiscountType { get; set; } = "Percentage"; // Percentage, FlatAmount, FreeProduct
    public decimal Value { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public string ApplyTo { get; set; } = "LINE"; // LINE or BILL
    public Guid? FreeProductId { get; set; } // For FreeProduct type
}

public class CartEvaluationDto
{
    public List<CartItemEvaluationDto> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal FinalTotal { get; set; }
    public string? AppliedPromoCode { get; set; }
    public List<string> AppliedOfferNames { get; set; } = new();
}

public class CartItemEvaluationDto
{
    public Guid ProductId { get; set; }
    public Guid? CategoryId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalLineTotal { get; set; }
    public string? AppliedOfferName { get; set; }
}
