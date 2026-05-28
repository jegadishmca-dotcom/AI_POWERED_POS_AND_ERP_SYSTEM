using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Reports.Queries.GetGSTReport;

public record GetGSTReportQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<List<GstReportDto>>;

public class GstReportDto
{
    public decimal TaxRate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal CgstCollected { get; set; }
    public decimal SgstCollected { get; set; }
    public decimal CessCollected { get; set; }
    public decimal TotalTax { get; set; }
}

public class GetGSTReportQueryHandler : IRequestHandler<GetGSTReportQuery, List<GstReportDto>>
{
    private readonly IApplicationDbContext _context;

    public GetGSTReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<GstReportDto>> Handle(GetGSTReportQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.FromDate?.Date ?? DateTime.UtcNow.Date.AddDays(-30);
        var toDate = request.ToDate?.Date ?? DateTime.UtcNow.Date;

        var items = await _context.InvoiceItems
            .Where(ii => ii.Invoice.BusinessDate >= fromDate && ii.Invoice.BusinessDate <= toDate && ii.Invoice.Status == "COMPLETED")
            .Select(ii => new {
                TaxRate = ii.CgstRate + ii.SgstRate,
                Taxable = ii.TotalAmount - ii.CgstAmount - ii.SgstAmount - ii.CessAmount,
                Cgst = ii.CgstAmount,
                Sgst = ii.SgstAmount,
                Cess = ii.CessAmount
            })
            .ToListAsync(cancellationToken);

        var result = items
            .GroupBy(ii => ii.TaxRate)
            .Select(g => new GstReportDto
            {
                TaxRate = g.Key,
                TaxableAmount = g.Sum(x => x.Taxable),
                CgstCollected = g.Sum(x => x.Cgst),
                SgstCollected = g.Sum(x => x.Sgst),
                CessCollected = g.Sum(x => x.Cess),
                TotalTax = g.Sum(x => x.Cgst + x.Sgst + x.Cess)
            })
            .OrderBy(x => x.TaxRate)
            .ToList();

        return result;
    }
}
