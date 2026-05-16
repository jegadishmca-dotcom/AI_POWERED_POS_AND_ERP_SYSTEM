using MediatR;
using System;

namespace PosErp.Application.Features.Pos.Commands.CreateInvoice
{
    public class CreateInvoiceCommand : IRequest<CreateInvoiceResponse>
    {
        public Guid TerminalId { get; set; }
        public string TerminalCode { get; set; } = string.Empty;
        public DateTime BusinessDate { get; set; } = DateTime.UtcNow.Date;
        
        // List of items in the cart
        public List<CreateInvoiceItemDto> Items { get; set; } = new();
        
        public Guid? CustomerId { get; set; }
        public decimal TenderedAmount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        
        // For offline mode (temporarily optional)
        // public OfflineInvoiceDto? OfflineInvoice { get; set; }
    }

    public class CreateInvoiceResponse
    {
        public Guid InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }
}