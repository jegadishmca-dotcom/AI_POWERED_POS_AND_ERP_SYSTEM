using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Commands.RejectStockTake;

public record RejectStockTakeCommand(Guid StockTakeId, Guid? ApproverId) : IRequest<bool>;

public class RejectStockTakeCommandHandler : IRequestHandler<RejectStockTakeCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public RejectStockTakeCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(RejectStockTakeCommand request, CancellationToken cancellationToken)
    {
        var take = await _context.StockTakeHeaders
            .FirstOrDefaultAsync(t => t.Id == request.StockTakeId, cancellationToken);

        if (take == null) throw new Exception("Stock Take not found.");
        if (take.Status != "REVIEW" && take.Status != "DRAFT") throw new Exception("Only DRAFT or REVIEW Stock Takes can be rejected.");

        take.Status = "REJECTED";
        take.ApprovedBy = request.ApproverId; // Log who rejected it

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
