using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands;

public record CreatePosSessionCommand(Guid TerminalId, Guid CashierId, decimal OpeningFloatCash) : IRequest<Guid>;

public class CreatePosSessionCommandHandler : IRequestHandler<CreatePosSessionCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreatePosSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreatePosSessionCommand request, CancellationToken cancellationToken)
    {
        // Safety constraint check: Ensure no duplicate active open sessions exist for this terminal or cashier.
        var existingSession = await _context.PosSessions
            .Where(s => s.Status == "OPEN" && (s.TerminalId == request.TerminalId || s.CashierId == request.CashierId))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingSession != null)
        {
            // Return existing active session ID to make the operation idempotent and prevent duplicates.
            return existingSession.Id;
        }

        var session = new PosSession
        {
            Id = Guid.NewGuid(),
            TerminalId = request.TerminalId,
            CashierId = request.CashierId,
            StartTime = DateTime.UtcNow,
            OpeningFloatCash = request.OpeningFloatCash,
            Status = "OPEN"
        };

        _context.PosSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        return session.Id;
    }
}
