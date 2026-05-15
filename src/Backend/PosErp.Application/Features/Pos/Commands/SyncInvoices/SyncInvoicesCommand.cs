using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands.SyncInvoices;

public record SyncInvoicesCommand(List<OfflineInvoiceDto> Invoices) : IRequest<SyncResult>;
public record SyncResult(int Synced, int Failed, List<string> Errors);

public record OfflineInvoiceDto(
    Guid Id,
    DateTime BusinessDate,
    string InvoiceNumber,
    Guid TerminalId,
    int TerminalSequence,
    Guid CashierId,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal RoundOff,
    decimal NetPayable,
    string PaymentMode,
    List<OfflineInvoiceItemDto> Items
);

public record OfflineInvoiceItemDto(
    Guid Id,
    Guid ProductId,
    string? Barcode,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal CgstRate,
    decimal CgstAmount,
    decimal SgstRate,
    decimal SgstAmount,
    decimal CessRate,
    decimal CessAmount,
    decimal TotalAmount
);

public class SyncInvoicesCommandHandler : IRequestHandler<SyncInvoicesCommand, SyncResult>
{
    private readonly IApplicationDbContext _context;

    private readonly PosErp.Application.Features.Offers.Services.IOfferEngine _offerEngine;
    public SyncInvoicesCommandHandler(IApplicationDbContext context, PosErp.Application.Features.Offers.Services.IOfferEngine offerEngine)
    {
        _context = context;
        _offerEngine = offerEngine;
    }

    public async Task<SyncResult> Handle(SyncInvoicesCommand request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        int synced = 0;
        int failed = 0;

        foreach (var dto in request.Invoices)
        {
            try
            {
                // Composite unique constraint check: TerminalId, TerminalSequence, BusinessDate.Date
                var exists = await _context.Invoices.AnyAsync(i => 
                    (i.Id == dto.Id) || 
                    (i.TerminalId == dto.TerminalId && i.TerminalSequence == dto.TerminalSequence && i.BusinessDate.Date == dto.BusinessDate.Date),
                    cancellationToken);
                
                if (exists)
                {
                    synced++;
                    continue; 
                }

                // ... Invoice mapping same as before ...
                var invoice = new Invoice {
                    Id = dto.Id, BusinessDate = dto.BusinessDate, InvoiceNumber = dto.InvoiceNumber,
                    TerminalId = dto.TerminalId, TerminalSequence = dto.TerminalSequence, CashierId = dto.CashierId,
                    SubTotal = dto.SubTotal, DiscountAmount = dto.DiscountAmount, TaxAmount = dto.TaxAmount,
                    TotalAmount = dto.TotalAmount, RoundOff = dto.RoundOff, NetPayable = dto.NetPayable,
                    PaymentMode = dto.PaymentMode, Status = "COMPLETED"
                };

                foreach (var itemDto in dto.Items)
                {
                    invoice.Items.Add(new InvoiceItem {
                        Id = itemDto.Id, BusinessDate = dto.BusinessDate, ProductId = itemDto.ProductId, Barcode = itemDto.Barcode,
                        ProductName = itemDto.ProductName, Quantity = itemDto.Quantity, UnitPrice = itemDto.UnitPrice,
                        DiscountAmount = itemDto.DiscountAmount, CgstRate = itemDto.CgstRate, CgstAmount = itemDto.CgstAmount,
                        SgstRate = itemDto.SgstRate, SgstAmount = itemDto.SgstAmount, CessRate = itemDto.CessRate,
                        CessAmount = itemDto.CessAmount, TotalAmount = itemDto.TotalAmount
                    });
                }
                _context.Invoices.Add(invoice);
                synced++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"Failed to sync {dto.InvoiceNumber}: {ex.Message}");
            }
        }

        if (synced > 0) await _context.SaveChangesAsync(cancellationToken);
        return new SyncResult(synced, failed, errors);
    }
}

