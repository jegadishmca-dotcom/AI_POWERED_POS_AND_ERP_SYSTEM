using System;

namespace PosErp.Application.Features.Pos.Commands.CreateInvoice
{
    public class CreateInvoiceItemDto
    {
        public Guid ProductId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal DiscountAmount { get; set; } = 0;
        public decimal TotalAmount { get; set; }
        
        public string? OfferCodeApplied { get; set; }
    }
}
