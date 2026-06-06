using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Inventory.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Jobs;

public class CheckExpiryJob
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailSettingsManager _emailSettingsManager;

    public CheckExpiryJob(IApplicationDbContext context, IEmailSettingsManager emailSettingsManager)
    {
        _context = context;
        _emailSettingsManager = emailSettingsManager;
    }

    public async Task ExecuteAsync()
    {
        var settings = _emailSettingsManager.GetSettings();
        int thresholdDays = settings.ExpiryAlertThresholdDays > 0 ? settings.ExpiryAlertThresholdDays : 10;
        var thresholdDate = DateTime.UtcNow.AddDays(thresholdDays);

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
