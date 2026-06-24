using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PosErp.Application.Interfaces;

namespace PosErp.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public Task SendSmsAsync(string phoneNumber, string message)
    {
        _logger.LogInformation("Simulating SMS to {PhoneNumber}: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }

    public Task SendWhatsAppAsync(string phoneNumber, string templateName, object parameters)
    {
        _logger.LogInformation("Simulating WhatsApp to {PhoneNumber} using template {TemplateName}", phoneNumber, templateName);
        return Task.CompletedTask;
    }

    public Task SendPushNotificationAsync(Guid customerId, string title, string message)
    {
        _logger.LogInformation("Simulating Push Notification to Customer {CustomerId}: {Title} - {Message}", customerId, title, message);
        return Task.CompletedTask;
    }
}
