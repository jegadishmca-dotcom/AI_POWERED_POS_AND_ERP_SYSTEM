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
    Guid? CustomerId,
    bool SuppressOffers = false
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
    decimal SgstAmount,
    decimal CessRate,
    decimal CessAmount
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

        // 1. Fetch Product details (Price, Taxes)
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _context.Products
            .Include(p => p.TaxSlab)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        // Fetch Customer for tier info
        var customer = request.CustomerId.HasValue 
            ? await _context.Customers.Include(c => c.Tier).FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value, cancellationToken) 
            : null;

        var cartEvaluation = new PosErp.Application.Features.Offers.Models.CartEvaluationDto
        {
            Items = request.Items.Select(i => new PosErp.Application.Features.Offers.Models.CartItemEvaluationDto
            {
                ProductId = i.ProductId,
                CategoryId = products.TryGetValue(i.ProductId, out var pInfo) ? pInfo.CategoryId : null,
                Quantity = i.Quantity,
                UnitPrice = products.TryGetValue(i.ProductId, out var pInfo2) ? pInfo2.SellingPrice : 0
            }).ToList()
        };

        // 2. Evaluate promotions dynamically
        if (!request.SuppressOffers)
        {
            bool isBirthday = customer?.Dob.HasValue == true && customer.Dob.Value.Month == DateTime.Today.Month;
            bool isAnniversary = customer?.Anniversary.HasValue == true && customer.Anniversary.Value.Month == DateTime.Today.Month;

            cartEvaluation = await _offerEngine.EvaluateOffersAsync(cartEvaluation, customer?.Tier?.Name, request.PromoCode, isBirthday, isAnniversary, cancellationToken);
        }

        decimal preDiscountSubtotalExTax = 0;
        decimal totalDiscountExTax = 0;

        // 3. Map results and calculate taxes based on post-discount prices
        foreach (var item in cartEvaluation.Items)
        {
            if (products.TryGetValue(item.ProductId, out var product))
            {
                decimal cgstRate = product.TaxSlab?.CgstRate ?? 0;
                decimal sgstRate = product.TaxSlab?.SgstRate ?? 0;
                decimal cessRate = product.TaxSlab?.CessRate ?? 0;
                decimal totalTaxRate = cgstRate + sgstRate + cessRate;
                
                decimal taxableAmount = totalTaxRate > 0
                    ? item.FinalLineTotal / (1 + (totalTaxRate / 100m))
                    : item.FinalLineTotal;

                decimal cgstAmount = Math.Round(taxableAmount * (cgstRate / 100m), 2);
                decimal sgstAmount = Math.Round(taxableAmount * (sgstRate / 100m), 2);
                decimal cessAmount = Math.Round(taxableAmount * (cessRate / 100m), 2);

                decimal preDiscountLineExTax = totalTaxRate > 0
                    ? item.LineTotal / (1 + (totalTaxRate / 100m))
                    : item.LineTotal;

                decimal discountExTax = totalTaxRate > 0
                    ? item.DiscountAmount / (1 + (totalTaxRate / 100m))
                    : item.DiscountAmount;

                preDiscountSubtotalExTax += preDiscountLineExTax;
                totalDiscountExTax += discountExTax;

                resultItems.Add(new CartItemCalculationResultDto(
                    ProductId: product.Id,
                    ProductName: product.Name,
                    Quantity: item.Quantity,
                    UnitPrice: product.SellingPrice,
                    LineTotal: item.LineTotal,
                    DiscountAmount: item.DiscountAmount,
                    FinalLineTotal: item.FinalLineTotal,
                    AppliedOfferName: item.AppliedOfferName,
                    CgstRate: cgstRate,
                    CgstAmount: cgstAmount,
                    SgstRate: sgstRate,
                    SgstAmount: sgstAmount,
                    CessRate: cessRate,
                    CessAmount: cessAmount
                ));
            }
        }

        decimal taxTotal = resultItems.Sum(i => i.CgstAmount + i.SgstAmount + i.CessAmount);
        decimal finalTotal = cartEvaluation.Items.Sum(i => i.FinalLineTotal);

        return new CartCalculationResultDto(
            SubTotal: Math.Round(preDiscountSubtotalExTax, 2),
            TotalDiscount: Math.Round(totalDiscountExTax, 2),
            TaxTotal: Math.Round(taxTotal, 2),
            FinalTotal: Math.Round(finalTotal, 2),
            AppliedOfferNames: cartEvaluation.AppliedOfferNames,
            Items: resultItems
        );
    }
}
