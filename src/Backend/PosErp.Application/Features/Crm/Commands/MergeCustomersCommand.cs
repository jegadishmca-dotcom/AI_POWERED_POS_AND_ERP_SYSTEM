using MediatR;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Crm;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Crm.Commands;

public record MergeCustomersCommand(Guid SourceCustomerId, Guid TargetCustomerId) : IRequest<bool>;

public class MergeCustomersCommandHandler : IRequestHandler<MergeCustomersCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public MergeCustomersCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(MergeCustomersCommand request, CancellationToken cancellationToken)
    {
        var sourceCustomer = await _context.Customers.FindAsync(new object[] { request.SourceCustomerId }, cancellationToken);
        var targetCustomer = await _context.Customers.FindAsync(new object[] { request.TargetCustomerId }, cancellationToken);

        if (sourceCustomer == null || targetCustomer == null)
        {
            throw new Exception("Source or Target customer not found.");
        }

        // 1. Merge Invoices
        var invoices = await _context.Invoices.Where(i => i.CustomerId == sourceCustomer.Id).ToListAsync(cancellationToken);
        foreach (var invoice in invoices)
        {
            invoice.CustomerId = targetCustomer.Id;
        }

        // 2. Merge Loyalty Ledger
        var loyaltyEntries = await _context.LoyaltyLedger.Where(l => l.CustomerId == sourceCustomer.Id).ToListAsync(cancellationToken);
        foreach (var entry in loyaltyEntries)
        {
            entry.CustomerId = targetCustomer.Id;
        }

        // 3. Add Merge Audit Event in Ledger
        if (sourceCustomer.RunningLoyaltyPoints > 0)
        {
            var targetPrevBalance = targetCustomer.RunningLoyaltyPoints;
            targetCustomer.RunningLoyaltyPoints += sourceCustomer.RunningLoyaltyPoints;
            targetCustomer.LifetimePointsEarned += sourceCustomer.LifetimePointsEarned;
            targetCustomer.LifetimeSpend += sourceCustomer.LifetimeSpend;
            
            // Adjust Target Customer Tier if Spend threshold crossed
            // Normally handled by a service, but inline for now
            
            _context.LoyaltyLedger.Add(new LoyaltyLedgerEntry
            {
                CustomerId = targetCustomer.Id,
                TransactionType = "Account Merge",
                PointsEarned = sourceCustomer.RunningLoyaltyPoints,
                PointsRedeemed = 0,
                PreviousBalance = targetPrevBalance,
                BalanceAfterTransaction = targetCustomer.RunningLoyaltyPoints,
                Remarks = $"Merged points from Source Customer {sourceCustomer.Id}",
                CreatedBy = null
            });
        }

        // 4. Mark Source Customer as Inactive and append Merge Note
        sourceCustomer.MembershipStatus = "Merged";
        sourceCustomer.CustomerSegment = "Merged";
        sourceCustomer.RunningLoyaltyPoints = 0;
        sourceCustomer.Name = sourceCustomer.Name + " (Merged)";
        
        await ((DbContext)_context).SaveChangesAsync(cancellationToken);
        return true;
    }
}
