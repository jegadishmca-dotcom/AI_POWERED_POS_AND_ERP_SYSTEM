using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Finance.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Pos.Commands;

public record ClosePosSessionCommand(Guid SessionId, decimal ActualClosingCash) : IRequest<bool>;

public class ClosePosSessionCommandHandler : IRequestHandler<ClosePosSessionCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IFinancialPostingService _financialPostingService;

    public ClosePosSessionCommandHandler(IApplicationDbContext context, IFinancialPostingService financialPostingService)
    {
        _context = context;
        _financialPostingService = financialPostingService;
    }

    public async Task<bool> Handle(ClosePosSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.PosSessions.FindAsync(new object[] { request.SessionId }, cancellationToken);
        if (session == null || session.Status == "CLOSED") return false;

        var db = (DbContext)_context;

        // BUG-05 FIX: Wrap the entire shift-close operation in a single transaction.
        // Previously, a failure between account creation and session update could leave the
        // session permanently stuck as OPEN (non-atomic operations across SaveChangesAsync calls).
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Calculate expected cash from invoices during this session
            var endTime = DateTime.UtcNow;
            var invoices = await _context.Invoices
                .Where(i => i.TerminalId == session.TerminalId && i.CashierId == session.CashierId && i.CreatedAt >= session.StartTime && i.CreatedAt <= endTime)
                .ToListAsync(cancellationToken);

            // H1 fix: sum CashAmount directly from all invoices in this session.
            // Previously filtered by PaymentMode == "CASH" which excluded split-payment invoices
            // (e.g. Cash+UPI marked as "SPLIT") — causing ExpectedClosingCash to be understated.
            decimal totalCashSales = invoices.Sum(i => i.CashAmount);
            
            session.ExpectedClosingCash = session.OpeningFloatCash + totalCashSales;
            session.ActualClosingCash = request.ActualClosingCash;
            session.Difference = session.ActualClosingCash - session.ExpectedClosingCash;
            session.EndTime = endTime;
            session.Status = "CLOSED";

            // Post discrepancy to journal if Difference != 0
            if (session.Difference != 0)
            {
                var journalLines = new List<JournalLineDto>();
                if (session.Difference > 0)
                {
                    // Cash Over
                    journalLines.Add(new JournalLineDto { AccountCode = "1000", Description = "Cash Drawer Overage", Debit = session.Difference, Credit = 0 });
                    journalLines.Add(new JournalLineDto { AccountCode = "4200", Description = "Other Income (Cash Over)", Debit = 0, Credit = session.Difference });
                }
                else
                {
                    // Cash Short
                    decimal shortage = Math.Abs(session.Difference);
                    journalLines.Add(new JournalLineDto { AccountCode = "5200", Description = "Cash Drawer Shortage", Debit = shortage, Credit = 0 });
                    journalLines.Add(new JournalLineDto { AccountCode = "1000", Description = "Cash Drawer Shortage", Debit = 0, Credit = shortage });
                }

                // Ensure discrepancy accounts exist in Chart of Accounts (within the same transaction)
                await EnsureAccountExistsAsync("1000", "Cash on Hand", "ASSET", cancellationToken);
                await EnsureAccountExistsAsync("4200", "Cash Drawer Overage (Other Income)", "REVENUE", cancellationToken);
                await EnsureAccountExistsAsync("5200", "Cash Drawer Shortage (Expense)", "EXPENSE", cancellationToken);

                await _financialPostingService.PostJournalEntryAsync(null, endTime.Date, $"Cash Discrepancy Session {session.Id}", $"SES-{session.Id}", journalLines, cancellationToken);
            }

            // Commit all changes (session status + accounts + journal) atomically
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task EnsureAccountExistsAsync(string code, string name, string type, CancellationToken cancellationToken)
    {
        var exists = await _context.Accounts.AnyAsync(a => a.AccountCode == code, cancellationToken);
        if (!exists)
        {
            _context.Accounts.Add(new PosErp.Domain.Entities.Finance.Account
            {
                Id = Guid.NewGuid(),
                AccountCode = code,
                Name = name,
                AccountType = type,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
