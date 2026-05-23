using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Queries.GetZReport;

public record GetZReportQuery(Guid TerminalId, DateTime BusinessDate, Guid? CashierId = null) : IRequest<ZReportDto>;

public record ZReportDto(
    Guid TerminalId, 
    DateTime BusinessDate,
    int TotalInvoices,
    decimal TotalSales,
    decimal TotalTax,
    decimal TotalDiscount,
    decimal CashCollected,
    decimal CardCollected,
    decimal UpiCollected
);

public class GetZReportQueryHandler : IRequestHandler<GetZReportQuery, ZReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetZReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ZReportDto> Handle(GetZReportQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Invoices
            .Where(i => i.TerminalId == request.TerminalId && i.BusinessDate.Date == request.BusinessDate.Date && i.Status == "COMPLETED");

        if (request.CashierId.HasValue)
        {
            query = query.Where(i => i.CashierId == request.CashierId.Value);
        }

        var invoices = await query.ToListAsync(cancellationToken);

        return new ZReportDto(
            request.TerminalId,
            request.BusinessDate,
            invoices.Count,
            invoices.Sum(i => i.TotalAmount),
            invoices.Sum(i => i.TaxAmount),
            invoices.Sum(i => i.DiscountAmount),
            invoices.Sum(i => i.CashAmount),
            invoices.Sum(i => i.CardAmount),
            invoices.Sum(i => i.UpiAmount)
        );
    }
}
