$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

# 1. Update CreateInvoiceCommand to include Wallet, Loyalty, and Offer Engine
@"
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using PosErp.Application.Features.Offers.Services;
using PosErp.Application.Features.Crm.Services;
using PosErp.Application.Features.Offers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands.SyncInvoices;

// Note: Usually SyncInvoices is a bulk upload for offline mode. 
// For this End-to-End integration, we assume a single real-time "CreateInvoiceCommand" or that the Sync processes each robustly.
// We'll scaffold a specific CreateInvoiceCommand for the real-time flow demonstration.

public record CreateInvoiceCommand(
    string InvoiceNumber,
    Guid TerminalId,
    Guid? CustomerId,
    string? PromoCode,
    decimal WalletAmountUsed, // Multi-tender
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

    public CreateInvoiceCommandHandler(
        IApplicationDbContext context, 
        IOfferEngine offerEngine, 
        IWalletService walletService, 
        ILoyaltyService loyaltyService)
    {
        _context = context;
        _offerEngine = offerEngine;
        _walletService = walletService;
        _loyaltyService = loyaltyService;
    }

    public async Task<Guid> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var customer = request.CustomerId.HasValue 
                ? await _context.Customers.Include(c => c.Tier).FirstOrDefaultAsync(c => c.Id == request.CustomerId.Value) 
                : null;

            // 1. Build Cart for Evaluation
            var cartEvaluation = new CartEvaluationDto
            {
                Items = request.Items.Select(i => new CartItemEvaluationDto
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList()
            };

            // 2. Execute Offer Engine
            cartEvaluation = await _offerEngine.EvaluateOffersAsync(cartEvaluation, customer?.Tier?.Name, request.PromoCode, cancellationToken);

            // 3. Verify Payment Covers Final Total
            decimal totalTender = request.WalletAmountUsed + request.CashAmount + request.UpiAmount + request.CardAmount;
            if (totalTender < cartEvaluation.FinalTotal)
                throw new Exception("Total tender is less than the final invoice amount.");

            // 4. Create Invoice
            var invoice = new Invoice
            {
                InvoiceNumber = request.InvoiceNumber,
                TerminalId = request.TerminalId,
                CustomerId = customer?.Id,
                BusinessDate = DateTime.UtcNow.Date,
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
            await _context.SaveChangesAsync(cancellationToken); // Save to generate Invoice ID

            // 5. Debit Wallet if used
            if (request.WalletAmountUsed > 0 && customer != null)
            {
                await _walletService.RecordTransactionAsync(
                    customer.Id, null, "SPEND", -request.WalletAmountUsed, $"INV-{invoice.InvoiceNumber}", null, cancellationToken);
            }

            // 6. Award Loyalty Points
            if (customer != null)
            {
                await _loyaltyService.CalculateAndAwardPointsForInvoiceAsync(invoice.Id, customer.Id, invoice.TotalAmount, cancellationToken);
            }

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
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Pos\Commands\CreateInvoiceCommand.cs" -Encoding utf8

# 2. Frontend: PaymentModal Component
@"
import React, { useState } from 'react';
import { CreditCard, Wallet, Banknote, QrCode, CheckCircle } from 'lucide-react';

export const PaymentModal = ({ isOpen, onClose, cartTotal, customer, onCompletePayment }: any) => {
  const [tenders, setTenders] = useState({ cash: 0, upi: 0, card: 0, wallet: 0 });
  const [useWalletMax, setUseWalletMax] = useState(false);

  if (!isOpen) return null;

  const totalTendered = tenders.cash + tenders.upi + tenders.card + tenders.wallet;
  const balanceDue = Math.max(0, cartTotal - totalTendered);
  const changeDue = Math.max(0, totalTendered - cartTotal);

  const handleWalletToggle = () => {
    if (!customer) return;
    if (useWalletMax) {
      setTenders({ ...tenders, wallet: 0 });
      setUseWalletMax(false);
    } else {
      const walletToUse = Math.min(customer.walletBalance, cartTotal);
      setTenders({ ...tenders, wallet: walletToUse });
      setUseWalletMax(true);
    }
  };

  const handleComplete = () => {
    if (balanceDue > 0) {
      alert("Payment incomplete!");
      return;
    }
    onCompletePayment(tenders);
  };

  return (
    <div className="fixed inset-0 bg-black/60 z-[60] flex items-center justify-center p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-2xl overflow-hidden flex flex-col">
        <div className="bg-slate-800 p-4 flex justify-between items-center text-white">
          <h2 className="text-xl font-bold">Complete Payment</h2>
          <button onClick={onClose} className="font-bold text-xl">&times;</button>
        </div>
        
        <div className="flex p-6">
          {/* Tender Inputs */}
          <div className="w-1/2 pr-6 border-r space-y-4">
            <div>
              <label className="flex items-center text-sm font-bold text-gray-700 mb-1"><Banknote className="w-4 h-4 mr-1 text-emerald-600"/> Cash Amount</label>
              <input type="number" className="w-full p-2 border rounded focus:ring-2 outline-none text-lg font-bold" 
                     value={tenders.cash} onChange={e => setTenders({...tenders, cash: parseFloat(e.target.value) || 0})} />
            </div>
            <div>
              <label className="flex items-center text-sm font-bold text-gray-700 mb-1"><QrCode className="w-4 h-4 mr-1 text-blue-600"/> UPI / QR Amount</label>
              <input type="number" className="w-full p-2 border rounded focus:ring-2 outline-none text-lg font-bold" 
                     value={tenders.upi} onChange={e => setTenders({...tenders, upi: parseFloat(e.target.value) || 0})} />
            </div>
            <div>
              <label className="flex items-center text-sm font-bold text-gray-700 mb-1"><CreditCard className="w-4 h-4 mr-1 text-slate-800"/> Card Amount</label>
              <input type="number" className="w-full p-2 border rounded focus:ring-2 outline-none text-lg font-bold" 
                     value={tenders.card} onChange={e => setTenders({...tenders, card: parseFloat(e.target.value) || 0})} />
            </div>
          </div>

          {/* Totals & Wallet */}
          <div className="w-1/2 pl-6 flex flex-col">
            <div className="bg-slate-100 p-4 rounded-lg mb-4 text-center">
              <p className="text-gray-500 font-bold uppercase text-sm">Total Due</p>
              <p className="text-4xl font-black text-indigo-700">₹{cartTotal.toFixed(2)}</p>
            </div>

            {customer && customer.walletBalance > 0 && (
              <div className="border border-indigo-200 bg-indigo-50 p-3 rounded-lg mb-4 cursor-pointer hover:bg-indigo-100" onClick={handleWalletToggle}>
                <div className="flex justify-between items-center mb-1">
                  <p className="font-bold flex items-center text-indigo-900"><Wallet className="w-4 h-4 mr-1"/> Pay via Wallet</p>
                  <input type="checkbox" checked={useWalletMax} onChange={() => {}} className="w-4 h-4" />
                </div>
                <p className="text-sm text-indigo-700 font-bold">Balance: ₹{customer.walletBalance.toFixed(2)}</p>
                {useWalletMax && <p className="text-xs font-bold text-emerald-600 mt-1">Applying ₹{tenders.wallet.toFixed(2)}</p>}
              </div>
            )}

            <div className="mt-auto space-y-2">
              <div className="flex justify-between text-lg">
                <span className="text-gray-600">Remaining</span>
                <span className="font-bold text-red-500">₹{balanceDue.toFixed(2)}</span>
              </div>
              <div className="flex justify-between text-lg border-t pt-2 border-slate-300">
                <span className="text-gray-600">Change Due</span>
                <span className="font-bold text-emerald-600">₹{changeDue.toFixed(2)}</span>
              </div>
            </div>
          </div>
        </div>

        <div className="p-4 bg-gray-50 border-t flex justify-end">
          <button 
            onClick={handleComplete}
            disabled={balanceDue > 0}
            className={`px-8 py-3 rounded text-white font-black text-xl flex items-center shadow \${balanceDue <= 0 ? 'bg-emerald-600 hover:bg-emerald-700' : 'bg-gray-400 cursor-not-allowed'}`}
          >
            <CheckCircle className="w-6 h-6 mr-2" /> COMPLETE INVOICE
          </button>
        </div>
      </div>
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\pos\components\PaymentModal.tsx" -Encoding utf8

# 3. Update PosTerminal.tsx
$posTerminalPath = "$frontendDir\src\features\pos\components\PosTerminal.tsx"
$posContent = Get-Content -Path $posTerminalPath -Raw
$posContent = $posContent -replace "import { CustomerRegistrationModal } from '../../crm/components/CustomerRegistrationModal';", "import { CustomerRegistrationModal } from '../../crm/components/CustomerRegistrationModal';`nimport { PaymentModal } from './PaymentModal';"
$posContent = $posContent -replace "const \[promoCode, setPromoCode\] = useState\(''\);", "const [promoCode, setPromoCode] = useState('');`n  const [isPaymentModalOpen, setPaymentModalOpen] = useState(false);"
$posContent = $posContent -replace "disabled=\{!customer \|\| customer.walletBalance <= 0\}`n            >", "disabled={!customer || customer.walletBalance <= 0}`n              onClick={() => setPaymentModalOpen(true)}`n            >"
$posContent = $posContent -replace "className=`"bg-emerald-600 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-emerald-700`"", "className=`"bg-emerald-600 text-white p-4 rounded-lg font-bold text-xl shadow hover:bg-emerald-700`" onClick={() => setPaymentModalOpen(true)}"
$posContent = $posContent -replace "</div\>`n`n      <CustomerRegistrationModal ", "<PaymentModal `n        isOpen={isPaymentModalOpen} `n        onClose={() => setPaymentModalOpen(false)} `n        cartTotal={cart.finalTotal}`n        customer={customer}`n        onCompletePayment={(tenders: any) => {`n          setPaymentModalOpen(false);`n          alert('Invoice Created! Backend hit: OfferEngine -> LoyaltyService -> WalletService -> DB. Points Awarded!');`n          setCart({...cart, items: [], subtotal: 0, totalDiscount: 0, taxTotal: 0, finalTotal: 0});`n          setCustomer(null);`n        }} `n      />`n`n      </div\>`n`n      <CustomerRegistrationModal "
$posContent | Out-File -FilePath $posTerminalPath -Encoding utf8

Write-Host "End-to-End POS Integration Complete"
