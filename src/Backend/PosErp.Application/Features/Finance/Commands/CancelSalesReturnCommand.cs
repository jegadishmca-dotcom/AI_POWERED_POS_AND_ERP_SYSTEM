using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Finance.Services;
using PosErp.Application.Features.Inventory.Services;
using PosErp.Application.Features.Audit.Services;
using PosErp.Domain.Entities.Finance;
using PosErp.Domain.Entities.Crm;

namespace PosErp.Application.Features.Finance.Commands;

public record CancelSalesReturnCommand(Guid SalesReturnId, string? Reason = null) : IRequest<bool>;

public class CancelSalesReturnCommandHandler : IRequestHandler<CancelSalesReturnCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IStockLedgerService _stockLedgerService;
    private readonly IFinancialPostingService _postingService;
    private readonly IAuditLoggingService _auditLogger;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly IPeriodLockService _periodLockService;

    public CancelSalesReturnCommandHandler(
        IApplicationDbContext context,
        IStockLedgerService stockLedgerService,
        IFinancialPostingService postingService,
        IAuditLoggingService auditLogger,
        IPeriodLockService periodLockService,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _context = context;
        _stockLedgerService = stockLedgerService;
        _postingService = postingService;
        _auditLogger = auditLogger;
        _periodLockService = periodLockService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<bool> Handle(CancelSalesReturnCommand request, CancellationToken cancellationToken)
    {
        // 1. User Gating & Verification (Authenticated ClaimsPrincipal)
        if (_httpContextAccessor?.HttpContext == null)
        {
            throw new UnauthorizedAccessException("Unable to verify caller identity for this operation.");
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var userIdStr = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdStr)) throw new UnauthorizedAccessException("User is not authenticated.");
        var callerId = Guid.Parse(userIdStr);

        var callerUser = await _context.Users
            .Join(_context.Roles, u => u.RoleId, r => r.Id, (u, r) => new { User = u, Role = r })
            .FirstOrDefaultAsync(x => x.User.Id == callerId, cancellationToken);

        bool isAllowed = callerUser != null &&
            (callerUser.Role.Name == "Owner" || callerUser.Role.Name == "Developer" || callerUser.Role.Name == "Manager");
        if (!isAllowed)
        {
            throw new UnauthorizedAccessException("Unauthorized: User does not have Owner, Developer, or Manager privileges.");
        }

        var strategy = ((DbContext)_context).Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await ((DbContext)_context).Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // 2. Validation
                var salesReturn = await _context.SalesReturns
                    .Include(sr => sr.Items)
                    .FirstOrDefaultAsync(sr => sr.Id == request.SalesReturnId, cancellationToken);

                if (salesReturn == null) throw new InvalidOperationException("Sales return not found.");

                if (salesReturn.Status == "CANCELLED")
                    throw new InvalidOperationException("Sales return is already cancelled.");

                // Validate linked Journal Entry exists
                if (!salesReturn.JournalEntryId.HasValue)
                {
                    throw new InvalidOperationException("Cannot cancel return: no linked journal entry found, financial state inconsistent.");
                }

                var invoice = await _context.Invoices
                    .FirstOrDefaultAsync(i => i.Id == salesReturn.InvoiceId, cancellationToken);
                if (invoice == null) throw new InvalidOperationException("Original invoice not found.");

                if (!invoice.StoreId.HasValue)
                    throw new InvalidOperationException("Invoice has no store assignment.");

                Guid storeId = invoice.StoreId.Value;

                // Check period lock on the return date
                await _periodLockService.CheckPeriodLockAsync(storeId, salesReturn.ReturnDate, cancellationToken);

                // 3. Row-Level Locked Stock Reversal
                foreach (var item in salesReturn.Items)
                {
                    // Acquire row lock to serialize concurrent checkout/reversal races on this batch
                    await ((DbContext)_context).Database.ExecuteSqlRawAsync(
                        "SELECT 1 FROM product_batches WHERE id = {0} FOR UPDATE",
                        new object[] { item.BatchId },
                        cancellationToken);

                    var batch = await _context.ProductBatches.FindAsync(new object[] { item.BatchId }, cancellationToken);
                    if (batch == null) throw new InvalidOperationException($"Product batch with ID {item.BatchId} not found.");

                    // Force EF Core to reload the latest values from database after obtaining lock
                    await ((DbContext)_context).Entry(batch).ReloadAsync(cancellationToken);

                    // Revert the restock (deduct returned quantity)
                    if (batch.AvailableQuantity < item.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient stock in batch {batch.BatchNumber} to reverse return. Available: {batch.AvailableQuantity}, Required: {item.Quantity}");
                    }

                    batch.AvailableQuantity -= item.Quantity;

                    // Record negative stock movement (represents reversal of restock)
                    await _stockLedgerService.RecordMovementAsync(
                        storeId: storeId,
                        warehouseId: null,
                        terminalId: null,
                        businessDate: DateTime.UtcNow.Date,
                        productId: item.ProductId,
                        batchId: batch.Id,
                        movementType: "SALES_RETURN_CANCEL",
                        quantity: -item.Quantity,
                        unitCost: batch.CostPrice,
                        expiryDate: batch.ExpiryDate,
                        referenceDocId: salesReturn.Id,
                        referenceNumber: $"CAN-{salesReturn.ReturnNumber}",
                        userId: callerId,
                        cancellationToken: cancellationToken
                    );
                }

                // 4. Direct Journal Entry Reversal (Swapping Debits/Credits)
                var originalLines = await (from jel in _context.JournalEntryLines
                                           join acc in _context.Accounts on jel.AccountId equals acc.Id
                                           where jel.JournalEntryId == salesReturn.JournalEntryId.Value
                                           select new JournalLineDto
                                           {
                                               AccountCode = acc.AccountCode,
                                               Description = $"Reversal of: {jel.Description}",
                                               Debit = jel.CreditAmount,  // Original credit becomes debit
                                               Credit = jel.DebitAmount,  // Original debit becomes credit
                                               CostCenterId = jel.CostCenterId
                                           })
                                           .ToListAsync(cancellationToken);

                if (originalLines.Count == 0)
                {
                    throw new InvalidOperationException("Cannot cancel return: original journal entry lines not found.");
                }

                Guid cancelJeId = await _postingService.PostJournalEntryWithUserAsync(
                    storeId: storeId,
                    date: DateTime.UtcNow.Date,
                    description: $"Reversal of Sales Return {salesReturn.ReturnNumber}",
                    refDoc: $"CAN-{salesReturn.ReturnNumber}",
                    lines: originalLines,
                    userId: callerId,
                    isDraft: false,
                    cancellationToken: cancellationToken,
                    sourceModule: "AR",
                    sourceDocType: "SALES_RETURN_CANCEL",
                    sourceDocId: salesReturn.Id
                );

                // Record GST Transaction reversal (negative of the reversal, which restores original tax state)
                decimal totalSubTotal = salesReturn.SubTotal;
                decimal totalTax = salesReturn.TaxAmount;
                decimal totalAmount = salesReturn.TotalAmount;
                decimal cgstReversal = Math.Round(totalTax / 2m, 2);
                decimal sgstReversal = totalTax - cgstReversal;

                await _postingService.RecordGstTransactionAsync(
                    storeId,
                    "SALES_RETURN_CANCEL",
                    $"CAN-{salesReturn.ReturnNumber}",
                    DateTime.UtcNow.Date,
                    totalSubTotal,  // Positive restores output base
                    cgstReversal,   // Positive restores output CGST
                    sgstReversal,   // Positive restores output SGST
                    0,
                    null,
                    cancellationToken
                );

                // 5. Customer Ledger Reversal (if CREDIT_NOTE refund mode)
                if (invoice.CustomerId.HasValue && salesReturn.RefundMode == "CREDIT_NOTE")
                {
                    var customer = await _context.Customers.FindAsync(new object[] { invoice.CustomerId.Value }, cancellationToken);
                    if (customer != null)
                    {
                        decimal runningBalance = await _context.CustomerLedger
                            .Where(c => c.CustomerId == invoice.CustomerId.Value && c.StoreId == storeId)
                            .OrderByDescending(c => c.CreatedAt)
                            .Select(c => c.RunningBalance)
                            .FirstOrDefaultAsync(cancellationToken);

                        runningBalance += totalAmount; // Reversing credit note increases customer receivables outstanding (debit)

                        var ledgerEntry = new CustomerLedgerEntry
                        {
                            StoreId = storeId,
                            CustomerId = invoice.CustomerId.Value,
                            EntryDate = DateTime.UtcNow.Date,
                            TransactionType = "CREDIT_NOTE_CANCEL",
                            ReferenceNumber = $"CAN-{salesReturn.ReturnNumber}",
                            DebitAmount = totalAmount, // Debit restores balance
                            CreditAmount = 0,
                            RunningBalance = runningBalance,
                            Description = $"Reversal of Sales Return Credit Note {salesReturn.ReturnNumber}",
                            JournalEntryId = cancelJeId,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.CustomerLedger.Add(ledgerEntry);
                    }
                }

                // 6. Audit Logging
                await _auditLogger.LogActionAsync(
                    userId: callerId == Guid.Empty ? null : callerId,
                    action: "CANCEL_SALES_RETURN",
                    entityName: "SalesReturn",
                    entityId: salesReturn.Id.ToString(),
                    oldValues: new { Status = "COMPLETED" },
                    newValues: new { Status = "CANCELLED", Reason = request.Reason ?? "Not specified" },
                    ipAddress: _httpContextAccessor?.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    cancellationToken: cancellationToken
                );

                // 7. Update Status
                salesReturn.Status = "CANCELLED";

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
