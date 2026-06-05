using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Queries;

public record GetActiveBusinessDateQuery(Guid? StoreId = null) : IRequest<StoreBusinessDate?>;

public class GetActiveBusinessDateQueryHandler : IRequestHandler<GetActiveBusinessDateQuery, StoreBusinessDate?>
{
    private readonly IApplicationDbContext _context;

    public GetActiveBusinessDateQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StoreBusinessDate?> Handle(GetActiveBusinessDateQuery request, CancellationToken cancellationToken)
    {
        var targetStoreId = request.StoreId ?? Guid.Empty;
        return await _context.StoreBusinessDates
            .Where(d => d.StoreId == targetStoreId && d.Status == "OPEN")
            .OrderByDescending(d => d.BusinessDate)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
