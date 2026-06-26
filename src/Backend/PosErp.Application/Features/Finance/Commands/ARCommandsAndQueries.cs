using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Finance.Services;
using PosErp.Domain.Entities.Finance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Finance.Commands;

public record ProcessCustomerReceiptCommand(
    Guid StoreId,
    Guid CustomerId,
    DateTime ReceiptDate,
    string PaymentMode,
    string? ReferenceNumber,
    decimal Amount,
    string? Notes,
    string AllocationMode, // AUTO_FIFO, MANUAL
    List<ManualAllocationInputDto>? ManualAllocations,
    Guid UserId
) : IRequest<Guid>;

public record GetCustomerLedgerQuery(Guid CustomerId, Guid StoreId) : IRequest<List<CustomerLedgerDto>>;

public class CustomerLedgerDto
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

public record GetCustomerAgingReportQuery(Guid StoreId, DateTime AsOfDate) : IRequest<List<CustomerAgingDto>>;

public class CustomerAgingDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalOutstanding { get; set; }
    public decimal Current { get; set; }
    public decimal Overdue1To30 { get; set; }
    public decimal Overdue31To60 { get; set; }
    public decimal Overdue61To90 { get; set; }
    public decimal Overdue90Plus { get; set; }
}

public record GetCustomerReceiptsQuery(Guid StoreId) : IRequest<List<CustomerReceiptDto>>;

public class CustomerReceiptDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public string PaymentMode { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public decimal Amount { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public record GetCreditMonitoringQuery(Guid StoreId) : IRequest<List<CreditMonitoringDto>>;

public class CreditMonitoringDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public decimal Outstanding { get; set; }
    public decimal AvailableCredit { get; set; }
    public decimal UtilizationPercentage { get; set; }
    public int OverdueDays { get; set; }
    public string RiskLevel { get; set; } = "LOW";
    public DateTime? LastPaymentDate { get; set; }
    public string Status { get; set; } = "Active";
}

public class ARCommandsAndQueriesHandler :
    IRequestHandler<ProcessCustomerReceiptCommand, Guid>,
    IRequestHandler<GetCustomerLedgerQuery, List<CustomerLedgerDto>>,
    IRequestHandler<GetCustomerAgingReportQuery, List<CustomerAgingDto>>,
    IRequestHandler<GetCustomerReceiptsQuery, List<CustomerReceiptDto>>,
    IRequestHandler<GetCreditMonitoringQuery, List<CreditMonitoringDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IFinancialPostingService _postingService;
    private readonly IDocumentSequenceService _sequenceService;
    private readonly IAllocationEngine _allocationEngine;

    public ARCommandsAndQueriesHandler(
        IApplicationDbContext context,
        IFinancialPostingService postingService,
        IDocumentSequenceService sequenceService,
        IAllocationEngine allocationEngine)
    {
        _context = context;
        _postingService = postingService;
        _sequenceService = sequenceService;
        _allocationEngine = allocationEngine;
    }

    public async Task<Guid> Handle(ProcessCustomerReceiptCommand request, CancellationToken cancellationToken)
    {
        using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var customer = await _context.Customers.FindAsync(new object[] { request.CustomerId }, cancellationToken);
            if (customer == null) throw new InvalidOperationException("Customer not found.");

            // Generate sequence number for receipt
            string recNumber = await _sequenceService.GenerateNextNumberAsync(request.StoreId, "CUSTOMER_RECEIPT", cancellationToken);

            var receipt = new CustomerReceipt
            {
                StoreId = request.StoreId,
                CustomerId = request.CustomerId,
                ReceiptDate = request.ReceiptDate.Date,
                ReceiptNumber = recNumber,
                PaymentMode = request.PaymentMode,
                ReferenceNumber = request.ReferenceNumber,
                Amount = request.Amount,
                Status = "POSTED",
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            };

            _context.CustomerReceipts.Add(receipt);
            await _context.SaveChangesAsync(cancellationToken);

            // Post double-entry journal entry:
            // Debit Bank Account 10200 (or Cash)
            // Credit Customer Ledger Entry 21100 (Deposits / Accounts Receivable)
            // Note: AR standard GL account is typically 20000 / 20200 (Receivables / Deposits).
            // Let's use 20200 'Customer Wallet Liabilities' or similar, or define a general customer receivable GL code 20000.
            // Looking at COA: ('20000', 'Current Liabilities'), ('2100', 'Customer Wallet Deposits') (Wait, in seed it was 2100, and in 12_Seed it is 20200 'Customer Wallet Liabilities')
            // Let's use '20200' as Customer Receivable/Deposits.
            string digitalAccountCode = await ResolveAccountCodeAsync("ASSET", "Current", "10200", cancellationToken);
            string arAccountCode = await ResolveAccountCodeAsync("LIABILITY", "Wallet", "20200", cancellationToken);

            var lines = new List<JournalLineDto>
            {
                new() { AccountCode = digitalAccountCode, Description = $"Customer receipt {recNumber}", Debit = request.Amount, Credit = 0 },
                new() { AccountCode = arAccountCode, Description = $"Customer account credit {customer.Name}", Debit = 0, Credit = request.Amount }
            };

            Guid jeId = await _postingService.PostJournalEntryWithUserAsync(
                request.StoreId,
                request.ReceiptDate,
                $"Customer receipt {recNumber} from {customer.Name}",
                recNumber,
                lines,
                request.UserId,
                isDraft: false,
                cancellationToken,
                sourceModule: "FINANCE",
                sourceDocType: "CUSTOMER_RECEIPT",
                sourceDocId: receipt.Id
            );

            receipt.JournalEntryId = jeId;

            // Update customer sub-ledger (Credit customer, reduces outstanding receivables)
            decimal runningBalance = await _context.CustomerLedger
                .Where(c => c.CustomerId == request.CustomerId && c.StoreId == request.StoreId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => c.RunningBalance)
                .FirstOrDefaultAsync(cancellationToken);

            runningBalance -= request.Amount; // Credit decreases customer receivable balance

            var ledgerEntry = new CustomerLedgerEntry
            {
                StoreId = request.StoreId,
                CustomerId = request.CustomerId,
                EntryDate = request.ReceiptDate.Date,
                TransactionType = "RECEIPT",
                ReferenceNumber = recNumber,
                DebitAmount = 0,
                CreditAmount = request.Amount,
                RunningBalance = runningBalance,
                Description = $"Receipt {recNumber} posted",
                JournalEntryId = jeId,
                CreatedAt = DateTime.UtcNow
            };
            _context.CustomerLedger.Add(ledgerEntry);

            // Update customer wallet balance if applicable
            customer.RunningWalletBalance += request.Amount;

            await _context.SaveChangesAsync(cancellationToken);

            // Run allocations
            await _allocationEngine.AllocateCustomerReceiptAsync(receipt.Id, request.AllocationMode, request.ManualAllocations, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return receipt.Id;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<List<CustomerLedgerDto>> Handle(GetCustomerLedgerQuery request, CancellationToken cancellationToken)
    {
        return await _context.CustomerLedger
            .AsNoTracking()
            .Where(c => c.CustomerId == request.CustomerId && c.StoreId == request.StoreId)
            .OrderBy(c => c.EntryDate)
            .ThenBy(c => c.CreatedAt)
            .Select(c => new CustomerLedgerDto
            {
                Id = c.Id,
                EntryDate = c.EntryDate,
                TransactionType = c.TransactionType,
                ReferenceNumber = c.ReferenceNumber,
                DebitAmount = c.DebitAmount,
                CreditAmount = c.CreditAmount,
                RunningBalance = c.RunningBalance,
                Description = c.Description,
                JournalEntryId = c.JournalEntryId
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CustomerAgingDto>> Handle(GetCustomerAgingReportQuery request, CancellationToken cancellationToken)
    {
        var activeInvoices = await (from i in _context.Invoices
                                    join c in _context.Customers on i.CustomerId equals c.Id
                                    where i.StoreId == request.StoreId && i.Status != "PAID" && i.Status != "CANCELLED" && i.PaymentMode == "CREDIT"
                                    select new { Invoice = i, CustomerName = c.Name, CustomerId = c.Id })
                                    .AsNoTracking()
                                    .ToListAsync(cancellationToken);

        var customerMap = new Dictionary<Guid, CustomerAgingDto>();

        foreach (var item in activeInvoices)
        {
            var inv = item.Invoice;

            // Calculate allocated amount
            decimal allocated = await _context.CustomerReceiptAllocations
                .Where(a => a.InvoiceId == inv.Id)
                .SumAsync(a => a.AllocatedAmount, cancellationToken);

            decimal outstanding = inv.NetPayable - allocated;
            if (outstanding <= 0) continue;

            if (!customerMap.TryGetValue(item.CustomerId, out var dto))
            {
                dto = new CustomerAgingDto
                {
                    CustomerId = item.CustomerId,
                    CustomerName = item.CustomerName,
                    TotalOutstanding = 0,
                    Current = 0,
                    Overdue1To30 = 0,
                    Overdue31To60 = 0,
                    Overdue61To90 = 0,
                    Overdue90Plus = 0
                };
                customerMap[item.CustomerId] = dto;
            }

            dto.TotalOutstanding += outstanding;

            // Calculate overdue days relative to DueDate
            DateTime dueDate = inv.DueDate ?? inv.BusinessDate.AddDays(30);
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

        return customerMap.Values.ToList();
    }

    public async Task<List<CustomerReceiptDto>> Handle(GetCustomerReceiptsQuery request, CancellationToken cancellationToken)
    {
        return await (from r in _context.CustomerReceipts
                      join c in _context.Customers on r.CustomerId equals c.Id
                      where r.StoreId == request.StoreId
                      orderby r.ReceiptDate descending, r.CreatedAt descending
                      select new CustomerReceiptDto
                      {
                          Id = r.Id,
                          StoreId = r.StoreId,
                          CustomerId = r.CustomerId,
                          CustomerName = c.Name,
                          ReceiptDate = r.ReceiptDate,
                          ReceiptNumber = r.ReceiptNumber,
                          PaymentMode = r.PaymentMode,
                          ReferenceNumber = r.ReferenceNumber,
                          Amount = r.Amount,
                          JournalEntryId = r.JournalEntryId,
                          Status = r.Status,
                          Notes = r.Notes,
                          CreatedAt = r.CreatedAt
                      })
                      .AsNoTracking()
                      .ToListAsync(cancellationToken);
    }

    public async Task<List<CreditMonitoringDto>> Handle(GetCreditMonitoringQuery request, CancellationToken cancellationToken)
    {
        var customers = await _context.Customers
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var list = new List<CreditMonitoringDto>();

        foreach (var customer in customers)
        {
            // Outstanding from CustomerLedger
            decimal outstanding = await _context.CustomerLedger
                .Where(c => c.CustomerId == customer.Id && c.StoreId == request.StoreId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => c.RunningBalance)
                .FirstOrDefaultAsync(cancellationToken);

            decimal availableCredit = Math.Max(0, customer.CreditLimit - outstanding);
            decimal utilization = customer.CreditLimit > 0 ? (outstanding / customer.CreditLimit) * 100 : 0;

            // Risk Level
            string risk = "LOW";
            if (outstanding > customer.CreditLimit && customer.CreditLimit > 0) risk = "EXCEEDED";
            else if (utilization > 80) risk = "HIGH";
            else if (utilization > 50) risk = "MEDIUM";

            // Overdue Days from unpaid credit invoices
            int maxOverdue = 0;
            var unpaidInvoices = await _context.Invoices
                .Where(i => i.CustomerId == customer.Id && i.StoreId == request.StoreId && i.PaymentMode == "CREDIT" && i.Status != "PAID" && i.Status != "CANCELLED")
                .Select(i => new { i.BusinessDate, i.DueDate })
                .ToListAsync(cancellationToken);

            foreach (var inv in unpaidInvoices)
            {
                DateTime dueDate = inv.DueDate ?? inv.BusinessDate.AddDays(30);
                if (DateTime.Today > dueDate)
                {
                    int diff = (DateTime.Today - dueDate).Days;
                    if (diff > maxOverdue) maxOverdue = diff;
                }
            }

            // Last Payment Date
            DateTime? lastPayDate = await _context.CustomerReceipts
                .Where(r => r.CustomerId == customer.Id && r.StoreId == request.StoreId && r.Status == "POSTED")
                .OrderByDescending(r => r.ReceiptDate)
                .Select(r => (DateTime?)r.ReceiptDate)
                .FirstOrDefaultAsync(cancellationToken);

            list.Add(new CreditMonitoringDto
            {
                CustomerId = customer.Id,
                CustomerName = customer.Name,
                CreditLimit = customer.CreditLimit,
                Outstanding = outstanding,
                AvailableCredit = availableCredit,
                UtilizationPercentage = Math.Round(utilization, 2),
                OverdueDays = maxOverdue,
                RiskLevel = risk,
                LastPaymentDate = lastPayDate,
                Status = customer.MembershipStatus == "Blocked" ? "Blocked" : "Active"
            });
        }

        return list;
    }

    private async Task<string> ResolveAccountCodeAsync(string accountType, string namePattern, string fallbackCode, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .Where(a => a.IsActive && a.AccountType == accountType)
            .ToListAsync(cancellationToken);

        var matched = account.FirstOrDefault(a => a.Name.Contains(namePattern, StringComparison.OrdinalIgnoreCase))
                   ?? account.FirstOrDefault(a => a.AccountCode == fallbackCode);

        return matched?.AccountCode ?? fallbackCode;
    }
}
