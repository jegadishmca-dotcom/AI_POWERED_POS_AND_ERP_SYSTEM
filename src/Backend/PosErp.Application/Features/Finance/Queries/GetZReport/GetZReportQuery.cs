using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Queries.GetZReport;

public record GetZReportQuery(Guid TerminalId, DateTime BusinessDate) : IRequest<ZReportDto>;

public class ZReportDto
{
    public DateTime BusinessDate { get; set; }
    public decimal GrossSales { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal NetSales { get; set; } // Gross - Discounts
    public decimal TotalTax { get; set; }
    public decimal FinalCollections { get; set; }
    
    // Detailed GST Breakup for Day Closing
    public decimal TotalCgst { get; set; }
    public decimal TotalSgst { get; set; }
    
    // Tender Breakdown (Ideally this would be tracked explicitly in a tender table, simulating from journals here)
    public decimal CashCollected { get; set; }
    public decimal DigitalCollected { get; set; }
    public decimal WalletRedeemed { get; set; }
}

public class GetZReportQueryHandler : IRequestHandler<GetZReportQuery, ZReportDto>
{
    private readonly IApplicationDbContext _context;

    public GetZReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ZReportDto> Handle(GetZReportQuery request, CancellationToken cancellationToken)
    {
        // 1. Fetch Invoices for the specific day & terminal
        var invoices = await _context.Invoices
            .Where(i => i.TerminalId == request.TerminalId && i.BusinessDate == request.BusinessDate.Date && i.Status == "COMPLETED")
            .ToListAsync(cancellationToken);

        var report = new ZReportDto
        {
            BusinessDate = request.BusinessDate.Date,
            GrossSales = invoices.Sum(i => i.SubTotal),
            TotalDiscounts = invoices.Sum(i => i.TotalDiscount),
            TotalTax = invoices.Sum(i => i.TaxTotal),
            FinalCollections = invoices.Sum(i => i.TotalAmount),
            NetSales = invoices.Sum(i => i.SubTotal - i.TotalDiscount)
        };

        // 2. Fetch specific Tax Transactions for precise GST Breakup
        var invoiceNumbers = invoices.Select(i => i.InvoiceNumber).ToList();
        var taxRecords = await _context.TaxTransactions
            .Where(t => invoiceNumbers.Contains(t.DocumentNumber) && t.TransactionType == "SALE")
            .ToListAsync(cancellationToken);

        report.TotalCgst = taxRecords.Sum(t => t.CgstAmount);
        report.TotalSgst = taxRecords.Sum(t => t.SgstAmount);

        // 3. To find exact Cash vs Digital collections, we aggregate from the JournalEntryLines connected to these invoices
        // Account 1000 = Cash, 1100 = Digital, 2100 = Wallet
        var refDocs = invoices.Select(i => $"INV-{i.Id}").ToList();
        
        var journalLines = await _context.JournalEntryLines
            .Include(l => l.Account)
            .Where(l => l.JournalEntry.ReferenceDocument != null && refDocs.Contains(l.JournalEntry.ReferenceDocument))
            .ToListAsync(cancellationToken);

        report.CashCollected = journalLines.Where(l => l.Account.AccountCode == "1000").Sum(l => l.DebitAmount);
        report.DigitalCollected = journalLines.Where(l => l.Account.AccountCode == "1100").Sum(l => l.DebitAmount);
        report.WalletRedeemed = journalLines.Where(l => l.Account.AccountCode == "2100").Sum(l => l.DebitAmount);

        return report;
    }
}
