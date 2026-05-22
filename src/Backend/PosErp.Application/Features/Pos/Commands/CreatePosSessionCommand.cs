using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using System;
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
