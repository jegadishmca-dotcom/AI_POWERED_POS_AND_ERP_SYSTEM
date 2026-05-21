using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Catalog.Queries.SearchProducts;

public record SearchProductsQuery(string Query, int Limit = 20) : IRequest<List<ProductSearchResultDto>>;

public record ProductSearchResultDto(Guid Id, string ProductCode, string Name, string? TamilName, decimal SellingPrice, string PrimaryBarcode, decimal CgstRate, decimal SgstRate, bool IsWeighable);

public class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, List<ProductSearchResultDto>>
{
    private readonly IApplicationDbContext _context;

    public SearchProductsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductSearchResultDto>> Handle(SearchProductsQuery request, CancellationToken cancellationToken)
    {
        var q = request.Query.Trim();

        var productsQuery = _context.Products
            .Include(p => p.Barcodes)
            .Include(p => p.TaxSlab)
            .Where(p => !p.IsDeleted && p.IsActive);

        if (!string.IsNullOrEmpty(q))
        {
            productsQuery = productsQuery.Where(p => 
                EF.Functions.ILike(p.Name, $"%{q}%")
                || EF.Functions.ILike(p.ProductCode, $"%{q}%")
                || (p.TamilName != null && EF.Functions.ILike(p.TamilName, $"%{q}%"))
                || p.Barcodes.Any(b => b.BarcodeValue == q));
        }

        var results = await productsQuery
            .Take(request.Limit)
            .Select(p => new ProductSearchResultDto(
                p.Id, 
                p.ProductCode, 
                p.Name, 
                p.TamilName, 
                p.SellingPrice, 
                p.Barcodes.Where(b => b.IsPrimary).Select(b => b.BarcodeValue).FirstOrDefault() ?? string.Empty,
                p.TaxSlab != null ? p.TaxSlab.CgstRate : 0m,
                p.TaxSlab != null ? p.TaxSlab.SgstRate : 0m,
                p.IsWeighable
            ))
            .ToListAsync(cancellationToken);

        return results;
    }
}
