using System;
using System.Threading.Tasks;

namespace PosErp.Application.Interfaces;

public interface INotificationService
{
    Task SendSmsAsync(string phoneNumber, string message);
    Task SendWhatsAppAsync(string phoneNumber, string templateName, object parameters);
    Task SendPushNotificationAsync(Guid customerId, string title, string message);
}
