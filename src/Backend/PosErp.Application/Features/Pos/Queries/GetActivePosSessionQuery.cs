using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Queries;

public record GetActivePosSessionQuery(Guid TerminalId, Guid CashierId) : IRequest<PosSession?>;

public class GetActivePosSessionQueryHandler : IRequestHandler<GetActivePosSessionQuery, PosSession?>
{
    private readonly IApplicationDbContext _context;

    public GetActivePosSessionQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PosSession?> Handle(GetActivePosSessionQuery request, CancellationToken cancellationToken)
    {
        return await _context.PosSessions
            .Where(s => s.TerminalId == request.TerminalId && s.CashierId == request.CashierId && s.Status == "OPEN")
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
