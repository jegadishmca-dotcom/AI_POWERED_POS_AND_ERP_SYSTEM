$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Offers\Models"

# 1. Update Offer Entity
@"
using System;

namespace PosErp.Domain.Entities.Offers;

public class Offer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public string OfferType { get; set; } = string.Empty; // PERCENTAGE, FLAT, BOGO, COMBO
    public string RulesJson { get; set; } = "{}"; // Complex rules engine configuration
    
    public string? PromoCode { get; set; } // Null if automatically applied
    public int Priority { get; set; } = 0; // Higher runs first
    public bool IsStackable { get; set; } = false; // Can this combine with others?
    public bool IsExclusive { get; set; } = false; // If true, overrides all other offers
    public int? MaxUsagePerInvoice { get; set; }
    
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
"@ | Out-File -FilePath "$backendDir\PosErp.Domain\Entities\Offers\Offer.cs" -Encoding utf8

# 2. Schema Migration for Offer Enhancements
@"
-- ==============================================================================
-- PHASE 3: OFFER ENHANCEMENTS
-- ==============================================================================
ALTER TABLE offers 
ADD COLUMN promo_code VARCHAR(50) UNIQUE,
ADD COLUMN is_exclusive BOOLEAN DEFAULT FALSE,
ADD COLUMN max_usage_per_invoice INT;

CREATE INDEX idx_offers_promocode ON offers(promo_code);
"@ | Out-File -FilePath "$backendDir\PosErp.Infrastructure\Persistence\Migrations\08_OfferEnhancementsSchema.sql" -Encoding utf8

# 3. Models for Offer Engine
@"
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
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Offers\Models\OfferRuleConfig.cs" -Encoding utf8

# 4. Implement Full Offer Engine Logic
@"
using Microsoft.EntityFrameworkCore;
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
}

public class OfferEngine : IOfferEngine
{
    private readonly IApplicationDbContext _context;

    public OfferEngine(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CartEvaluationDto> EvaluateOffersAsync(CartEvaluationDto cart, string? customerTier, string? promoCode, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        
        // 1. Fetch Active Offers (In production, from Redis)
        var activeOffersQuery = _context.Offers
            .Where(o => o.IsActive && o.StartDate <= now && o.EndDate >= now);
            
        // Filter out promo code specific offers unless the code matches
        var offers = await activeOffersQuery.ToListAsync(cancellationToken);
        
        var applicableOffers = offers.Where(o => 
            string.IsNullOrEmpty(o.PromoCode) || 
            (promoCode != null && o.PromoCode.Equals(promoCode, StringComparison.OrdinalIgnoreCase))
        ).OrderByDescending(o => o.Priority).ToList();

        // 2. Initialize Cart Totals
        cart.Subtotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);
        foreach(var item in cart.Items)
        {
            item.LineTotal = item.Quantity * item.UnitPrice;
            item.DiscountAmount = 0;
            item.FinalLineTotal = item.LineTotal;
            item.AppliedOfferName = null;
        }
        
        cart.TotalDiscount = 0;
        cart.AppliedOfferNames.Clear();

        bool exclusiveOfferApplied = false;

        // 3. Evaluate each offer
        foreach (var offer in applicableOffers)
        {
            if (exclusiveOfferApplied) break;

            var config = JsonSerializer.Deserialize<OfferRuleConfig>(offer.RulesJson) ?? new OfferRuleConfig();
            
            // Check Bill-Level Conditions
            if (config.Conditions.MinCartValue.HasValue && cart.Subtotal < config.Conditions.MinCartValue) continue;
            if (!string.IsNullOrEmpty(config.Conditions.RequiredCustomerTier) && config.Conditions.RequiredCustomerTier != customerTier) continue;

            bool offerApplied = false;

            if (config.Reward.ApplyTo == "LINE")
            {
                foreach (var item in cart.Items)
                {
                    // Skip if item already has a non-stackable discount
                    if (item.DiscountAmount > 0 && !offer.IsStackable) continue;

                    // Check Line-Level Conditions
                    if (config.Conditions.RequiredProductIds != null && config.Conditions.RequiredProductIds.Any() && !config.Conditions.RequiredProductIds.Contains(item.ProductId)) continue;
                    if (config.Conditions.RequiredCategoryIds != null && config.Conditions.RequiredCategoryIds.Any() && (!item.CategoryId.HasValue || !config.Conditions.RequiredCategoryIds.Contains(item.CategoryId.Value))) continue;
                    if (config.Conditions.MinQuantity.HasValue && item.Quantity < config.Conditions.MinQuantity) continue;

                    // Calculate Discount
                    decimal itemDiscount = 0;
                    if (config.Reward.DiscountType == "Percentage")
                    {
                        itemDiscount = item.LineTotal * (config.Reward.Value / 100m);
                    }
                    else if (config.Reward.DiscountType == "FlatAmount")
                    {
                        itemDiscount = config.Reward.Value * item.Quantity; // Or flat off line depending on exact rule
                    }

                    if (config.Reward.MaxDiscountAmount.HasValue && itemDiscount > config.Reward.MaxDiscountAmount)
                    {
                        itemDiscount = config.Reward.MaxDiscountAmount.Value;
                    }

                    // Apply Discount
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
                 // Bill Level Discount
                 decimal billDiscount = 0;
                 if (config.Reward.DiscountType == "Percentage")
                 {
                     billDiscount = cart.Subtotal * (config.Reward.Value / 100m);
                 }
                 else if (config.Reward.DiscountType == "FlatAmount")
                 {
                     billDiscount = config.Reward.Value;
                 }

                 if (config.Reward.MaxDiscountAmount.HasValue && billDiscount > config.Reward.MaxDiscountAmount)
                 {
                     billDiscount = config.Reward.MaxDiscountAmount.Value;
                 }

                 if (billDiscount > 0)
                 {
                     // In a real system, you might apportion this discount across lines for tax reasons.
                     // For simplicity, we add it to TotalDiscount.
                     cart.TotalDiscount += billDiscount;
                     offerApplied = true;
                 }
            }

            if (offerApplied)
            {
                cart.AppliedOfferNames.Add(offer.Name);
                if (offer.PromoCode == promoCode) cart.AppliedPromoCode = promoCode;
                if (offer.IsExclusive) exclusiveOfferApplied = true;
                if (!offer.IsStackable) exclusiveOfferApplied = true; // Treats non-stackable as breaking the chain for simplicty here
            }
        }

        // Recalculate Line totals into cart total
        cart.TotalDiscount += cart.Items.Sum(i => i.DiscountAmount);
        cart.FinalTotal = cart.Subtotal - cart.TotalDiscount + cart.TaxTotal; // Assuming tax calculated elsewhere or proportionally
        
        return cart;
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Offers\Services\OfferEngine.cs" -Encoding utf8

# 5. Update PosTerminal.tsx with Cart Calculation UI
@"
import React, { useState, useEffect } from 'react';
import { Search, ShoppingCart, User, Plus, X, CreditCard, Wallet, Award, Tag } from 'lucide-react';
import { CustomerRegistrationModal } from '../../crm/components/CustomerRegistrationModal';

export const PosTerminal = () => {
  const [customer, setCustomer] = useState<any>(null);
  const [isCustomerModalOpen, setCustomerModalOpen] = useState(false);
  const [promoCode, setPromoCode] = useState('');
  
  // Mock Cart State mimicking CartEvaluationDto
  const [cart, setCart] = useState<any>({
    items: [
      { id: '1', productId: 'p1', name: 'Aashirvaad Atta 5kg', qty: 2, unitPrice: 200, lineTotal: 400, discountAmount: 0, finalLineTotal: 400, appliedOfferName: null },
      { id: '2', productId: 'p2', name: 'Tata Salt 1kg', qty: 1, unitPrice: 20, lineTotal: 20, discountAmount: 0, finalLineTotal: 20, appliedOfferName: null }
    ],
    subtotal: 420,
    totalDiscount: 0,
    taxTotal: 21,
    finalTotal: 441,
    appliedOfferNames: []
  });

  // Mock Offer Engine Evaluation
  const evaluateCart = () => {
    let newCart = { ...cart };
    let subtotal = newCart.items.reduce((sum: number, item: any) => sum + (item.qty * item.unitPrice), 0);
    let totalDiscount = 0;
    let appliedOffers: string[] = [];

    // Mock logic: 10% off Atta (Line Level)
    newCart.items = newCart.items.map((item: any) => {
      let discount = 0;
      let offerName = null;
      if (item.productId === 'p1') {
        discount = item.lineTotal * 0.10; // 10%
        offerName = '10% OFF Staples';
        if (!appliedOffers.includes(offerName)) appliedOffers.push(offerName);
      }
      return { ...item, discountAmount: discount, finalLineTotal: item.lineTotal - discount, appliedOfferName: offerName };
    });

    totalDiscount += newCart.items.reduce((sum: number, item: any) => sum + item.discountAmount, 0);

    // Mock logic: Flat 50 off if promo code applied (Bill Level)
    if (promoCode === 'SAVE50') {
      totalDiscount += 50;
      appliedOffers.push('SAVE50 Promo');
    }

    newCart.subtotal = subtotal;
    newCart.totalDiscount = totalDiscount;
    newCart.finalTotal = subtotal - totalDiscount + newCart.taxTotal;
    newCart.appliedOfferNames = appliedOffers;
    
    setCart(newCart);
  };

  // Evaluate whenever promo code changes
  useEffect(() => {
    evaluateCart();
  }, [promoCode]);


  const handleCustomerSearch = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      const val = e.currentTarget.value;
      if (val === '9988776655') {
        setCustomer({ id: '1', name: 'Rahul Sharma', phone: '9988776655', walletBalance: 500, points: 120, tier: 'Gold' });
      }
    }
  };

  return (
    <div className="flex h-screen bg-slate-100">
      {/* Left: Product/Cart Panel */}
      <div className="w-2/3 flex flex-col border-r border-slate-300 bg-white">
        
        {/* CRM Top Bar */}
        <div className="p-4 bg-indigo-50 border-b border-indigo-100 flex items-center justify-between">
          <div className="flex items-center w-1/2 relative">
            <User className="absolute left-3 text-indigo-400" />
            <input 
              type="text" 
              placeholder="F3: Search Customer (Phone/Name)..." 
              className="w-full pl-10 p-2 rounded-l border border-indigo-200 outline-none focus:ring-2 ring-indigo-500 font-bold"
              onKeyDown={handleCustomerSearch}
            />
            <button 
              onClick={() => setCustomerModalOpen(true)}
              className="bg-indigo-600 text-white p-2 rounded-r hover:bg-indigo-700 flex items-center"
            >
              <Plus className="w-5 h-5" />
            </button>
          </div>

          {customer && (
            <div className="flex items-center gap-4 bg-white p-2 rounded shadow-sm border border-indigo-200">
              <div>
                <p className="font-bold text-slate-800 text-sm">{customer.name} <span className="bg-yellow-100 text-yellow-800 text-xs px-1 rounded ml-1">{customer.tier}</span></p>
                <p className="text-xs text-gray-500">{customer.phone}</p>
              </div>
              <div className="text-right border-l pl-3">
                <p className="text-xs text-gray-600 flex items-center justify-end"><Wallet className="w-3 h-3 mr-1 text-blue-500"/> ₹{customer.walletBalance}</p>
                <p className="text-xs text-gray-600 flex items-center justify-end"><Award className="w-3 h-3 mr-1 text-orange-500"/> {customer.points} Pts</p>
              </div>
              <button onClick={() => setCustomer(null)} className="text-gray-400 hover:text-red-500 ml-2"><X className="w-5 h-5"/></button>
            </div>
          )}
        </div>

        {/* Cart Table with Offers */}
        <div className="p-0 flex-1 overflow-y-auto">
          <table className="w-full text-left">
            <thead className="bg-slate-100 sticky top-0 border-b">
              <tr>
                <th className="p-3">Item</th>
                <th className="p-3 text-center">Qty</th>
                <th className="p-3 text-right">Price</th>
                <th className="p-3 text-right">Total</th>
              </tr>
            </thead>
            <tbody>
              {cart.items.map((item: any) => (
                <tr key={item.id} className="border-b">
                  <td className="p-3">
                    <p className="font-bold text-lg">{item.name}</p>
                    {item.appliedOfferName && (
                      <p className="text-xs text-emerald-600 flex items-center font-bold bg-emerald-50 w-max px-2 py-0.5 rounded mt-1">
                        <Tag className="w-3 h-3 mr-1" /> {item.appliedOfferName}
                      </p>
                    )}
                  </td>
                  <td className="p-3 text-center font-bold text-xl">{item.qty}</td>
                  <td className="p-3 text-right">₹{item.unitPrice}</td>
                  <td className="p-3 text-right">
                    {item.discountAmount > 0 && <p className="text-sm text-gray-400 line-through">₹{item.lineTotal}</p>}
                    <p className="font-black text-xl text-slate-800">₹{item.finalLineTotal}</p>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Right: Payment Panel */}
      <div className="w-1/3 flex flex-col bg-slate-50 p-6">
        <h2 className="text-2xl font-black text-slate-800 mb-6 border-b pb-2">Payment</h2>
        
        <div className="flex-1">
          {/* Promo Code Input */}
          <div className="flex mb-6">
            <input 
              type="text" 
              placeholder="Promo Code" 
              className="w-full p-2 border border-r-0 rounded-l outline-none focus:border-indigo-500 font-bold uppercase"
              value={promoCode}
              onChange={(e) => setPromoCode(e.target.value.toUpperCase())}
            />
            <button className="bg-slate-800 text-white px-4 rounded-r font-bold hover:bg-slate-700">Apply</button>
          </div>

          <div className="flex justify-between text-lg mb-2"><span>Subtotal</span><span className="font-bold text-slate-700">₹{cart.subtotal.toFixed(2)}</span></div>
          <div className="flex justify-between text-lg mb-2 text-emerald-600">
            <span>Discounts</span>
            <span className="font-bold">-₹{cart.totalDiscount.toFixed(2)}</span>
          </div>
          {cart.appliedOfferNames.length > 0 && (
             <div className="text-xs text-emerald-600 mb-2 italic">Applied: {cart.appliedOfferNames.join(', ')}</div>
          )}
          
          <div className="flex justify-between text-lg mb-6"><span>Tax (GST)</span><span>₹{cart.taxTotal.toFixed(2)}</span></div>
          
          <div className="flex justify-between text-4xl font-black text-indigo-700 mb-8 border-t pt-4">
            <span>Total</span><span>₹{cart.finalTotal.toFixed(2)}</span>
          </div>

          {/* Payment Methods */}
          <div className="grid grid-cols-2 gap-4">
            <button className="bg-emerald-600 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-emerald-700">CASH</button>
            <button className="bg-blue-600 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-blue-700">UPI / QR</button>
            <button className="bg-slate-800 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-slate-900">CARD</button>
            
            <button 
              className={`p-4 rounded-lg font-bold text-xl shadow flex flex-col items-center justify-center \${customer && customer.walletBalance > 0 ? 'bg-indigo-600 text-white hover:bg-indigo-700' : 'bg-gray-200 text-gray-400 cursor-not-allowed'}`}
              disabled={!customer || customer.walletBalance <= 0}
            >
              WALLET
              {customer && <span className="text-sm">Bal: ₹{customer.walletBalance}</span>}
            </button>
          </div>
        </div>
      </div>

      <CustomerRegistrationModal 
        isOpen={isCustomerModalOpen} 
        onClose={() => setCustomerModalOpen(false)} 
        onRegister={() => {}} 
      />
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\pos\components\PosTerminal.tsx" -Encoding utf8

Write-Host "Offer Engine Scaffolded"
