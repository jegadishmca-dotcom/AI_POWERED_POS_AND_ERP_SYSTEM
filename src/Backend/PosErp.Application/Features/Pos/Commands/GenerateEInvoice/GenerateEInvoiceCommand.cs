using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Finance.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands.GenerateEInvoice;

public record GenerateEInvoiceCommand(Guid InvoiceId) : IRequest<bool>;

public class GenerateEInvoiceCommandHandler : IRequestHandler<GenerateEInvoiceCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IEInvoiceService _eInvoiceService;

    public GenerateEInvoiceCommandHandler(IApplicationDbContext context, IEInvoiceService eInvoiceService)
    {
        _context = context;
        _eInvoiceService = eInvoiceService;
    }

    public async Task<bool> Handle(GenerateEInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices.FindAsync(new object[] { request.InvoiceId }, cancellationToken);
        if (invoice == null) throw new Exception("Invoice not found");
        if (!string.IsNullOrEmpty(invoice.Irn)) return true; // Already generated

        var result = await _eInvoiceService.GenerateInvoiceIrnAsync(invoice.Id, cancellationToken);

        if (result.Success)
        {
            invoice.Irn = result.Irn;
            invoice.AckNo = result.AckNo;
            invoice.AckDate = result.AckDate;
            invoice.QrCodeUrl = result.SignedQrCodeUrl;
            invoice.Status = "E-INVOICED";

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        throw new Exception($"E-Invoice generation failed: {result.ErrorMessage}");
    }
}
