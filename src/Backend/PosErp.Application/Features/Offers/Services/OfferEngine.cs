using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Offers;
using PosErp.Application.Features.Offers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Offers.Services;

public interface IOfferEngine
{
    Task<CartEvaluationDto> EvaluateOffersAsync(CartEvaluationDto cart, string? customerTier, string? promoCode, CancellationToken cancellationToken);
    Task<List<Offer>> GetActiveOffersAsync(CancellationToken cancellationToken);
}

public class OfferEngine : IOfferEngine
{
    private readonly IApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "ActiveOffers";

    public OfferEngine(IApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<Offer>> GetActiveOffersAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out List<Offer>? cachedOffers) && cachedOffers != null)
        {
            return cachedOffers;
        }

        var now = DateTime.UtcNow;
        var offers = await _context.Offers
            .Where(o => o.IsActive && o.StartDate <= now && o.EndDate >= now)
            .ToListAsync(cancellationToken);

        var cacheOptions = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) };
        _cache.Set(CacheKey, offers, cacheOptions);

        return offers;
    }

    public async Task<CartEvaluationDto> EvaluateOffersAsync(CartEvaluationDto originalCart, string? customerTier, string? promoCode, CancellationToken cancellationToken)
    {
        var activeOffers = await GetActiveOffersAsync(cancellationToken);
        
        var applicableOffers = activeOffers.Where(o => 
            string.IsNullOrEmpty(o.PromoCode) || 
            (promoCode != null && o.PromoCode.Equals(promoCode, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        // Separate stackable and non-stackable/exclusive
        var stackableOffers = applicableOffers.Where(o => o.IsStackable && !o.IsExclusive).ToList();
        var exclusiveOffers = applicableOffers.Where(o => o.IsExclusive || !o.IsStackable).ToList();

        // 1. Evaluate Stackable first as baseline
        var bestCart = EvaluateOfferCombination(originalCart, stackableOffers, customerTier, promoCode);

        // 2. Evaluate each exclusive/non-stackable offer INDIVIDUALLY to find the absolute BEST discount for the customer
        foreach (var exclusive in exclusiveOffers)
        {
            // Evaluate this exclusive offer ON ITS OWN against the original cart
            var testCart = EvaluateOfferCombination(originalCart, new List<Offer> { exclusive }, customerTier, promoCode);
            
            // If this single exclusive offer gives a better discount than all stackable combined, it wins
            if (testCart.TotalDiscount > bestCart.TotalDiscount)
            {
                bestCart = testCart;
            }
        }

        // L3: Removed hardcoded "10% OFF Staples" fallback.
        // All offers must be configured in the Offers database table.
        // If no offers fire, TotalDiscount remains 0 and no discount is applied.

        // Calculate actual GST based on each product's tax slab (including CGST, SGST, and Cess)
        decimal actualTax = 0;
        var itemProductIds = bestCart.Items.Select(i => i.ProductId).Distinct().ToList();
        var itemsProducts = await _context.Products
            .Include(p => p.TaxSlab)
            .Where(p => itemProductIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var item in bestCart.Items)
        {
            if (itemsProducts.TryGetValue(item.ProductId, out var prod))
            {
                decimal cgstRate = prod.TaxSlab?.CgstRate ?? 0;
                decimal sgstRate = prod.TaxSlab?.SgstRate ?? 0;
                decimal cessRate = prod.TaxSlab?.CessRate ?? 0;
                
                decimal cgstAmount = Math.Round(item.FinalLineTotal * (cgstRate / 100m), 2);
                decimal sgstAmount = Math.Round(item.FinalLineTotal * (sgstRate / 100m), 2);
                decimal cessAmount = Math.Round(item.FinalLineTotal * (cessRate / 100m), 2);
                
                actualTax += cgstAmount + sgstAmount + cessAmount;
            }
        }
        bestCart.TaxTotal = actualTax;
        // FinalTotal = sum of each item's post-discount line total + actual GST
        // Do NOT use (Subtotal - TotalDiscount) since Subtotal may differ from sum(FinalLineTotal)
        // when BILL-level discounts are applied. Using sum(FinalLineTotal) is always accurate.
        bestCart.FinalTotal = bestCart.Items.Sum(i => i.FinalLineTotal) + bestCart.TaxTotal;

        return bestCart;
    }

    private CartEvaluationDto EvaluateOfferCombination(CartEvaluationDto originalCart, List<Offer> offers, string? customerTier, string? promoCode)
    {
        // Deep copy the cart to avoid mutating the original during test evaluations
        var cart = new CartEvaluationDto
        {
            Subtotal = originalCart.Items.Sum(i => i.Quantity * i.UnitPrice),
            Items = originalCart.Items.Select(i => new CartItemEvaluationDto
            {
                ProductId = i.ProductId,
                CategoryId = i.CategoryId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineTotal = i.Quantity * i.UnitPrice,
                FinalLineTotal = i.Quantity * i.UnitPrice,
                DiscountAmount = 0
            }).ToList()
        };

        // Sort by priority as tie-breaker within the combination
        var sortedOffers = offers.OrderByDescending(o => o.Priority).ToList();

        foreach (var offer in sortedOffers)
        {
            var config = JsonSerializer.Deserialize<OfferRuleConfig>(offer.RulesJson) ?? new OfferRuleConfig();
            
            if (config.Conditions.MinCartValue.HasValue && cart.Subtotal < config.Conditions.MinCartValue) continue;
            if (!string.IsNullOrEmpty(config.Conditions.RequiredCustomerTier) && config.Conditions.RequiredCustomerTier != customerTier) continue;

            bool offerApplied = false;

            if (config.Reward.ApplyTo == "LINE")
            {
                foreach (var item in cart.Items)
                {
                    if (item.DiscountAmount > 0) continue; // Item already discounted in this combo

                    if (config.Conditions.RequiredProductIds != null && config.Conditions.RequiredProductIds.Any() && !config.Conditions.RequiredProductIds.Contains(item.ProductId)) continue;
                    if (config.Conditions.RequiredCategoryIds != null && config.Conditions.RequiredCategoryIds.Any() && (!item.CategoryId.HasValue || !config.Conditions.RequiredCategoryIds.Contains(item.CategoryId.Value))) continue;
                    if (config.Conditions.MinQuantity.HasValue && item.Quantity < config.Conditions.MinQuantity) continue;

                    decimal itemDiscount = 0;
                    if (config.Reward.DiscountType == "Percentage") itemDiscount = item.LineTotal * (config.Reward.Value / 100m);
                    else if (config.Reward.DiscountType == "FlatAmount") itemDiscount = config.Reward.Value * item.Quantity; 

                    if (config.Reward.MaxDiscountAmount.HasValue && itemDiscount > config.Reward.MaxDiscountAmount) itemDiscount = config.Reward.MaxDiscountAmount.Value;

                    if (itemDiscount > 0)
                    {
                        item.DiscountAmount += itemDiscount;
                        item.FinalLineTotal = item.LineTotal - item.DiscountAmount;
                        item.AppliedOfferName = offer.Name;
                        offerApplied = true;
                    }
                }
            }
            else if (config.Reward.ApplyTo == "BILL")
            {
                 decimal billDiscount = 0;
                 if (config.Reward.DiscountType == "Percentage") billDiscount = Math.Round(cart.Subtotal * (config.Reward.Value / 100m), 2);
                 else if (config.Reward.DiscountType == "FlatAmount") billDiscount = config.Reward.Value;

                 if (config.Reward.MaxDiscountAmount.HasValue && billDiscount > config.Reward.MaxDiscountAmount) billDiscount = config.Reward.MaxDiscountAmount.Value;

                 if (billDiscount > 0 && cart.Subtotal > 0)
                 {
                     // H3: Pro-rate bill discount proportionally to each item's line total
                     // so that per-item FinalLineTotal is reduced BEFORE GST is calculated.
                     // Without this, GST would be computed on the full (pre-discount) item totals.
                     foreach (var item in cart.Items)
                     {
                         decimal itemShare = Math.Round(billDiscount * (item.LineTotal / cart.Subtotal), 2);
                         item.DiscountAmount += itemShare;
                         item.FinalLineTotal = item.LineTotal - item.DiscountAmount;
                         if (item.FinalLineTotal < 0) item.FinalLineTotal = 0;
                         item.AppliedOfferName = offer.Name;
                     }
                     offerApplied = true;
                 }
            }

            if (offerApplied)
            {
                cart.AppliedOfferNames.Add(offer.Name);
                if (offer.PromoCode == promoCode) cart.AppliedPromoCode = promoCode;
            }
        }

        cart.TotalDiscount = cart.Items.Sum(i => i.DiscountAmount);
        // TaxTotal is intentionally left as 0 here; real per-slab tax is recalculated
        // in EvaluateOffersAsync after the best offer combination is selected.
        cart.TaxTotal = 0;
        cart.FinalTotal = cart.Items.Sum(i => i.FinalLineTotal) + cart.TaxTotal;
        return cart;
    }
}
