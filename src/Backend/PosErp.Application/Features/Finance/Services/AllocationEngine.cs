using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Finance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Services;

public interface IAllocationEngine
{
    Task AllocateSupplierPaymentAsync(Guid paymentId, string mode, List<ManualAllocationInputDto>? manualAllocations, CancellationToken cancellationToken);
    Task AllocateCustomerReceiptAsync(Guid receiptId, string mode, List<ManualAllocationInputDto>? manualAllocations, CancellationToken cancellationToken);
}

public class ManualAllocationInputDto
{
    public Guid DocumentId { get; set; }
    public decimal Amount { get; set; }
}

public class AllocationEngine : IAllocationEngine
{
    private readonly IApplicationDbContext _context;

    public AllocationEngine(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AllocateSupplierPaymentAsync(Guid paymentId, string mode, List<ManualAllocationInputDto>? manualAllocations, CancellationToken cancellationToken)
    {
        var payment = await _context.SupplierPayments
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

        if (payment == null) throw new InvalidOperationException("Supplier payment not found.");

        decimal amountToAllocate = payment.Amount;

        if (string.Equals(mode, "MANUAL", StringComparison.OrdinalIgnoreCase))
        {
            if (manualAllocations == null || !manualAllocations.Any())
                throw new InvalidOperationException("Manual allocations list cannot be empty in MANUAL mode.");

            decimal sum = manualAllocations.Sum(a => a.Amount);
            if (sum > payment.Amount)
                throw new InvalidOperationException($"Sum of manual allocations ({sum}) cannot exceed payment amount ({payment.Amount}).");

            foreach (var allocInput in manualAllocations)
            {
                var bill = await _context.PurchaseBills
                    .FirstOrDefaultAsync(b => b.Id == allocInput.DocumentId, cancellationToken);

                if (bill == null)
                    throw new InvalidOperationException($"Purchase bill with ID {allocInput.DocumentId} not found.");

                // Calculate current allocated amount
                decimal currentAllocated = await _context.SupplierPaymentAllocations
                    .Where(a => a.PurchaseBillId == bill.Id)
                    .SumAsync(a => a.AllocatedAmount, cancellationToken);

                decimal outstanding = bill.TotalAmount - currentAllocated;
                if (allocInput.Amount > outstanding)
                    throw new InvalidOperationException($"Cannot allocate {allocInput.Amount} to bill {bill.BillNumber} which has outstanding {outstanding}.");

                var allocation = new SupplierPaymentAllocation
                {
                    PaymentId = paymentId,
                    PurchaseBillId = bill.Id,
                    AllocatedAmount = allocInput.Amount,
                    CreatedAt = DateTime.UtcNow
                };
                _context.SupplierPaymentAllocations.Add(allocation);

                // Update bill status
                if (currentAllocated + allocInput.Amount >= bill.TotalAmount)
                {
                    bill.Status = "PAID";
                }
                else
                {
                    bill.Status = "PARTIALLY_PAID";
                }
            }
        }
        else // AUTO_FIFO
        {
            // Fetch outstanding bills (excluding PAID bills) ordered by date ascending (FIFO)
            var outstandingBills = await _context.PurchaseBills
                .Where(b => b.SupplierId == payment.SupplierId && b.StoreId == payment.StoreId && b.Status != "PAID")
                .OrderBy(b => b.BillDate)
                .ToListAsync(cancellationToken);

            foreach (var bill in outstandingBills)
            {
                if (amountToAllocate <= 0) break;

                decimal currentAllocated = await _context.SupplierPaymentAllocations
                    .Where(a => a.PurchaseBillId == bill.Id)
                    .SumAsync(a => a.AllocatedAmount, cancellationToken);

                decimal outstanding = bill.TotalAmount - currentAllocated;
                if (outstanding <= 0) continue;

                decimal allocatedAmount = Math.Min(amountToAllocate, outstanding);

                var allocation = new SupplierPaymentAllocation
                {
                    PaymentId = paymentId,
                    PurchaseBillId = bill.Id,
                    AllocatedAmount = allocatedAmount,
                    CreatedAt = DateTime.UtcNow
                };
                _context.SupplierPaymentAllocations.Add(allocation);

                amountToAllocate -= allocatedAmount;

                if (currentAllocated + allocatedAmount >= bill.TotalAmount)
                {
                    bill.Status = "PAID";
                }
                else
                {
                    bill.Status = "PARTIALLY_PAID";
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AllocateCustomerReceiptAsync(Guid receiptId, string mode, List<ManualAllocationInputDto>? manualAllocations, CancellationToken cancellationToken)
    {
        var receipt = await _context.CustomerReceipts
            .FirstOrDefaultAsync(r => r.Id == receiptId, cancellationToken);

        if (receipt == null) throw new InvalidOperationException("Customer receipt not found.");

        decimal amountToAllocate = receipt.Amount;

        if (string.Equals(mode, "MANUAL", StringComparison.OrdinalIgnoreCase))
        {
            if (manualAllocations == null || !manualAllocations.Any())
                throw new InvalidOperationException("Manual allocations list cannot be empty in MANUAL mode.");

            decimal sum = manualAllocations.Sum(a => a.Amount);
            if (sum > receipt.Amount)
                throw new InvalidOperationException($"Sum of manual allocations ({sum}) cannot exceed receipt amount ({receipt.Amount}).");

            foreach (var allocInput in manualAllocations)
            {
                // Invoices have a composite key, so DocumentId is the Invoice ID. We retrieve InvoiceBusinessDate from the record.
                var invoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.Id == allocInput.DocumentId, cancellationToken);

                if (invoice == null)
                    throw new InvalidOperationException($"Invoice with ID {allocInput.DocumentId} not found.");

                // Calculate current allocated amount
                decimal currentAllocated = await _context.CustomerReceiptAllocations
                    .Where(a => a.InvoiceId == invoice.Id)
                    .SumAsync(a => a.AllocatedAmount, cancellationToken);

                decimal outstanding = invoice.NetPayable - currentAllocated;
                if (allocInput.Amount > outstanding)
                    throw new InvalidOperationException($"Cannot allocate {allocInput.Amount} to invoice {invoice.InvoiceNumber} which has outstanding {outstanding}.");

                var allocation = new CustomerReceiptAllocation
                {
                    ReceiptId = receiptId,
                    InvoiceId = invoice.Id,
                    InvoiceBusinessDate = invoice.BusinessDate,
                    AllocatedAmount = allocInput.Amount,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CustomerReceiptAllocations.Add(allocation);

                // Update invoice status
                if (currentAllocated + allocInput.Amount >= invoice.NetPayable)
                {
                    invoice.Status = "PAID";
                }
                else
                {
                    invoice.Status = "PARTIALLY_PAID";
                }
            }
        }
        else // AUTO_FIFO
        {
            // Fetch outstanding credit invoices (excluding PAID / CANCELLED status) ordered by date ascending
            var outstandingInvoices = await _context.Invoices
                .Where(i => i.CustomerId == receipt.CustomerId && i.StoreId == receipt.StoreId && i.Status != "PAID" && i.Status != "CANCELLED" && i.PaymentMode == "CREDIT")
                .OrderBy(i => i.BusinessDate)
                .ToListAsync(cancellationToken);

            foreach (var invoice in outstandingInvoices)
            {
                if (amountToAllocate <= 0) break;

                decimal currentAllocated = await _context.CustomerReceiptAllocations
                    .Where(a => a.InvoiceId == invoice.Id)
                    .SumAsync(a => a.AllocatedAmount, cancellationToken);

                decimal outstanding = invoice.NetPayable - currentAllocated;
                if (outstanding <= 0) continue;

                decimal allocatedAmount = Math.Min(amountToAllocate, outstanding);

                var allocation = new CustomerReceiptAllocation
                {
                    ReceiptId = receiptId,
                    InvoiceId = invoice.Id,
                    InvoiceBusinessDate = invoice.BusinessDate,
                    AllocatedAmount = allocatedAmount,
                    CreatedAt = DateTime.UtcNow
                };
                _context.CustomerReceiptAllocations.Add(allocation);

                amountToAllocate -= allocatedAmount;

                if (currentAllocated + allocatedAmount >= invoice.NetPayable)
                {
                    invoice.Status = "PAID";
                }
                else
                {
                    invoice.Status = "PARTIALLY_PAID";
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
