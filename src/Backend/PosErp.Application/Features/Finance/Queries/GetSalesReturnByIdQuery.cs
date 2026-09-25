using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Finance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Queries;

public record GetSalesReturnByIdQuery(string Identifier, Guid? StoreId = null) : IRequest<SalesReturnDetailDto?>;

public class SalesReturnDetailDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid InvoiceId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public DateTime BusinessDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public string RefundMode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? JournalEntryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }

    // Original invoice info
    public string? OriginalInvoiceNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? TerminalCode { get; set; }
    public string? CashierName { get; set; }

    // Line items
    public List<SalesReturnItemDetailDto> Items { get; set; } = new();
}

public class SalesReturnItemDetailDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public Guid BatchId { get; set; }
    public string? BatchNumber { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class GetSalesReturnByIdQueryHandler : IRequestHandler<GetSalesReturnByIdQuery, SalesReturnDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetSalesReturnByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SalesReturnDetailDto?> Handle(GetSalesReturnByIdQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier))
            return null;

        var query = _context.SalesReturns
            .AsNoTracking()
            .Include(sr => sr.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Barcodes)
            .Include(sr => sr.Items)
                .ThenInclude(i => i.ProductBatch)
            .AsQueryable();

        if (request.StoreId.HasValue)
        {
            query = query.Where(sr => sr.StoreId == request.StoreId.Value);
        }

        SalesReturn? salesReturn = null;
        if (Guid.TryParse(request.Identifier, out Guid returnGuid))
        {
            salesReturn = await query.FirstOrDefaultAsync(sr => sr.Id == returnGuid, cancellationToken);
        }

        if (salesReturn == null)
        {
            var cleanNo = request.Identifier.Trim();
            // Handle if identifier has CAN- prefix from cancellation reference document
            if (cleanNo.StartsWith("CAN-", StringComparison.OrdinalIgnoreCase))
            {
                cleanNo = cleanNo.Substring(4);
            }

            salesReturn = await query.FirstOrDefaultAsync(
                sr => sr.ReturnNumber.ToLower() == cleanNo.ToLower() ||
                      sr.ReturnNumber.ToLower() == request.Identifier.Trim().ToLower(),
                cancellationToken);
        }

        if (salesReturn == null)
            return null;

        // Fetch original invoice details with Customer, Terminal, Cashier
        var invoiceData = await (from inv in _context.Invoices
                                 join cashier in _context.Users on inv.CashierId equals cashier.Id into cashiers
                                 from c in cashiers.DefaultIfEmpty()
                                 join terminal in _context.Terminals on inv.TerminalId equals terminal.Id into terminals
                                 from t in terminals.DefaultIfEmpty()
                                 join cust in _context.Customers on inv.CustomerId equals cust.Id into customers
                                 from cu in customers.DefaultIfEmpty()
                                 where inv.Id == salesReturn.InvoiceId
                                 select new {
                                     inv.InvoiceNumber,
                                     CashierName = c != null ? c.FullName : "Cashier",
                                     TerminalCode = t != null ? t.TerminalCode : "POS-01",
                                     CustomerName = cu != null ? cu.Name : "Walk-in Customer",
                                     CustomerPhone = cu != null ? cu.Phone : ""
                                 })
                                 .FirstOrDefaultAsync(cancellationToken);

        // Fetch creator user if any
        string? createdByName = null;
        if (salesReturn.CreatedBy.HasValue)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == salesReturn.CreatedBy.Value, cancellationToken);
            createdByName = user?.FullName;
        }

        var dto = new SalesReturnDetailDto
        {
            Id = salesReturn.Id,
            StoreId = salesReturn.StoreId,
            InvoiceId = salesReturn.InvoiceId,
            ReturnNumber = salesReturn.ReturnNumber,
            ReturnDate = salesReturn.ReturnDate,
            BusinessDate = salesReturn.BusinessDate,
            SubTotal = salesReturn.SubTotal,
            TaxAmount = salesReturn.TaxAmount,
            TotalAmount = salesReturn.TotalAmount,
            RefundAmount = salesReturn.RefundAmount,
            RefundMode = salesReturn.RefundMode,
            Status = salesReturn.Status,
            JournalEntryId = salesReturn.JournalEntryId,
            CreatedAt = salesReturn.CreatedAt,
            CreatedBy = salesReturn.CreatedBy,
            CreatedByName = createdByName,
            OriginalInvoiceNumber = invoiceData?.InvoiceNumber,
            CustomerName = invoiceData?.CustomerName,
            CustomerPhone = invoiceData?.CustomerPhone,
            TerminalCode = invoiceData?.TerminalCode,
            CashierName = invoiceData?.CashierName,
            Items = salesReturn.Items.Select(item => new SalesReturnItemDetailDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? "Unknown Product",
                ProductCode = item.Product?.ProductCode ?? string.Empty,
                Barcode = item.Product?.Barcodes?.FirstOrDefault(b => b.IsPrimary && !b.IsDeleted)?.BarcodeValue
                          ?? item.Product?.Barcodes?.FirstOrDefault(b => !b.IsDeleted)?.BarcodeValue,
                BatchId = item.BatchId,
                BatchNumber = item.ProductBatch?.BatchNumber,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TaxAmount = item.TaxAmount,
                TotalAmount = item.TotalAmount
            }).ToList()
        };

        return dto;
    }
}
