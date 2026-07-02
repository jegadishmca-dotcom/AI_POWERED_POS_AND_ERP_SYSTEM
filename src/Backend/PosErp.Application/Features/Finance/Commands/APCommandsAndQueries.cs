using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Finance.Services;
using PosErp.Domain.Entities.Finance;
using PosErp.Domain.Entities.Purchasing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Commands;

public record CreatePurchaseBillCommand(
    Guid StoreId,
    Guid GRNHeaderId,
    string BillNumber,
    DateTime BillDate,
    Guid UserId
) : IRequest<Guid>;

public record ProcessSupplierPaymentCommand(
    Guid StoreId,
    Guid SupplierId,
    DateTime PaymentDate,
    string PaymentMode, // CASH, BANK_TRANSFER, CHEQUE, UPI
    string? ReferenceNumber,
    decimal Amount,
    string? Notes,
    string AllocationMode, // AUTO_FIFO, MANUAL
    List<ManualAllocationInputDto>? ManualAllocations,
    Guid UserId
) : IRequest<Guid>;

public record GetSupplierLedgerQuery(Guid SupplierId, Guid StoreId) : IRequest<List<SupplierLedgerDto>>;

public class SupplierLedgerDto
{
    public Guid Id { get; set; }
    public DateTime EntryDate { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RunningBalance { get; set; }
    public string? Description { get; set; }
    public Guid? JournalEntryId { get; set; }
}

public record GetSupplierAgingReportQuery(Guid StoreId, DateTime AsOfDate) : IRequest<List<SupplierAgingDto>>;

public class SupplierAgingDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal TotalOutstanding { get; set; }
    public decimal Current { get; set; }
    public decimal Overdue1To30 { get; set; }
    public decimal Overdue31To60 { get; set; }
    public decimal Overdue61To90 { get; set; }
    public decimal Overdue90Plus { get; set; }
}

public record GetPurchaseBillsQuery(Guid StoreId) : IRequest<List<PurchaseBillDto>>;

public class PurchaseBillDto
{
    public Guid Id { get; set; }
    public Guid? StoreId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid GRNHeaderId { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record GetSupplierPaymentsQuery(Guid StoreId) : IRequest<List<SupplierPaymentDto>>;

public class SupplierPaymentDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public string PaymentMode { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public decimal Amount { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class APCommandsAndQueriesHandler :
    IRequestHandler<CreatePurchaseBillCommand, Guid>,
    IRequestHandler<ProcessSupplierPaymentCommand, Guid>,
    IRequestHandler<GetSupplierLedgerQuery, List<SupplierLedgerDto>>,
    IRequestHandler<GetSupplierAgingReportQuery, List<SupplierAgingDto>>,
    IRequestHandler<GetPurchaseBillsQuery, List<PurchaseBillDto>>,
    IRequestHandler<GetSupplierPaymentsQuery, List<SupplierPaymentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFinancialPostingService _postingService;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IAllocationEngine _allocationEngine;
    private readonly IApprovalWorkflowService _approvalService;

    public APCommandsAndQueriesHandler(
        IApplicationDbContext context,
        IFinancialPostingService postingService,
        IDocumentSequenceService sequenceService,
        IAllocationEngine allocationEngine,
        IApprovalWorkflowService approvalService)
    {
        _context = context;
        _postingService = postingService;
        _sequenceService = sequenceService;
        _allocationEngine = allocationEngine;
        _approvalService = approvalService;
    }

    public async Task<Guid> Handle(CreatePurchaseBillCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var grn = await _context.GRNHeaders
                .Include(g => g.Items)
                .FirstOrDefaultAsync(g => g.Id == request.GRNHeaderId, cancellationToken);

            if (grn == null) throw new InvalidOperationException("GRN not found.");
            if (grn.Status != "CONFIRMED") throw new InvalidOperationException("Only CONFIRMED GRNs can be billed.");

            var supplier = await _context.Suppliers.FindAsync(new object[] { grn.SupplierId }, cancellationToken);
            if (supplier == null) throw new InvalidOperationException("Supplier not found.");

            // Calculate credit days
            int creditDays = 30; // default
            if (!string.IsNullOrEmpty(supplier.PaymentTerms))
            {
                var match = Regex.Match(supplier.PaymentTerms, @"\d+");
                if (match.Success)
                {
                    int.TryParse(match.Value, out creditDays);
                }
            }

            DateTime dueDate = request.BillDate.AddDays(creditDays);

            // Compute total CGST and SGST on purchase bill items
            // For simplicity, we calculate 9% CGST and 9% SGST on cost items if not explicitly set,
            // or sum from the items.
            decimal itemsCost = grn.Items.Sum(i => i.TotalCost);
            decimal cgstRate = 0.09m;
            decimal sgstRate = 0.09m;
            decimal cgstAmount = itemsCost * cgstRate;
            decimal sgstAmount = itemsCost * sgstRate;
            decimal totalAmount = itemsCost + cgstAmount + sgstAmount;

            var bill = new PurchaseBillHeader
            {
                StoreId = request.StoreId,
                SupplierId = grn.SupplierId,
                GRNHeaderId = grn.Id,
                BillNumber = request.BillNumber,
                BillDate = request.BillDate.Date,
                DueDate = dueDate.Date,
                SubTotal = itemsCost,
                TaxAmount = cgstAmount + sgstAmount,
                TotalAmount = totalAmount,
                Status = "PENDING_PAYMENT",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.UserId
            };

            foreach (var item in grn.Items)
            {
                bill.Items.Add(new PurchaseBillItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.AcceptedQuantity,
                    UnitCost = item.UnitCost,
                    TaxAmount = item.TotalCost * (cgstRate + sgstRate),
                    TotalAmount = item.TotalCost * (1 + cgstRate + sgstRate)
                });
            }

            _context.PurchaseBills.Add(bill);
            await _context.SaveChangesAsync(cancellationToken);

            // Post double-entry journal entry:
            // Debit Inventory Asset (SubTotal)
            // Debit Input CGST (cgstAmount)
            // Debit Input SGST (sgstAmount)
            // Credit Accounts Payable - Vendors (TotalAmount)
            string inventoryAccountCode = await ResolveAccountCodeAsync("ASSET", "Inventory", "10300", cancellationToken);
            string inputCgstAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Input CGST", "22030", cancellationToken);
            string inputSgstAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Input SGST", "22040", cancellationToken);
            string apAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Accounts Payable", "20100", cancellationToken);

            var lines = new List<JournalLineDto>
            {
                new() { AccountCode = inventoryAccountCode, Description = $"Inventory addition from GRN {grn.GrnNumber}", Debit = itemsCost, Credit = 0 },
                new() { AccountCode = inputCgstAccountCode, Description = $"Input CGST on Bill {request.BillNumber}", Debit = cgstAmount, Credit = 0 },
                new() { AccountCode = inputSgstAccountCode, Description = $"Input SGST on Bill {request.BillNumber}", Debit = sgstAmount, Credit = 0 },
                new() { AccountCode = apAccountCode, Description = $"Accounts Payable vendor {supplier.Name}", Debit = 0, Credit = totalAmount }
            };

            Guid jeId = await _postingService.PostJournalEntryWithUserAsync(
                request.StoreId,
                request.BillDate,
                $"Supplier Purchase Bill {request.BillNumber} matching GRN {grn.GrnNumber}",
                request.BillNumber,
                lines,
                request.UserId,
                isDraft: false,
                cancellationToken,
                sourceModule: "PURCHASING",
                sourceDocType: "PURCHASE_BILL",
                sourceDocId: bill.Id
            );

            // Add record to supplier ledger (Credit vendor)
            decimal runningBalance = await _context.SupplierLedger
                .Where(s => s.SupplierId == grn.SupplierId && s.StoreId == request.StoreId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.RunningBalance)
                .FirstOrDefaultAsync(cancellationToken);

            runningBalance += totalAmount; // Credit increases accounts payable liability

            var ledgerEntry = new SupplierLedgerEntry
            {
                StoreId = request.StoreId,
                SupplierId = grn.SupplierId,
                EntryDate = request.BillDate.Date,
                TransactionType = "BILL",
                ReferenceNumber = request.BillNumber,
                DebitAmount = 0,
                CreditAmount = totalAmount,
                RunningBalance = runningBalance,
                Description = $"Purchase Bill {request.BillNumber} posted",
                JournalEntryId = jeId,
                CreatedAt = DateTime.UtcNow
            };

            _context.SupplierLedger.Add(ledgerEntry);
            await _postingService.RecordGstTransactionAsync(
                request.StoreId,
                "PURCHASE",
                request.BillNumber,
                request.BillDate,
                itemsCost,
                cgstAmount,
                sgstAmount,
                0,
                null,
                cancellationToken
            );

            grn.Status = "BILLED";
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return bill.Id;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Guid> Handle(ProcessSupplierPaymentCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var supplier = await _context.Suppliers.FindAsync(new object[] { request.SupplierId }, cancellationToken);
            if (supplier == null) throw new InvalidOperationException("Supplier not found.");

            // Generate Payment Sequence Number
            string payNumber = await _sequenceService.GenerateNextNumberAsync(request.StoreId, "SUPPLIER_PAYMENT", cancellationToken);

            // Determine if approval is needed (Manager limit default ₹25,000)
            bool requiresApproval = await _approvalService.RequiresApprovalAsync(request.StoreId, "SUPPLIER_PAYMENT", request.Amount, cancellationToken);

            var payment = new SupplierPayment
            {
                StoreId = request.StoreId,
                SupplierId = request.SupplierId,
                PaymentDate = request.PaymentDate.Date,
                PaymentNumber = payNumber,
                PaymentMode = request.PaymentMode,
                ReferenceNumber = request.ReferenceNumber,
                Amount = request.Amount,
                Status = requiresApproval ? "PENDING_APPROVAL" : "POSTED",
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.SupplierPayments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);

            if (requiresApproval)
            {
                // Route to approval requests
                await _approvalService.SubmitForApprovalAsync(request.StoreId, "SUPPLIER_PAYMENT", payment.Id, request.Amount, request.UserId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return payment.Id;
            }

            // Directly post payment
            Guid jeId = await PostPaymentGLAndLedgerAsync(payment, request.UserId, cancellationToken);
            payment.JournalEntryId = jeId;

            await _context.SaveChangesAsync(cancellationToken);

            // Run allocations
            await _allocationEngine.AllocateSupplierPaymentAsync(payment.Id, request.AllocationMode, request.ManualAllocations, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return payment.Id;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<Guid> PostPaymentGLAndLedgerAsync(SupplierPayment payment, Guid userId, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers.FindAsync(new object[] { payment.SupplierId }, cancellationToken);

        // Journal: Debit Accounts Payable - Vendors, Credit Bank Account (10200)
        string apAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Accounts Payable", "20100", cancellationToken);
        string digitalAccountCode = await ResolveAccountCodeAsync("ASSET", "Current", "10200", cancellationToken);

        var lines = new List<JournalLineDto>
        {
            new() { AccountCode = apAccountCode, Description = $"Supplier payment to {supplier?.Name ?? "Vendor"}", Debit = payment.Amount, Credit = 0 },
            new() { AccountCode = digitalAccountCode, Description = $"Bank payout for payment {payment.PaymentNumber}", Debit = 0, Credit = payment.Amount }
        };

        Guid jeId = await _postingService.PostJournalEntryWithUserAsync(
            payment.StoreId,
            payment.PaymentDate,
            $"Vendor payout to {supplier?.Name ?? "Supplier"} ({payment.PaymentNumber})",
            payment.PaymentNumber,
            lines,
            userId,
            isDraft: false,
            cancellationToken,
            sourceModule: "FINANCE",
            sourceDocType: "SUPPLIER_PAYMENT",
            sourceDocId: payment.Id
        );

        // Insert vendor ledger record (Debit vendor, reduces accounts payable)
        decimal runningBalance = await _context.SupplierLedger
            .Where(s => s.SupplierId == payment.SupplierId && s.StoreId == payment.StoreId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.RunningBalance)
            .FirstOrDefaultAsync(cancellationToken);

        runningBalance -= payment.Amount; // Debit decreases accounts payable liability

        var ledgerEntry = new SupplierLedgerEntry
        {
            StoreId = payment.StoreId,
            SupplierId = payment.SupplierId,
            EntryDate = payment.PaymentDate.Date,
            TransactionType = "PAYMENT",
            ReferenceNumber = payment.PaymentNumber,
            DebitAmount = payment.Amount,
            CreditAmount = 0,
            RunningBalance = runningBalance,
            Description = $"Payment {payment.PaymentNumber} posted",
            JournalEntryId = jeId,
            CreatedAt = DateTime.UtcNow
        };
        _context.SupplierLedger.Add(ledgerEntry);

        return jeId;
    }

    public async Task<List<SupplierLedgerDto>> Handle(GetSupplierLedgerQuery request, CancellationToken cancellationToken)
    {
        return await _context.SupplierLedger
            .AsNoTracking()
            .Where(s => s.SupplierId == request.SupplierId && s.StoreId == request.StoreId)
            .OrderBy(s => s.EntryDate)
            .ThenBy(s => s.CreatedAt)
            .Select(s => new SupplierLedgerDto
            {
                Id = s.Id,
                EntryDate = s.EntryDate,
                TransactionType = s.TransactionType,
                ReferenceNumber = s.ReferenceNumber,
                DebitAmount = s.DebitAmount,
                CreditAmount = s.CreditAmount,
                RunningBalance = s.RunningBalance,
                Description = s.Description,
                JournalEntryId = s.JournalEntryId
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<SupplierAgingDto>> Handle(GetSupplierAgingReportQuery request, CancellationToken cancellationToken)
    {
        var activeBills = await (from b in _context.PurchaseBills
                                 join s in _context.Suppliers on b.SupplierId equals s.Id
                                 where b.StoreId == request.StoreId && b.Status != "PAID"
                                 select new { Bill = b, SupplierName = s.Name })
                                 .AsNoTracking()
                                 .ToListAsync(cancellationToken);

        var supplierMap = new Dictionary<Guid, SupplierAgingDto>();

        foreach (var item in activeBills)
        {
            var bill = item.Bill;
            var supplierName = item.SupplierName;

            // Calculate allocated amount
            decimal allocated = await _context.SupplierPaymentAllocations
                .Where(a => a.PurchaseBillId == bill.Id)
                .SumAsync(a => a.AllocatedAmount, cancellationToken);

            decimal outstanding = bill.TotalAmount - allocated;
            if (outstanding <= 0) continue;

            if (!supplierMap.TryGetValue(bill.SupplierId, out var dto))
            {
                dto = new SupplierAgingDto
                {
                    SupplierId = bill.SupplierId,
                    SupplierName = supplierName,
                    TotalOutstanding = 0,
                    Current = 0,
                    Overdue1To30 = 0,
                    Overdue31To60 = 0,
                    Overdue61To90 = 0,
                    Overdue90Plus = 0
                };
                supplierMap[bill.SupplierId] = dto;
            }

            dto.TotalOutstanding += outstanding;

            // Calculate overdue days relative to DueDate
            DateTime dueDate = bill.DueDate ?? bill.BillDate.AddDays(30);
            int overdueDays = (request.AsOfDate.Date - dueDate.Date).Days;

            if (overdueDays <= 0)
            {
                dto.Current += outstanding;
            }
            else if (overdueDays <= 30)
            {
                dto.Overdue1To30 += outstanding;
            }
            else if (overdueDays <= 60)
            {
                dto.Overdue31To60 += outstanding;
            }
            else if (overdueDays <= 90)
            {
                dto.Overdue61To90 += outstanding;
            }
            else
            {
                dto.Overdue90Plus += outstanding;
            }
        }

        return supplierMap.Values.ToList();
    }

    public async Task<List<PurchaseBillDto>> Handle(GetPurchaseBillsQuery request, CancellationToken cancellationToken)
    {
        return await (from b in _context.PurchaseBills
                      join s in _context.Suppliers on b.SupplierId equals s.Id
                      where b.StoreId == request.StoreId
                      orderby b.BillDate descending, b.CreatedAt descending
                      select new PurchaseBillDto
                      {
                          Id = b.Id,
                          StoreId = b.StoreId,
                          SupplierId = b.SupplierId,
                          SupplierName = s.Name,
                          GRNHeaderId = b.GRNHeaderId,
                          BillNumber = b.BillNumber,
                          BillDate = b.BillDate,
                          SubTotal = b.SubTotal,
                          TaxAmount = b.TaxAmount,
                          TotalAmount = b.TotalAmount,
                          Status = b.Status,
                          DueDate = b.DueDate,
                          CreatedAt = b.CreatedAt
                      })
                      .AsNoTracking()
                      .ToListAsync(cancellationToken);
    }

    public async Task<List<SupplierPaymentDto>> Handle(GetSupplierPaymentsQuery request, CancellationToken cancellationToken)
    {
        return await (from p in _context.SupplierPayments
                      join s in _context.Suppliers on p.SupplierId equals s.Id
                      where p.StoreId == request.StoreId
                      orderby p.PaymentDate descending, p.CreatedAt descending
                      select new SupplierPaymentDto
                      {
                          Id = p.Id,
                          StoreId = p.StoreId,
                          SupplierId = p.SupplierId,
                          SupplierName = s.Name,
                          PaymentDate = p.PaymentDate,
                          PaymentNumber = p.PaymentNumber,
                          PaymentMode = p.PaymentMode,
                          ReferenceNumber = p.ReferenceNumber,
                          Amount = p.Amount,
                          JournalEntryId = p.JournalEntryId,
                          Status = p.Status,
                          Notes = p.Notes,
                          CreatedAt = p.CreatedAt
                      })
                      .AsNoTracking()
                      .ToListAsync(cancellationToken);
    }

    private async Task<string> ResolveAccountCodeAsync(string accountType, string namePattern, string fallbackCode, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .Where(a => a.IsActive && a.AccountType == accountType)
            .OrderByDescending(a => a.AccountCode.Length)
            .ThenBy(a => a.AccountCode)
            .ToListAsync(cancellationToken);

        var matched = account.FirstOrDefault(a => a.Name.Contains(namePattern, StringComparison.OrdinalIgnoreCase))
                   ?? account.FirstOrDefault(a => a.AccountCode == fallbackCode);

        return matched?.AccountCode ?? fallbackCode;
    }
}
