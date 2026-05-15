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

public record ProductSearchResultDto(Guid Id, string ProductCode, string Name, string? TamilName, decimal SellingPrice, string PrimaryBarcode);

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
            .Where(p => !p.IsDeleted && p.IsActive);

        if (!string.IsNullOrEmpty(q))
        {
            // Mandatory Full-Text Search using Postgres MATCHES operator (@@)
            // It relies on the pre-computed 'search_vector' column.
            
            // Note: In EF Core for PostgreSQL (Npgsql), ToTsQuery formats words with '&' for exact match logic
            string formattedQuery = string.Join(" & ", q.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w + ":*"));
            
            productsQuery = productsQuery.Where(p => 
                EF.Functions.ToTsVector("english", p.SearchVector!).Matches(EF.Functions.ToTsQuery("english", formattedQuery))
                || EF.Functions.ILike(p.ProductCode, $"%{q}%") // Direct code fallback
                || p.Barcodes.Any(b => b.BarcodeValue == q)); // Exact barcode match is faster
        }

        var results = await productsQuery
            .Take(request.Limit)
            .Select(p => new ProductSearchResultDto(
                p.Id, 
                p.ProductCode, 
                p.Name, 
                p.TamilName, 
                p.SellingPrice, 
                p.Barcodes.FirstOrDefault(b => b.IsPrimary).BarcodeValue ?? string.Empty
            ))
            .ToListAsync(cancellationToken);

        return results;
    }
}
