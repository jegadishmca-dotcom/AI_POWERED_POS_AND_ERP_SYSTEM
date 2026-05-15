$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Pos\Commands\CreateInvoice"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Pos\Commands\ParkInvoice"

# 1. Backend: CreateInvoiceCommand (Online Mode)
@"
using MediatR;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands.CreateInvoice;

public record CreateInvoiceCommand(OfflineInvoiceDto Invoice) : IRequest<Guid>;

public class CreateInvoiceCommandHandler : IRequestHandler<CreateInvoiceCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateInvoiceCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Invoice;

        var invoice = new Invoice
        {
            Id = dto.Id,
            BusinessDate = dto.BusinessDate,
            InvoiceNumber = dto.InvoiceNumber,
            TerminalId = dto.TerminalId,
            TerminalSequence = dto.TerminalSequence,
            CashierId = dto.CashierId,
            SubTotal = dto.SubTotal,
            DiscountAmount = dto.DiscountAmount,
            TaxAmount = dto.TaxAmount,
            TotalAmount = dto.TotalAmount,
            RoundOff = dto.RoundOff,
            NetPayable = dto.NetPayable,
            PaymentMode = dto.PaymentMode,
            Status = "COMPLETED"
        };

        foreach (var itemDto in dto.Items)
        {
            invoice.Items.Add(new InvoiceItem
            {
                Id = itemDto.Id,
                BusinessDate = dto.BusinessDate,
                ProductId = itemDto.ProductId,
                Barcode = itemDto.Barcode,
                ProductName = itemDto.ProductName,
                Quantity = itemDto.Quantity,
                UnitPrice = itemDto.UnitPrice,
                DiscountAmount = itemDto.DiscountAmount,
                CgstRate = itemDto.CgstRate,
                CgstAmount = itemDto.CgstAmount,
                SgstRate = itemDto.SgstRate,
                SgstAmount = itemDto.SgstAmount,
                CessRate = itemDto.CessRate,
                CessAmount = itemDto.CessAmount,
                TotalAmount = itemDto.TotalAmount
            });
        }

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Pos\Commands\CreateInvoice\CreateInvoiceCommand.cs" -Encoding utf8

# 2. Update SyncInvoicesCommand to be robust
@"
using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Pos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands.SyncInvoices;

public record SyncInvoicesCommand(List<OfflineInvoiceDto> Invoices) : IRequest<SyncResult>;
public record SyncResult(int Synced, int Failed, List<string> Errors);

public record OfflineInvoiceDto(
    Guid Id,
    DateTime BusinessDate,
    string InvoiceNumber,
    Guid TerminalId,
    int TerminalSequence,
    Guid CashierId,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal RoundOff,
    decimal NetPayable,
    string PaymentMode,
    List<OfflineInvoiceItemDto> Items
);

public record OfflineInvoiceItemDto(
    Guid Id,
    Guid ProductId,
    string? Barcode,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    decimal CgstRate,
    decimal CgstAmount,
    decimal SgstRate,
    decimal SgstAmount,
    decimal CessRate,
    decimal CessAmount,
    decimal TotalAmount
);

public class SyncInvoicesCommandHandler : IRequestHandler<SyncInvoicesCommand, SyncResult>
{
    private readonly IApplicationDbContext _context;

    public SyncInvoicesCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SyncResult> Handle(SyncInvoicesCommand request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        int synced = 0;
        int failed = 0;

        foreach (var dto in request.Invoices)
        {
            try
            {
                // Composite unique constraint check: TerminalId, TerminalSequence, BusinessDate.Date
                var exists = await _context.Invoices.AnyAsync(i => 
                    (i.Id == dto.Id) || 
                    (i.TerminalId == dto.TerminalId && i.TerminalSequence == dto.TerminalSequence && i.BusinessDate.Date == dto.BusinessDate.Date),
                    cancellationToken);
                
                if (exists)
                {
                    synced++;
                    continue; 
                }

                // ... Invoice mapping same as before ...
                var invoice = new Invoice {
                    Id = dto.Id, BusinessDate = dto.BusinessDate, InvoiceNumber = dto.InvoiceNumber,
                    TerminalId = dto.TerminalId, TerminalSequence = dto.TerminalSequence, CashierId = dto.CashierId,
                    SubTotal = dto.SubTotal, DiscountAmount = dto.DiscountAmount, TaxAmount = dto.TaxAmount,
                    TotalAmount = dto.TotalAmount, RoundOff = dto.RoundOff, NetPayable = dto.NetPayable,
                    PaymentMode = dto.PaymentMode, Status = "COMPLETED"
                };

                foreach (var itemDto in dto.Items)
                {
                    invoice.Items.Add(new InvoiceItem {
                        Id = itemDto.Id, BusinessDate = dto.BusinessDate, ProductId = itemDto.ProductId, Barcode = itemDto.Barcode,
                        ProductName = itemDto.ProductName, Quantity = itemDto.Quantity, UnitPrice = itemDto.UnitPrice,
                        DiscountAmount = itemDto.DiscountAmount, CgstRate = itemDto.CgstRate, CgstAmount = itemDto.CgstAmount,
                        SgstRate = itemDto.SgstRate, SgstAmount = itemDto.SgstAmount, CessRate = itemDto.CessRate,
                        CessAmount = itemDto.CessAmount, TotalAmount = itemDto.TotalAmount
                    });
                }
                _context.Invoices.Add(invoice);
                synced++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"Failed to sync {dto.InvoiceNumber}: {ex.Message}");
            }
        }

        if (synced > 0) await _context.SaveChangesAsync(cancellationToken);
        return new SyncResult(synced, failed, errors);
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Pos\Commands\SyncInvoices\SyncInvoicesCommand.cs" -Encoding utf8

# 3. PosController Update (Online Create + Print by ID)
@"
using Microsoft.AspNetCore.Mvc;
using MediatR;
using System.Threading.Tasks;
using PosErp.Application.Features.Pos.Commands.SyncInvoices;
using PosErp.Application.Features.Pos.Commands.CreateInvoice;
using PosErp.Infrastructure.Printing;
using System;

namespace PosErp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PosController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPrintService _printService;

    public PosController(IMediator mediator, IPrintService printService)
    {
        _mediator = mediator;
        _printService = printService;
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

    [HttpPost("print/{invoiceId}")]
    public async Task<IActionResult> PrintReceipt(Guid invoiceId)
    {
        // Fetch invoice from DB using MediatR query (assumed)
        // Convert to ESC/POS bytes
        // Send to Printer IP
        await _printService.PrintReceiptAsync("192.168.1.100", 9100, "Receipt Content Placeholder for " + invoiceId);
        return Ok();
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Api\Controllers\PosController.cs" -Encoding utf8

# 4. Frontend Dexie DB updates
@"
import Dexie, { Table } from 'dexie';
import { Invoice } from '../types';

export interface LocalProduct {
  id: string;
  code: string;
  name: string;
  barcode: string;
  price: number;
  isWeighable: boolean;
}

export class PosDatabase extends Dexie {
  catalog!: Table<LocalProduct, string>;
  invoices!: Table<Invoice, string>;
  sync_queue!: Table<Invoice, string>;
  held_invoices!: Table<Invoice, string>; // Local holding

  constructor() {
    super('PosDatabase');
    this.version(2).stores({
      catalog: 'id, code, barcode, name', 
      invoices: 'id, status',
      sync_queue: 'id',
      held_invoices: 'id' // Held carts
    });
  }
}

export const posDb = new PosDatabase();
"@ | Out-File -FilePath "$frontendDir\src\features\pos\db\pos.db.ts" -Encoding utf8

# 5. Payment Modal Component (Multi-Tender)
@"
import React, { useState } from 'react';
import { X, CreditCard, Wallet, Banknote, QrCode } from 'lucide-react';

export const PaymentModal = ({ isOpen, onClose, onPay, totalAmount }: any) => {
  const [tenderType, setTenderType] = useState('CASH');
  const [amountTendered, setAmountTendered] = useState(totalAmount);

  if (!isOpen) return null;

  const handlePay = () => {
    onPay({ type: tenderType, amount: amountTendered });
  };

  return (
    <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4">
      <div className="bg-white rounded-xl shadow-2xl w-full max-w-2xl overflow-hidden flex flex-col">
        <div className="bg-slate-800 p-4 flex justify-between items-center text-white">
          <h2 className="text-xl font-bold">Complete Payment</h2>
          <button onClick={onClose}><X /></button>
        </div>
        
        <div className="p-8 flex gap-8">
          <div className="w-1/3 flex flex-col gap-3">
            <button onClick={() => setTenderType('CASH')} className={`p-4 rounded-lg flex items-center font-bold border-2 \${tenderType === 'CASH' ? 'border-blue-600 bg-blue-50 text-blue-700' : 'border-gray-200'}`}>
              <Banknote className="mr-3" /> Cash
            </button>
            <button onClick={() => setTenderType('CARD')} className={`p-4 rounded-lg flex items-center font-bold border-2 \${tenderType === 'CARD' ? 'border-blue-600 bg-blue-50 text-blue-700' : 'border-gray-200'}`}>
              <CreditCard className="mr-3" /> Card
            </button>
            <button onClick={() => setTenderType('UPI')} className={`p-4 rounded-lg flex items-center font-bold border-2 \${tenderType === 'UPI' ? 'border-blue-600 bg-blue-50 text-blue-700' : 'border-gray-200'}`}>
              <QrCode className="mr-3" /> UPI QR
            </button>
          </div>
          
          <div className="w-2/3 flex flex-col justify-center">
            <div className="text-center mb-6">
              <p className="text-gray-500 font-medium mb-1">Total Due</p>
              <p className="text-5xl font-black text-slate-800">₹{totalAmount.toFixed(2)}</p>
            </div>
            
            {tenderType === 'CASH' && (
              <div className="mb-6">
                <label className="block text-sm font-medium text-gray-700 mb-2">Amount Tendered</label>
                <input 
                  type="number" 
                  value={amountTendered}
                  onChange={(e) => setAmountTendered(parseFloat(e.target.value) || 0)}
                  className="w-full text-3xl p-3 text-right border-2 border-gray-300 rounded focus:border-blue-600 outline-none"
                />
                <div className="flex justify-between mt-4 p-4 bg-gray-100 rounded-lg">
                  <span className="font-bold text-gray-600">Change Due:</span>
                  <span className="font-bold text-red-600 text-xl">₹{Math.max(0, amountTendered - totalAmount).toFixed(2)}</span>
                </div>
              </div>
            )}

            {tenderType === 'UPI' && (
              <div className="text-center p-8 border-2 border-dashed border-gray-300 rounded-lg mb-6">
                <QrCode className="w-32 h-32 mx-auto text-gray-400 mb-4" />
                <p className="text-gray-500 font-bold">Dynamic QR Code (Placeholder)</p>
                <p className="text-sm">Scan to pay ₹{totalAmount.toFixed(2)}</p>
              </div>
            )}

            <button 
              onClick={handlePay}
              className="w-full py-4 bg-emerald-600 text-white font-black text-xl rounded-lg hover:bg-emerald-700 transition"
            >
              CONFIRM PAYMENT
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
"@ | Out-File -FilePath "$frontendDir\src\features\pos\components\PaymentModal.tsx" -Encoding utf8

# 6. Update PosTerminal.tsx
@"
import React, { useState, useEffect } from 'react';
import { useBarcodeScanner } from '../hooks/useBarcodeScanner';
import { usePosKeyboardShortcuts } from '../hooks/usePosKeyboardShortcuts';
import { CartView } from './CartView';
import { PaymentModal } from './PaymentModal';
import { CartItem, Invoice } from '../types';
import { posDb } from '../db/pos.db';
import { v4 as uuidv4 } from 'uuid';
import { api } from '@/utils/api';

export const PosTerminal = () => {
  const [cart, setCart] = useState<CartItem[]>([]);
  const [isPaymentOpen, setIsPaymentOpen] = useState(false);
  const [invoiceId] = useState(uuidv4());

  const totalPayable = cart.reduce((sum, item) => sum + item.totalAmount, 0);

  // Hook 1: Scanner (Optimistic lookup)
  useBarcodeScanner(async (barcode, weight) => {
    const product = await posDb.catalog.where('barcode').equals(barcode).or('code').equals(barcode).first();
    if (product) {
      const qty = weight ? weight : 1;
      setCart(prev => [...prev, {
        id: uuidv4(), productId: product.id, name: product.name,
        quantity: qty, unitPrice: product.price, discountAmount: 0,
        totalAmount: product.price * qty
      }]);
    } else {
      // Beep error
    }
  });

  // Hook 2: F-Keys
  usePosKeyboardShortcuts({
    onF4Payment: () => cart.length > 0 && setIsPaymentOpen(true),
    onF9Park: () => handleHold(),
  });

  // Hold / Park locally
  const handleHold = async () => {
    if (cart.length === 0) return;
    const inv: Invoice = {
      id: invoiceId, businessDate: new Date().toISOString(),
      invoiceNumber: 'HELD-' + new Date().getTime(),
      terminalId: 'T1', terminalSequence: 0, cashierId: 'C1',
      subTotal: totalPayable, discountAmount: 0, taxAmount: 0,
      totalAmount: totalPayable, roundOff: 0, netPayable: totalPayable,
      paymentMode: 'CASH', status: 'HOLD', items: cart
    };
    await posDb.held_invoices.add(inv);
    setCart([]);
  };

  const handleCompletePayment = async (paymentDetails: any) => {
    setIsPaymentOpen(false);
    
    // Generate true composite sequence locally or fetch from Dexie config
    const seq = new Date().getTime() % 10000;
    const invNo = \`T1-\${new Date().toISOString().slice(0,10).replace(/-/g,'')}-\${seq}\`;

    const invoice: Invoice = {
      id: invoiceId, businessDate: new Date().toISOString(),
      invoiceNumber: invNo, terminalId: 'T1', terminalSequence: seq,
      cashierId: 'C1', subTotal: totalPayable, discountAmount: 0, taxAmount: 0,
      totalAmount: totalPayable, roundOff: 0, netPayable: totalPayable,
      paymentMode: paymentDetails.type, status: 'COMPLETED', items: cart
    };

    // Save offline first
    await posDb.sync_queue.add(invoice);
    
    // Fire & Forget Print to API
    try {
      if (navigator.onLine) {
        await api.post(\`/api/pos/print/\${invoice.id}\`, { invoice });
      }
    } catch(e) {}

    setCart([]);
  };

  return (
    <div className="h-screen flex flex-col bg-slate-100">
      <div className="flex-1 flex overflow-hidden">
        <div className="w-1/3 bg-slate-800 text-white p-4 flex flex-col">
          <div className="text-xl font-bold mb-4">POS Engine (Online)</div>
          <div className="grid grid-cols-2 gap-2 flex-1">
            <button className="bg-slate-700 hover:bg-slate-600 p-4 rounded text-center">F1 - Search</button>
            <button className="bg-slate-700 hover:bg-slate-600 p-4 rounded text-center">F2 - Qty/Price</button>
            <button onClick={() => setIsPaymentOpen(true)} className="bg-emerald-600 hover:bg-emerald-500 p-4 rounded text-center">F4 - Pay</button>
            <button onClick={handleHold} className="bg-orange-600 hover:bg-orange-500 p-4 rounded text-center">F9 - Hold Bill</button>
            <button className="bg-blue-600 hover:bg-blue-500 p-4 rounded text-center">F10 - Resume</button>
            <button className="bg-red-600 hover:bg-red-500 p-4 rounded text-center">F12 - Cancel</button>
          </div>
        </div>

        <div className="flex-1 flex flex-col">
          <CartView items={cart} />
          <div className="bg-white p-6 border-t shadow-lg flex justify-between items-center">
            <div className="text-3xl font-bold text-slate-800">
              Total: ₹{totalPayable.toFixed(2)}
            </div>
          </div>
        </div>
      </div>
      
      <PaymentModal 
        isOpen={isPaymentOpen} 
        onClose={() => setIsPaymentOpen(false)} 
        totalAmount={totalPayable}
        onPay={handleCompletePayment}
      />
    </div>
  );
};
"@ | Out-File -FilePath "$frontendDir\src\features\pos\components\PosTerminal.tsx" -Encoding utf8

Write-Host "POS Step 5 Enhancements Applied"
