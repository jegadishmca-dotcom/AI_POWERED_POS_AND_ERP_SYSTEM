using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Common.Interfaces;
using PosErp.Application.Features.Reports.Queries.GetGSTReport;
using PosErp.Application.Features.Reports.Queries.GetMarginReport;
using PosErp.Application.Features.Reports.Queries.GetInventoryInsights;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationDbContext _context;

    public ReportsController(IMediator mediator, IApplicationDbContext context)
    {
        _mediator = mediator;
        _context = context;
    }

    [HttpGet("gst")]
    public async Task<IActionResult> GetGSTReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var result = await _mediator.Send(new GetGSTReportQuery(fromDate, toDate));
        return Ok(result);
    }

    [HttpGet("margin")]
    public async Task<IActionResult> GetMarginReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var result = await _mediator.Send(new GetMarginReportQuery(fromDate, toDate));
        return Ok(result);
    }

    [HttpGet("inventory-insights")]
    public async Task<IActionResult> GetInventoryInsights()
    {
        var result = await _mediator.Send(new GetInventoryInsightsQuery());
        return Ok(result);
    }

    [HttpGet("invoice-sales")]
    public async Task<IActionResult> GetInvoiceSalesReport([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
    {
        var start = (fromDate ?? DateTime.Today).Date;
        var end = (toDate ?? DateTime.Today).Date.AddDays(1).AddTicks(-1);

        var invoices = await (from inv in _context.Invoices.Include(i => i.Items)
                              join cashier in _context.Users on inv.CashierId equals cashier.Id into cashiers
                              from c in cashiers.DefaultIfEmpty()
                              join cust in _context.Customers on inv.CustomerId equals cust.Id into customers
                              from cu in customers.DefaultIfEmpty()
                              where (inv.BusinessDate >= start && inv.BusinessDate <= end) || (inv.CreatedAt >= start && inv.CreatedAt <= end)
                              orderby inv.CreatedAt descending
                              select new
                              {
                                  inv.Id,
                                  inv.InvoiceNumber,
                                  BusinessDate = inv.BusinessDate ?? inv.CreatedAt,
                                  CreatedAt = inv.CreatedAt,
                                  CashierName = c != null ? c.FullName : "Cashier",
                                  CustomerName = cu != null ? cu.Name : "WALK-IN",
                                  CustomerPhone = cu != null ? cu.Phone : "",
                                  ItemCount = inv.Items.Count,
                                  TotalQty = inv.Items.Sum(i => i.Quantity),
                                  inv.SubTotal,
                                  inv.DiscountAmount,
                                  inv.TaxAmount,
                                  inv.TotalAmount,
                                  inv.RoundOff,
                                  inv.NetPayable,
                                  inv.PaymentMode,
                                  inv.CashAmount,
                                  inv.UpiAmount,
                                  inv.CardAmount,
                                  inv.WalletAmount,
                                  inv.Status
                              }).ToListAsync();

        return Ok(invoices);
    }
}
