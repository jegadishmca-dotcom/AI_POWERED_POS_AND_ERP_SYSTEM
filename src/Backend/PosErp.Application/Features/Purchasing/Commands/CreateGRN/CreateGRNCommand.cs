using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Purchasing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Purchasing.Commands.CreateGRN;

public record CreateGRNCommand(
    Guid PurchaseOrderHeaderId,
    Guid SupplierId,
    string SupplierInvoiceNumber,
    DateTime ReceivedDate,
    List<CreateGRNItemDto> Items,
    Guid? UserId
) : IRequest<Guid>;

public record CreateGRNItemDto(
    Guid PurchaseOrderItemId,
    Guid ProductId,
    string BatchNumber,
    DateTime? MfgDate,
    DateTime? ExpiryDate,
    decimal ReceivedQuantity,
    decimal AcceptedQuantity,
    decimal RejectedQuantity,
    string? RejectionReason,
    decimal UnitCost
);

public class CreateGRNCommandHandler : IRequestHandler<CreateGRNCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateGRNCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateGRNCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SupplierInvoiceNumber))
            throw new Exception("Supplier Invoice Number is mandatory.");

        // Backend guard: reject empty GRN submissions
        if (request.Items == null || request.Items.Count == 0)
            throw new Exception("Cannot create a GRN with no items. Please enter at least one item with accepted quantity.");

        var hasAccepted = request.Items.Any(i => i.AcceptedQuantity > 0);
        if (!hasAccepted)
            throw new Exception("Cannot create a GRN with zero accepted quantities. Please enter accepted quantity for at least one item.");

        var grn = new GRNHeader
        {
            PurchaseOrderHeaderId = request.PurchaseOrderHeaderId,
            SupplierId = request.SupplierId,
            GrnNumber = $"GRN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
            SupplierInvoiceNumber = request.SupplierInvoiceNumber,
            ReceivedDate = request.ReceivedDate,
            Status = "DRAFT",
            CreatedBy = request.UserId
        };

        foreach (var itemDto in request.Items)
        {
            var itemTotal = itemDto.AcceptedQuantity * itemDto.UnitCost;
            grn.Items.Add(new GRNItem
            {
                PurchaseOrderItemId = itemDto.PurchaseOrderItemId,
                ProductId = itemDto.ProductId,
                BatchNumber = itemDto.BatchNumber ?? string.Empty,
                MfgDate = itemDto.MfgDate,
                ExpiryDate = itemDto.ExpiryDate,
                ReceivedQuantity = itemDto.ReceivedQuantity,
                AcceptedQuantity = itemDto.AcceptedQuantity,
                RejectedQuantity = itemDto.RejectedQuantity,
                RejectionReason = itemDto.RejectionReason,
                UnitCost = itemDto.UnitCost,
                TotalCost = itemTotal
            });
            grn.TotalAmount += itemTotal;
        }

        _context.GRNHeaders.Add(grn);
        await _context.SaveChangesAsync(cancellationToken);

        return grn.Id;
    }
}
