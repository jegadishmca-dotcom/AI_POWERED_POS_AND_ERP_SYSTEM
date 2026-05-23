using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using PosErp.Application.Features.Offers.Services;
using PosErp.Application.Features.Crm.Services;
using PosErp.Application.Features.Offers.Models;
using PosErp.Application.Features.Finance.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands.SyncInvoices;

public record CreateInvoiceCommand(
    string InvoiceNumber,
    Guid TerminalId,
    Guid CashierId,
    Guid? CustomerId,
    string? PromoCode,
    decimal WalletAmountUsed,
    decimal CashAmount,
    decimal UpiAmount,
    decimal CardAmount,
    List<InvoiceItemDto> Items
) : IRequest<Guid>;

public record InvoiceItemDto(Guid ProductId, decimal Quantity, decimal UnitPrice);

public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IOfferEngine _offerEngine;
    private readonly IWalletService _walletService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly IFinancialPostingService _financialPostingService;

    public CreateInvoiceCommandHandler(
        IApplicationDbContext context, 
        IOfferEngine offerEngine, 
        IWalletService walletService, 
        ILoyaltyService loyaltyService,
        IFinancialPostingService financialPostingService)
    {
        _context = context;
        _offerEngine = offerEngine;
        _walletService = walletService;
        _loyaltyService = loyaltyService;
        _financialPostingService = financialPostingService;
    }

    public async Task<Guid> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var customer = request.CustomerId.HasValue 
                ? await _context.Customers.Include(c => c.Tier).FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value) 
                : null;

            var productIds = request.Items.Select(i => i.ProductId).ToList();
            var productCategories = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.CategoryId })
                .ToDictionaryAsync(p => p.Id, p => p.CategoryId, cancellationToken);

            var cartEvaluation = new CartEvaluationDto
            {
                Items = request.Items.Select(i => new CartItemEvaluationDto
                {
                    ProductId = i.ProductId,
                    CategoryId = productCategories.TryGetValue(i.ProductId, out var catId) ? catId : null,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };

            cartEvaluation = await _offerEngine.EvaluateOffersAsync(cartEvaluation, customer?.Tier?.Name, request.PromoCode, cancellationToken);

            decimal totalTender = request.WalletAmountUsed + request.CashAmount + request.UpiAmount + request.CardAmount;
            if (totalTender < cartEvaluation.FinalTotal)
                throw new Exception("Total tender is less than the final invoice amount.");

            var today = DateTime.UtcNow.Date;
            var lastSeq = await _context.Invoices
                .Where(i => i.TerminalId == request.TerminalId && i.BusinessDate == today)
                .Select(i => (int?)i.TerminalSequence)
                .MaxAsync(cancellationToken) ?? 0;
            var nextSeq = lastSeq + 1;

            var invoice = new Invoice
            {
                InvoiceNumber = request.InvoiceNumber,
                TerminalId = request.TerminalId,
                CashierId = request.CashierId,
                TerminalSequence = nextSeq,
                CustomerId = customer?.Id,
                BusinessDate = today,
                SubTotal = cartEvaluation.Subtotal,
                TotalDiscount = cartEvaluation.TotalDiscount,
                TaxTotal = cartEvaluation.TaxTotal,
                TotalAmount = cartEvaluation.FinalTotal,
                Status = "COMPLETED"
            };

            foreach (var item in cartEvaluation.Items)
            {
                invoice.Items.Add(new InvoiceItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Total = item.LineTotal,
                    DiscountAmount = item.DiscountAmount,
                    FinalTotal = item.FinalLineTotal
                });
            }

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync(cancellationToken); 

            if (request.WalletAmountUsed > 0 && customer != null)
                await _walletService.RecordTransactionAsync(customer.Id, null, "SPEND", -request.WalletAmountUsed, $"INV-{invoice.InvoiceNumber}", null, cancellationToken);

            if (customer != null)
                await _loyaltyService.CalculateAndAwardPointsForInvoiceAsync(invoice.Id, customer.Id, invoice.TotalAmount, cancellationToken);

            // ==========================================
            // PHASE 4: FINANCIAL DOUBLE-ENTRY POSTING
            // ==========================================
            
            // For Indian GST, usually it's split 50/50 CGST/SGST for intra-state. 
            // In a real system, the cart Evaluation provides exact tax breakdown per item. We mock a 50/50 split here.
            decimal cgst = cartEvaluation.TaxTotal / 2m;
            decimal sgst = cartEvaluation.TaxTotal / 2m;
            decimal taxableValue = cartEvaluation.FinalTotal - cartEvaluation.TaxTotal;

            var journalLines = new List<JournalLineDto>();

            // Debits (What we received)
            decimal actualCashPaid = cartEvaluation.FinalTotal - request.UpiAmount - request.CardAmount - request.WalletAmountUsed;
            if (actualCashPaid > 0) journalLines.Add(new JournalLineDto { AccountCode = "1000", Description = "Cash Tender", Debit = actualCashPaid, Credit = 0 });
            if (request.UpiAmount > 0 || request.CardAmount > 0) journalLines.Add(new JournalLineDto { AccountCode = "1100", Description = "Digital Tender", Debit = request.UpiAmount + request.CardAmount, Credit = 0 });
            if (request.WalletAmountUsed > 0) journalLines.Add(new JournalLineDto { AccountCode = "2100", Description = "Wallet Redemption", Debit = request.WalletAmountUsed, Credit = 0 }); // Reducing liability
            
            // Credits (Revenue & Tax Liability)
            journalLines.Add(new JournalLineDto { AccountCode = "4000", Description = "Sales Revenue", Debit = 0, Credit = taxableValue });
            if (cgst > 0) journalLines.Add(new JournalLineDto { AccountCode = "2200", Description = "Output CGST", Debit = 0, Credit = cgst });
            if (sgst > 0) journalLines.Add(new JournalLineDto { AccountCode = "2201", Description = "Output SGST", Debit = 0, Credit = sgst });

            // Post Journal Entry
            await _financialPostingService.PostJournalEntryAsync(
                null, DateTime.UtcNow.Date, $"POS Invoice {invoice.InvoiceNumber}", $"INV-{invoice.Id}", journalLines, cancellationToken);

            // Post Dedicated Tax Transaction for GSTR Returns
            await _financialPostingService.RecordGstTransactionAsync(
                null, "SALE", invoice.InvoiceNumber, DateTime.UtcNow.Date, taxableValue, cgst, sgst, null, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return invoice.Id;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
