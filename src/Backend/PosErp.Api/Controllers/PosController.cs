using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Pos.Commands.SyncInvoices;
using PosErp.Infrastructure.Printing;
using System;
using PosErp.Application.Features.Pos.Queries.GetZReport;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPrintService _printService;
    private readonly IApplicationDbContext _context;

    public PosController(IMediator mediator, IPrintService printService, IApplicationDbContext context)
    {
        _mediator = mediator;
        _printService = printService;
        _context = context;
    }

    [HttpGet("invoice/{id}")]
    public async Task<IActionResult> GetInvoice(Guid id)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice == null)
        {
            return NotFound("Invoice not found.");
        }

        var cashier = await _context.Users.FindAsync(invoice.CashierId);
        string cashierName = cashier?.FullName ?? "Cashier";

        string customerName = "";
        string customerPhone = "";
        if (invoice.CustomerId.HasValue)
        {
            var customer = await _context.Customers.FindAsync(invoice.CustomerId.Value);
            if (customer != null)
            {
                customerName = customer.Name;
                customerPhone = customer.Phone;
            }
        }

        var terminal = await _context.Terminals.FindAsync(invoice.TerminalId);
        string terminalCode = terminal?.TerminalCode ?? "POS-01";

        return Ok(new
        {
            invoice.Id,
            invoice.StoreId,
            invoice.BusinessDate,
            invoice.InvoiceNumber,
            invoice.TerminalId,
            TerminalCode = terminalCode,
            invoice.CashierId,
            CashierName = cashierName,
            invoice.CustomerId,
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            invoice.SubTotal,
            invoice.DiscountAmount,
            invoice.TaxAmount,
            invoice.TotalAmount,
            invoice.RoundOff,
            invoice.NetPayable,
            invoice.Status,
            invoice.PaymentMode,
            invoice.CashAmount,
            invoice.UpiAmount,
            invoice.CardAmount,
            invoice.WalletAmount,
            invoice.CreatedAt,
            invoice.Irn,
            invoice.AckNo,
            invoice.AckDate,
            invoice.QrCode,
            Items = invoice.Items.Select(item => new
            {
                item.Id,
                item.ProductId,
                item.Barcode,
                item.ProductName,
                item.Quantity,
                item.UnitPrice,
                item.DiscountAmount,
                item.CgstRate,
                item.CgstAmount,
                item.SgstRate,
                item.SgstAmount,
                item.CessRate,
                item.CessAmount,
                item.TotalAmount
            })
        });
    }

    [HttpGet("z-report")]
    public async Task<IActionResult> GetZReport([FromQuery] Guid terminalId, [FromQuery] DateTime businessDate, [FromQuery] Guid? cashierId = null)
    {
        return Ok(await _mediator.Send(new GetZReportQuery(terminalId, businessDate, cashierId)));
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] SyncInvoicesCommand command)
    {
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceCommand command)
    {
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("session/open")]
    public async Task<IActionResult> OpenSession([FromBody] PosErp.Application.Features.Pos.Commands.CreatePosSessionCommand command)
    {
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("calculate-cart")]
    public async Task<IActionResult> CalculateCart([FromBody] PosErp.Application.Features.Pos.Queries.CalculateCart.CalculateCartQuery query)
    {
        return Ok(await _mediator.Send(query));
    }

    [HttpGet("session/current")]
    public async Task<IActionResult> GetCurrentSession([FromQuery] Guid terminalId, [FromQuery] Guid cashierId)
    {
        var session = await _mediator.Send(new PosErp.Application.Features.Pos.Queries.GetActivePosSessionQuery(terminalId, cashierId));
        return Ok(session);
    }

    [HttpPost("session/close")]
    public async Task<IActionResult> CloseSession([FromBody] PosErp.Application.Features.Pos.Commands.ClosePosSessionCommand command)
    {
        return Ok(await _mediator.Send(command));
    }

    [HttpPost("print/{invoiceId}")]
    public async Task<IActionResult> PrintReceipt(Guid invoiceId, [FromQuery] string printerIp = "192.168.1.100")
    {
        var invoice = await _context.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice == null)
        {
            return NotFound("Invoice not found.");
        }

        var cashier = await _context.Users.FindAsync(invoice.CashierId);
        string cashierName = cashier?.FullName ?? "Cashier";

        string customerName = "";
        string customerPhone = "";
        if (invoice.CustomerId.HasValue)
        {
            var customer = await _context.Customers.FindAsync(invoice.CustomerId.Value);
            if (customer != null)
            {
                customerName = customer.Name;
                customerPhone = customer.Phone;
            }
        }

        var terminal = await _context.Terminals.FindAsync(invoice.TerminalId);
        string terminalCode = terminal?.TerminalCode ?? "POS-01";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("         ஆப்பிள் சூப்பர் மார்க்கெட்");
        sb.AppendLine("            Apple Super Market");
        sb.AppendLine("       1E-16, Matha Kovil Street,");
        sb.AppendLine("          Ilayankudi - 630702");
        sb.AppendLine("      Ph: 7339056767 / 04564-221190");
        sb.AppendLine("          GSTIN: 33ABTFA7190F1Z7");
        sb.AppendLine("          FSSAI: 12421019000047");
        sb.AppendLine("               TAX INVOICE");
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Bill No: {invoice.InvoiceNumber}");
        sb.AppendLine($"Date: {invoice.BusinessDate:dd/MM/yyyy}  Time: {invoice.CreatedAt:HH:mm}");
        sb.AppendLine($"Cashier: {cashierName,-15} Term: {terminalCode}");
        if (!string.IsNullOrEmpty(customerName))
        {
            sb.AppendLine($"Customer: {customerName} | {customerPhone}");
        }
        sb.AppendLine("----------------------------------------");
        sb.AppendLine("Item                     Qty  Rate   Amt");
        sb.AppendLine("----------------------------------------");

        foreach (var item in invoice.Items)
        {
            string name = item.ProductName ?? "Item";
            if (name.Length > 20) name = name.Substring(0, 19) + ".";

            string qtyStr = item.Quantity.ToString("0.##");
            string rateStr = item.UnitPrice.ToString("0.00");
            string amtStr = (item.Quantity * item.UnitPrice - item.DiscountAmount).ToString("0.00");

            sb.AppendLine($"{name,-20} {qtyStr,3} {rateStr,6} {amtStr,7}");
            if (item.DiscountAmount > 0)
            {
                sb.AppendLine($"  Discount: -{item.DiscountAmount:0.00}");
            }
        }
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"Sub Total:                  {invoice.SubTotal,12:0.00}");
        if (invoice.DiscountAmount > 0)
        {
            sb.AppendLine($"Discount:                  -{invoice.DiscountAmount,12:0.00}");
        }
        if (invoice.TaxAmount > 0)
        {
            sb.AppendLine($"GST (CGST+SGST):           +{invoice.TaxAmount,12:0.00}");
        }
        if (invoice.RoundOff != 0)
        {
            string sign = invoice.RoundOff > 0 ? "+" : "";
            sb.AppendLine($"Round Off:                 {sign}{invoice.RoundOff,12:0.00}");
        }
        sb.AppendLine("----------------------------------------");
        sb.AppendLine($"NET PAYABLE:               INR {invoice.NetPayable,8:0.00}");
        sb.AppendLine("----------------------------------------");

        if (invoice.CashAmount > 0)  sb.AppendLine($"Cash Tendered:              {invoice.CashAmount,12:0.00}");
        if (invoice.UpiAmount > 0)   sb.AppendLine($"UPI Paid:                   {invoice.UpiAmount,12:0.00}");
        if (invoice.CardAmount > 0)  sb.AppendLine($"Card Paid:                  {invoice.CardAmount,12:0.00}");
        if (invoice.WalletAmount > 0) sb.AppendLine($"Wallet Paid:                {invoice.WalletAmount,12:0.00}");

        decimal tendered = invoice.CashAmount + invoice.UpiAmount + invoice.CardAmount + invoice.WalletAmount;
        decimal change = Math.Max(0, tendered - invoice.NetPayable);
        if (change > 0)
        {
            sb.AppendLine($"Change Due:                 {change,12:0.00}");
        }

        sb.AppendLine("----------------------------------------");
        sb.AppendLine("GST Summary:");
        sb.AppendLine("Slab      Taxable       CGST        SGST");

        var gstGroups = new System.Collections.Generic.Dictionary<decimal, (decimal taxable, decimal cgst, decimal sgst)>();
        foreach (var item in invoice.Items)
        {
            decimal rate = item.CgstRate + item.SgstRate;
            if (rate > 0)
            {
                if (!gstGroups.ContainsKey(rate))
                    gstGroups[rate] = (0, 0, 0);

                decimal lineAmt = item.Quantity * item.UnitPrice - item.DiscountAmount;
                var current = gstGroups[rate];
                gstGroups[rate] = (
                    current.taxable + lineAmt,
                    current.cgst + item.CgstAmount,
                    current.sgst + item.SgstAmount
                );
            }
        }

        if (gstGroups.Count > 0)
        {
            foreach (var kvp in gstGroups)
            {
                sb.AppendLine($"GST {kvp.Key,2:0}% {kvp.Value.taxable,10:0.00} {kvp.Value.cgst,10:0.00} {kvp.Value.sgst,10:0.00}");
            }
        }
        else
        {
            sb.AppendLine("All items: Nil Rated / Exempt");
        }

        sb.AppendLine("----------------------------------------");
        sb.AppendLine("    அனைத்தும் வாங்க ஆப்பிளுக்கு வாங்க");
        sb.AppendLine("   Thank you for shopping with us!");
        sb.AppendLine("             Visit Again!");
        sb.AppendLine("\n\n\n");

        await _printService.PrintReceiptAsync(printerIp, 9100, sb.ToString());
        return Ok();
    }
}
