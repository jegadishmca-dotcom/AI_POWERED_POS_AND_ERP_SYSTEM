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
using PosErp.Application.Features.Inventory.Services;

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
    decimal RoundOff,
    decimal NetPayable,
    string PaymentMode,
    List<InvoiceItemDto> Items,
    string? SupervisorOverridePin = null
) : IRequest<Guid>;

public record InvoiceItemDto(Guid ProductId, decimal Quantity, decimal UnitPrice, Guid? BatchId);

public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IOfferEngine _offerEngine;
    private readonly IWalletService _walletService;
    private readonly ILoyaltyService _loyaltyService;
    private readonly IFinancialPostingService _financialPostingService;
    private readonly PosErp.Application.Features.Inventory.Services.IStockLedgerService _stockLedgerService;
    private readonly IPasswordHasher _passwordHasher;

    public CreateInvoiceCommandHandler(
        IApplicationDbContext context, 
        IOfferEngine offerEngine, 
        IWalletService walletService, 
        ILoyaltyService loyaltyService,
        IFinancialPostingService financialPostingService,
        PosErp.Application.Features.Inventory.Services.IStockLedgerService stockLedgerService,
        IPasswordHasher passwordHasher)
    {
        _context = context;
        _offerEngine = offerEngine;
        _walletService = walletService;
        _loyaltyService = loyaltyService;
        _financialPostingService = financialPostingService;
        _stockLedgerService = stockLedgerService;
        _passwordHasher = passwordHasher;
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
            var productsInfo = await _context.Products
                .Include(p => p.TaxSlab)
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var cartEvaluation = new CartEvaluationDto
            {
                Items = request.Items.Select(i => new CartItemEvaluationDto
                {
                    ProductId = i.ProductId,
                    CategoryId = productsInfo.TryGetValue(i.ProductId, out var pInfo) ? pInfo.CategoryId : null,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };

            cartEvaluation = await _offerEngine.EvaluateOffersAsync(cartEvaluation, customer?.Tier?.Name, request.PromoCode, cancellationToken);

            decimal totalTender = request.WalletAmountUsed + request.CashAmount + request.UpiAmount + request.CardAmount;
            if (totalTender < cartEvaluation.FinalTotal)
                throw new Exception("Total tender is less than the final invoice amount.");

            var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
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
                DiscountAmount = cartEvaluation.TotalDiscount,
                TaxAmount = cartEvaluation.TaxTotal,
                TotalAmount = cartEvaluation.FinalTotal,
                RoundOff = request.RoundOff,
                NetPayable = request.NetPayable,
                PaymentMode = request.PaymentMode,
                CashAmount = request.CashAmount,
                UpiAmount = request.UpiAmount,
                CardAmount = request.CardAmount,
                WalletAmount = request.WalletAmountUsed,
                Status = "COMPLETED"
            };

            foreach (var item in cartEvaluation.Items)
            {
                var product = productsInfo.TryGetValue(item.ProductId, out var p) ? p : null;
                decimal cgstRate = product?.TaxSlab?.CgstRate ?? 0;
                decimal sgstRate = product?.TaxSlab?.SgstRate ?? 0;
                decimal cgstAmount = item.FinalLineTotal * (cgstRate / 100m);
                decimal sgstAmount = item.FinalLineTotal * (sgstRate / 100m);

                invoice.Items.Add(new InvoiceItem
                {
                    ProductId = item.ProductId,
                    ProductName = product?.Name ?? string.Empty,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Total = item.LineTotal,
                    DiscountAmount = item.DiscountAmount,
                    FinalTotal = item.FinalLineTotal,
                    BusinessDate = today,
                    CgstRate = cgstRate,
                    CgstAmount = cgstAmount,
                    SgstRate = sgstRate,
                    SgstAmount = sgstAmount
                });
            }

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync(cancellationToken); 

            // Deduct Stock
            Guid storeId = Guid.Empty;
            var rules = InventoryRulesManager.GetRules();

            if (rules.PreventNegativeStock)
            {
                foreach (var item in cartEvaluation.Items)
                {
                    var product = productsInfo.TryGetValue(item.ProductId, out var p) ? p : null;
                    var productName = product?.Name ?? "Unknown Product";

                    var availableStock = await _context.StockLedger
                        .Where(sl => sl.ProductId == item.ProductId && sl.StoreId == storeId)
                        .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;

                    if (availableStock < item.Quantity)
                    {
                        bool overrideApproved = false;
                        if (!string.IsNullOrWhiteSpace(request.SupervisorOverridePin))
                        {
                            var usersWithPin = await _context.Users
                                .Where(u => u.IsActive && !u.IsDeleted && u.PinHash != null)
                                .ToListAsync(cancellationToken);

                            foreach (var user in usersWithPin)
                            {
                                if (_passwordHasher.VerifyPassword(request.SupervisorOverridePin, user.PinHash!))
                                {
                                    overrideApproved = true;
                                    break;
                                }
                            }
                        }

                        if (!overrideApproved)
                        {
                            throw new Exception($"INSUFFICIENT_STOCK: Item '{productName}' is out of stock. Available: {availableStock}, Requested: {item.Quantity}. Scan a supervisor PIN to override.");
                        }
                    }
                }
            }

            foreach (var item in cartEvaluation.Items)
            {
                var originalItem = request.Items.FirstOrDefault(x => x.ProductId == item.ProductId);
                Guid? selectedBatchId = originalItem?.BatchId;
                var hasExpiry = productsInfo.TryGetValue(item.ProductId, out var pInfo) && pInfo.HasExpiry;
                DateTime? expiryDate = null;

                if (selectedBatchId == null && hasExpiry)
                {
                    var activeBatches = await _context.ProductBatches
                        .Where(b => b.ProductId == item.ProductId && b.IsActive)
                        .ToListAsync(cancellationToken);

                    if (activeBatches.Any())
                    {
                        var batchStocks = new List<(Guid Id, DateTime? ExpiryDate, decimal Stock)>();
                        foreach (var b in activeBatches)
                        {
                            var stock = await _context.StockLedger
                                .Where(sl => sl.ProductId == item.ProductId && sl.BatchId == b.Id)
                                .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;
                            batchStocks.Add((b.Id, b.ExpiryDate, stock));
                        }

                        var bestBatchId = batchStocks
                            .Where(x => x.Stock > 0)
                            .OrderBy(x => x.ExpiryDate.HasValue ? 0 : 1)
                            .ThenBy(x => x.ExpiryDate)
                            .Select(x => (Guid?)x.Id)
                            .FirstOrDefault();

                        if (bestBatchId == null)
                        {
                            bestBatchId = batchStocks
                                .OrderBy(x => x.ExpiryDate.HasValue ? 0 : 1)
                                .ThenBy(x => x.ExpiryDate)
                                .Select(x => (Guid?)x.Id)
                                .FirstOrDefault();
                        }

                        selectedBatchId = bestBatchId;
                    }
                }

                if (selectedBatchId.HasValue)
                {
                    var selectedBatch = await _context.ProductBatches.FindAsync(new object[] { selectedBatchId.Value }, cancellationToken);
                    expiryDate = selectedBatch?.ExpiryDate;
                }

                // Check if this specific item breached stock level to log override status
                var itemStock = await _context.StockLedger
                    .Where(sl => sl.ProductId == item.ProductId && sl.StoreId == storeId)
                    .SumAsync(sl => (decimal?)sl.Quantity, cancellationToken) ?? 0;

                string movementTypeVal = (rules.PreventNegativeStock && itemStock < item.Quantity) ? "SALE_OVERRIDE" : "SALE";

                await _stockLedgerService.RecordMovementAsync(
                    storeId: storeId,
                    warehouseId: null,
                    terminalId: request.TerminalId,
                    businessDate: today,
                    productId: item.ProductId,
                    batchId: selectedBatchId,
                    movementType: movementTypeVal,
                    quantity: -item.Quantity, // Negative quantity for stock deduction
                    unitCost: item.UnitPrice, // In a real system, this would be the actual cost price, not selling price.
                    expiryDate: expiryDate,
                    referenceDocId: invoice.Id,
                    referenceNumber: $"INV-{invoice.InvoiceNumber}",
                    userId: request.CashierId,
                    cancellationToken: cancellationToken
                );
            }

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
                null, DateTime.UtcNow, $"POS Invoice {invoice.InvoiceNumber}", $"INV-{invoice.Id}", journalLines, cancellationToken);

            // Post Dedicated Tax Transaction for GSTR Returns
            await _financialPostingService.RecordGstTransactionAsync(
                null, "SALE", invoice.InvoiceNumber, DateTime.UtcNow, taxableValue, cgst, sgst, null, cancellationToken);

            // Flush ALL pending EF changes (loyalty ledger, wallet, financial lines) before committing the transaction
            await _context.SaveChangesAsync(cancellationToken);

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
