using MediatR;
using PosErp.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace PosErp.Application.Features.Purchasing.Commands.DeleteSupplier;

public class DeleteSupplierCommand : IRequest<DeleteSupplierResult>
{
    public Guid Id { get; set; }
}

public class DeleteSupplierResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool NotFound { get; set; }
}

public class DeleteSupplierCommandHandler : IRequestHandler<DeleteSupplierCommand, DeleteSupplierResult>
{
    private readonly IApplicationDbContext _context;

    public DeleteSupplierCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DeleteSupplierResult> Handle(DeleteSupplierCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        
        if (supplier == null)
        {
            return new DeleteSupplierResult { Success = false, NotFound = true, Message = "Supplier not found." };
        }

        // Check if there are associated Purchase Orders
        var hasPurchaseOrders = await _context.PurchaseOrders.AnyAsync(x => x.SupplierId == request.Id, cancellationToken);
        // Check if there are associated GRNs
        var hasGrns = await _context.GRNHeaders.AnyAsync(x => x.SupplierId == request.Id, cancellationToken);

        if (hasPurchaseOrders || hasGrns)
        {
            return new DeleteSupplierResult 
            { 
                Success = false, 
                Message = "Cannot delete supplier because they have associated Purchase Orders or Goods Receipt Notes. You can deactivate them instead." 
            };
        }

        _context.Suppliers.Remove(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        return new DeleteSupplierResult { Success = true, Message = "Supplier deleted successfully." };
    }
}
