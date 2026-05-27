using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Inventory.Commands.RejectStockAdjustment;

public record RejectStockAdjustmentCommand(Guid AdjustmentId, Guid? ApproverId) : IRequest<bool>;

public class RejectStockAdjustmentCommandHandler : IRequestHandler<RejectStockAdjustmentCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public RejectStockAdjustmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(RejectStockAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var adj = await _context.StockAdjustments
            .FirstOrDefaultAsync(a => a.Id == request.AdjustmentId, cancellationToken);
            
        if (adj == null || adj.Status != "PENDING") throw new Exception("Invalid or already processed Adjustment.");

        adj.Status = "REJECTED";
        adj.ApprovedBy = request.ApproverId; // Using ApprovedBy column to log who rejected it

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
