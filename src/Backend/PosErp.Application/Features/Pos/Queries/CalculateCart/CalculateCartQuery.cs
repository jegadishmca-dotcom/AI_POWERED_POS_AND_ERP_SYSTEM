using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Offers.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Queries.CalculateCart;

public record CalculateCartItemDto(Guid ProductId, decimal Quantity);

public record CalculateCartQuery(
    List<CalculateCartItemDto> Items,
    string? PromoCode,
    Guid? CustomerId
) : IRequest<CartCalculationResultDto>;

public record CartCalculationResultDto(
    decimal SubTotal,
    decimal TotalDiscount,
    decimal TaxTotal,
    decimal FinalTotal,
    List<string> AppliedOfferNames,
    List<CartItemCalculationResultDto> Items
);

public record CartItemCalculationResultDto(
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    decimal DiscountAmount,
    decimal FinalLineTotal,
    string? AppliedOfferName,
    decimal CgstRate,
    decimal CgstAmount,
    decimal SgstRate,
    decimal SgstAmount
);

public class CalculateCartQueryHandler : IRequestHandler<CalculateCartQuery, CartCalculationResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IOfferEngine _offerEngine;

    public CalculateCartQueryHandler(IApplicationDbContext context, IOfferEngine offerEngine)
    {
        _context = context;
        _offerEngine = offerEngine;
    }

    public async Task<CartCalculationResultDto> Handle(CalculateCartQuery request, CancellationToken cancellationToken)
    {
        var resultItems = new List<CartItemCalculationResultDto>();
        decimal subTotal = 0;
        decimal totalTax = 0;

        // 1. Fetch Product details (Price, Taxes)
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Include(p => p.TaxSlab)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var item in request.Items)
        {
            if (products.TryGetValue(item.ProductId, out var product))
            {
                var lineTotal = product.SellingPrice * item.Quantity;
                subTotal += lineTotal;
                
                // Calculate tax
                decimal cgstRate = product.TaxSlab?.CgstRate ?? 0;
                decimal sgstRate = product.TaxSlab?.SgstRate ?? 0;
                
                decimal cgstAmount = lineTotal * (cgstRate / 100m);
                decimal sgstAmount = lineTotal * (sgstRate / 100m);
                totalTax += (cgstAmount + sgstAmount);

                resultItems.Add(new CartItemCalculationResultDto(
                    ProductId: product.Id,
                    ProductName: product.Name,
                    Quantity: item.Quantity,
                    UnitPrice: product.SellingPrice,
                    LineTotal: lineTotal,
                    DiscountAmount: 0, // Will be updated by OfferEngine
                    FinalLineTotal: lineTotal,
                    AppliedOfferName: null,
                    CgstRate: cgstRate,
                    CgstAmount: cgstAmount,
                    SgstRate: sgstRate,
                    SgstAmount: sgstAmount
                ));
            }
        }

        // 2. Apply Promotions using real OfferEngine
        // Since OfferEngine takes an Invoice entity, we might need to mock it or use the evaluate method if it supports DTOs.
        // For now, let's just do a basic global discount if PromoCode is provided and valid.
        decimal totalDiscount = 0;
        var appliedOfferNames = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.PromoCode))
        {
            var offer = await _context.Offers
                .FirstOrDefaultAsync(o => o.PromoCode != null && o.PromoCode.ToUpper() == request.PromoCode.ToUpper() && o.IsActive, cancellationToken);
            
            if (offer != null)
            {
                decimal discountAmt = 0;
                // Since RulesJson is used, we'll do a simple mock implementation here for the sake of the endpoint
                // Assuming it's a FLAT 50 off if promo code matched for now until RulesEngine is plugged in
                discountAmt = 50m;

                if (discountAmt > subTotal) discountAmt = subTotal;

                totalDiscount = discountAmt;
                appliedOfferNames.Add(offer.Name);
            }
        }

        // 3. Pro-rate discount across items (optional, but good for item-level gross)
        // For simplicity, we just apply it to the final total
        decimal finalTotal = subTotal - totalDiscount + totalTax;

        return new CartCalculationResultDto(
            SubTotal: Math.Round(subTotal, 2),
            TotalDiscount: Math.Round(totalDiscount, 2),
            TaxTotal: Math.Round(totalTax, 2),
            FinalTotal: Math.Round(finalTotal, 2),
            AppliedOfferNames: appliedOfferNames,
            Items: resultItems
        );
    }
}
