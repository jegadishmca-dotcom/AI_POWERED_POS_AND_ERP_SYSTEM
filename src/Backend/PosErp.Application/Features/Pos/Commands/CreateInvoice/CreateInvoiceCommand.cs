using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands.CreateInvoice;

public record CreateInvoiceCommand(OfflineInvoiceDto Invoice) : IRequest<Guid>;

public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateInvoiceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Invoice;

        var invoice = new Invoice
        {
            Id = dto.Id,
            BusinessDate = dto.BusinessDate,
            InvoiceNumber = dto.InvoiceNumber,
            TerminalId = dto.TerminalId,
            TerminalSequence = dto.TerminalSequence,
            CashierId = dto.CashierId,
            SubTotal = dto.SubTotal,
            DiscountAmount = dto.DiscountAmount,
            TaxAmount = dto.TaxAmount,
            TotalAmount = dto.TotalAmount,
            RoundOff = dto.RoundOff,
            NetPayable = dto.NetPayable,
            PaymentMode = dto.PaymentMode,
            Status = "COMPLETED"
        };

        foreach (var itemDto in dto.Items)
        {
            invoice.Items.Add(new InvoiceItem
            {
                Id = itemDto.Id,
                BusinessDate = dto.BusinessDate,
                ProductId = itemDto.ProductId,
                Barcode = itemDto.Barcode,
                ProductName = itemDto.ProductName,
                Quantity = itemDto.Quantity,
                UnitPrice = itemDto.UnitPrice,
                DiscountAmount = itemDto.DiscountAmount,
                CgstRate = itemDto.CgstRate,
                CgstAmount = itemDto.CgstAmount,
                SgstRate = itemDto.SgstRate,
                SgstAmount = itemDto.SgstAmount,
                CessRate = itemDto.CessRate,
                CessAmount = itemDto.CessAmount,
                TotalAmount = itemDto.TotalAmount
            });
        }

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}
