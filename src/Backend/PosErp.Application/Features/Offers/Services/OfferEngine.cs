using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
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
    private readonly IDistributedCache _cache;
    private const string CacheKey = "ActiveOffers";

    public OfferEngine(IApplicationDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<Offer>> GetActiveOffersAsync(CancellationToken cancellationToken)
    {
        var cachedOffers = await _cache.GetStringAsync(CacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedOffers))
        {
            return JsonSerializer.Deserialize<List<Offer>>(cachedOffers) ?? new List<Offer>();
        }

        var now = DateTime.UtcNow;
        var offers = await _context.Offers
            .Where(o => o.IsActive && o.StartDate <= now && o.EndDate >= now)
            .ToListAsync(cancellationToken);

        var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) };
        await _cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(offers), cacheOptions, cancellationToken);

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
                 if (config.Reward.DiscountType == "Percentage") billDiscount = cart.Subtotal * (config.Reward.Value / 100m);
                 else if (config.Reward.DiscountType == "FlatAmount") billDiscount = config.Reward.Value;

                 if (config.Reward.MaxDiscountAmount.HasValue && billDiscount > config.Reward.MaxDiscountAmount) billDiscount = config.Reward.MaxDiscountAmount.Value;

                 if (billDiscount > 0)
                 {
                     cart.TotalDiscount += billDiscount; // Apportion logic omitted for simplicity
                     offerApplied = true;
                 }
            }

            if (offerApplied)
            {
                cart.AppliedOfferNames.Add(offer.Name);
                if (offer.PromoCode == promoCode) cart.AppliedPromoCode = promoCode;
            }
        }

        cart.TotalDiscount += cart.Items.Sum(i => i.DiscountAmount);
        cart.FinalTotal = cart.Subtotal - cart.TotalDiscount + cart.TaxTotal;
        return cart;
    }
}
