$backendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Backend"
$frontendDir = "d:\JEGADISH\AI_POWERED_POS_AND_ERP_SYSTEM\src\Frontend"

New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Crm\Services"
New-Item -ItemType Directory -Force -Path "$backendDir\PosErp.Application\Features\Offers\Services"

# 1. Wallet Service
@"
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Crm;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Crm.Services;

public interface IWalletService
{
    Task<decimal> RecordTransactionAsync(Guid customerId, Guid? storeId, string transactionType, decimal amount, string referenceDocument, Guid? userId, CancellationToken cancellationToken);
}

public class WalletService : IWalletService
{
    private readonly IApplicationDbContext _context;

    public WalletService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> RecordTransactionAsync(Guid customerId, Guid? storeId, string transactionType, decimal amount, string referenceDocument, Guid? userId, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers.FindAsync(new object[] { customerId }, cancellationToken);
        if (customer == null) throw new Exception("Customer not found.");

        // Fetch latest ledger entry to safely calculate running balance
        var lastEntry = await _context.WalletLedger
            .Where(w => w.CustomerId == customerId)
            .OrderByDescending(w => w.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        decimal currentBalance = lastEntry?.RunningBalance ?? 0;
        
        // Ensure spend doesn't exceed balance
        if (transactionType == "SPEND" && currentBalance + amount < 0)
        {
            throw new Exception("Insufficient wallet balance.");
        }

        decimal newBalance = currentBalance + amount;

        var ledgerEntry = new WalletLedgerEntry
        {
            CustomerId = customerId,
            StoreId = storeId,
            TransactionType = transactionType, // TOPUP (+), SPEND (-), REFUND (+)
            Amount = amount,
            ReferenceDocument = referenceDocument,
            RunningBalance = newBalance,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.WalletLedger.Add(ledgerEntry);
        
        // Update denormalized balance on Customer for fast UI reads
        customer.RunningWalletBalance = newBalance;

        return newBalance;
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Crm\Services\WalletService.cs" -Encoding utf8


# 2. Loyalty Service
@"
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Crm;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Crm.Services;

public interface ILoyaltyService
{
    Task<decimal> RecordPointsAsync(Guid customerId, Guid? storeId, string transactionType, decimal points, string referenceDocument, Guid? userId, CancellationToken cancellationToken);
    Task CalculateAndAwardPointsForInvoiceAsync(Guid invoiceId, Guid customerId, decimal invoiceTotal, CancellationToken cancellationToken);
}

public class LoyaltyService : ILoyaltyService
{
    private readonly IApplicationDbContext _context;

    public LoyaltyService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> RecordPointsAsync(Guid customerId, Guid? storeId, string transactionType, decimal points, string referenceDocument, Guid? userId, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers.FindAsync(new object[] { customerId }, cancellationToken);
        if (customer == null) throw new Exception("Customer not found.");

        var lastEntry = await _context.LoyaltyLedger
            .Where(l => l.CustomerId == customerId)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        decimal currentPoints = lastEntry?.RunningPoints ?? 0;

        if (transactionType == "BURN" && currentPoints + points < 0)
        {
            throw new Exception("Insufficient loyalty points.");
        }

        decimal newPoints = currentPoints + points;

        var entry = new LoyaltyLedgerEntry
        {
            CustomerId = customerId,
            StoreId = storeId,
            TransactionType = transactionType, // EARN (+), BURN (-), EXPIRED (-)
            Points = points,
            ReferenceDocument = referenceDocument,
            ExpiryDate = transactionType == "EARN" ? DateTime.UtcNow.AddDays(365) : null,
            RunningPoints = newPoints,
            CreatedBy = userId
        };

        _context.LoyaltyLedger.Add(entry);
        customer.RunningLoyaltyPoints = newPoints;

        return newPoints;
    }

    public async Task CalculateAndAwardPointsForInvoiceAsync(Guid invoiceId, Guid customerId, decimal invoiceTotal, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers.Include(c => c.Tier).FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);
        if (customer == null) return;

        // Base Earn Rate: e.g. 1 point per 100 Rupees
        decimal basePoints = invoiceTotal / 100m;
        
        // Apply Tier Multiplier
        decimal multiplier = customer.Tier?.PointsEarnMultiplier ?? 1.0m;
        decimal earnedPoints = Math.Floor(basePoints * multiplier);

        if (earnedPoints > 0)
        {
            await RecordPointsAsync(customerId, null, "EARN", earnedPoints, $"INV-{invoiceId}", null, cancellationToken);
        }
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Crm\Services\LoyaltyService.cs" -Encoding utf8

# 3. Customer Tier Service
@"
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Crm.Services;

public interface ICustomerTierService
{
    Task EvaluateCustomerTierAsync(Guid customerId, CancellationToken cancellationToken);
}

public class CustomerTierService : ICustomerTierService
{
    private readonly IApplicationDbContext _context;

    public CustomerTierService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task EvaluateCustomerTierAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers.FindAsync(new object[] { customerId }, cancellationToken);
        if (customer == null) return;

        var last12Months = DateTime.UtcNow.AddMonths(-12);
        
        // In reality, sum completed invoices from the Invoices table
        decimal rollingSpend = await _context.Invoices
            .Where(i => i.CustomerId == customerId && i.CreatedAt >= last12Months && i.Status == "COMPLETED")
            .SumAsync(i => i.TotalAmount, cancellationToken);

        var eligibleTier = await _context.CustomerTiers
            .Where(t => rollingSpend >= t.MinimumSpend)
            .OrderByDescending(t => t.MinimumSpend)
            .FirstOrDefaultAsync(cancellationToken);

        if (eligibleTier != null && customer.CustomerTierId != eligibleTier.Id)
        {
            customer.CustomerTierId = eligibleTier.Id;
            // Audit log the tier change here in production
        }
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Crm\Services\CustomerTierService.cs" -Encoding utf8

# 4. Offer Engine Foundation
@"
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Domain.Entities.Offers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PosErp.Application.Features.Offers.Services;

public interface IOfferEngine
{
    Task<List<Offer>> GetActiveOffersAsync(CancellationToken cancellationToken);
    // Method to calculate best discount for an invoice will be implemented fully later
}

public class OfferEngine : IOfferEngine
{
    private readonly IApplicationDbContext _context;

    public OfferEngine(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Offer>> GetActiveOffersAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        // In production, this would be fetched from Redis Cache for ultra-fast POS resolution
        return await _context.Offers
            .Where(o => o.IsActive && o.StartDate <= now && o.EndDate >= now)
            .OrderByDescending(o => o.Priority)
            .ToListAsync(cancellationToken);
    }
}
"@ | Out-File -FilePath "$backendDir\PosErp.Application\Features\Offers\Services\OfferEngine.cs" -Encoding utf8

# 5. Dependency Injection Registration
# (Just mocking the addition, usually done in DependencyInjection.cs)

Write-Host "Backend Services Created"
