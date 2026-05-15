using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Purchasing.Commands.ApprovePurchaseOrder;

public record ApprovePurchaseOrderCommand(Guid PurchaseOrderId, Guid? UserId) : IRequest<bool>;

public class ApprovePurchaseOrderCommandHandler : IRequestHandler<ApprovePurchaseOrderCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ApprovePurchaseOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ApprovePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId, cancellationToken);
        
        if (po == null) throw new Exception("Purchase Order not found.");
        if (po.Status != "DRAFT") throw new Exception("Only DRAFT Purchase Orders can be approved.");

        po.Status = "APPROVED";
        // Optionally capture ApproverId here
        
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
