using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands;

public record ForceClosePosSessionCommand(Guid SessionId) : IRequest<bool>;

public class ForceClosePosSessionCommandHandler : IRequestHandler<ForceClosePosSessionCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ForceClosePosSessionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ForceClosePosSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.PosSessions.FindAsync(new object[] { request.SessionId }, cancellationToken);
        if (session == null || session.Status == "CLOSED") return false;

        var endTime = DateTime.UtcNow;

        // Calculate expected cash from invoices during this session
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

        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
