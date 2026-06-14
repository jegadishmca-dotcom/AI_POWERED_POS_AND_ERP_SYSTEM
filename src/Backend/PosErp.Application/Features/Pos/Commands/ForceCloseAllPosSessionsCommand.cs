using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands;

public record ForceCloseAllPosSessionsCommand() : IRequest<bool>;

public class ForceCloseAllPosSessionsCommandHandler : IRequestHandler<ForceCloseAllPosSessionsCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ForceCloseAllPosSessionsCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ForceCloseAllPosSessionsCommand request, CancellationToken cancellationToken)
    {
        var openSessions = await _context.PosSessions
            .Where(s => s.Status == "OPEN")
            .ToListAsync(cancellationToken);

        if (openSessions.Count == 0) return true;

        var endTime = DateTime.UtcNow;

        foreach (var session in openSessions)
        {
            var invoices = await _context.Invoices
                .Where(i => i.TerminalId == session.TerminalId && 
                            i.CashierId == session.CashierId && 
                            i.CreatedAt >= session.StartTime && 
                            i.CreatedAt <= endTime)
                .ToListAsync(cancellationToken);

            decimal totalCashSales = invoices.Sum(i => i.CashAmount);

            session.ExpectedClosingCash = session.OpeningFloatCash + totalCashSales;
            session.ActualClosingCash = session.ExpectedClosingCash;
            session.Difference = 0;
            session.EndTime = endTime;
            session.Status = "CLOSED";
        }

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
