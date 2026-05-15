using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Jobs;

public class CheckExpiryJob
{
    private readonly IApplicationDbContext _context;

    public CheckExpiryJob(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task ExecuteAsync()
    {
        var thresholdDate = DateTime.UtcNow.AddDays(30);

        var expiringBatches = await _context.ProductBatches
            .Where(b => b.IsActive && b.ExpiryDate != null && b.ExpiryDate <= thresholdDate)
            .ToListAsync();

        foreach (var batch in expiringBatches)
        {
            // In a real app, integrate with INotificationService (Email/Push)
            Console.WriteLine($"[ALERT] Batch {batch.BatchNumber} expires on {batch.ExpiryDate}");
        }
    }
}
