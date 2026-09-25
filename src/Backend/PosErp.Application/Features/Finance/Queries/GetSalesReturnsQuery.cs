using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Queries;

public record GetSalesReturnsQuery(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int Limit = 50,
    Guid? StoreId = null
) : IRequest<List<SalesReturnSummaryDto>>;

public class SalesReturnSummaryDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public DateTime BusinessDate { get; set; }
    public Guid InvoiceId { get; set; }
    public string OriginalInvoiceNumber { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal TotalQty { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public string RefundMode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class GetSalesReturnsQueryHandler : IRequestHandler<GetSalesReturnsQuery, List<SalesReturnSummaryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSalesReturnsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SalesReturnSummaryDto>> Handle(GetSalesReturnsQuery request, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 100);

        var query = from ret in _context.SalesReturns.Include(r => r.Items)
                    join inv in _context.Invoices on ret.InvoiceId equals inv.Id into invoices
                    from i in invoices.DefaultIfEmpty()
                    join cashier in _context.Users on ret.CreatedBy equals cashier.Id into cashiers
                    from c in cashiers.DefaultIfEmpty()
                    join cust in _context.Customers on i.CustomerId equals cust.Id into customers
                    from cu in customers.DefaultIfEmpty()
                    select new SalesReturnSummaryDto
                    {
                        Id = ret.Id,
                        StoreId = ret.StoreId,
                        ReturnNumber = ret.ReturnNumber,
                        ReturnDate = ret.ReturnDate,
                        BusinessDate = ret.BusinessDate,
                        InvoiceId = ret.InvoiceId,
                        OriginalInvoiceNumber = i != null ? i.InvoiceNumber : "",
                        CashierName = c != null ? c.FullName : "Cashier",
                        CustomerName = cu != null ? cu.Name : "WALK-IN",
                        CustomerPhone = cu != null ? cu.Phone : "",
                        ItemCount = ret.Items.Count,
                        TotalQty = ret.Items.Sum(x => x.Quantity),
                        SubTotal = ret.SubTotal,
                        TaxAmount = ret.TaxAmount,
                        TotalAmount = ret.TotalAmount,
                        RefundAmount = ret.RefundAmount,
                        RefundMode = ret.RefundMode,
                        Status = ret.Status,
                        CreatedAt = ret.CreatedAt
                    };

        if (request.FromDate.HasValue)
        {
            var start = request.FromDate.Value.Date;
            query = query.Where(x => x.ReturnDate >= start);
        }

        if (request.ToDate.HasValue)
        {
            var end = request.ToDate.Value.Date;
            query = query.Where(x => x.ReturnDate <= end);
        }

        if (request.StoreId.HasValue)
        {
            query = query.Where(x => x.StoreId == request.StoreId.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
