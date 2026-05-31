using Microsoft.Extensions.Configuration;
using PosErp.Application.Interfaces;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace PosErp.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public SmtpEmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        try
        {
            var savedSettings = PosErp.Application.Features.Inventory.Services.EmailSettingsManager.GetSettings();

            var smtpServer = !string.IsNullOrWhiteSpace(savedSettings.SmtpServer)
                ? savedSettings.SmtpServer
                : (_configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com");

            var senderEmail = !string.IsNullOrWhiteSpace(savedSettings.SenderEmail)
                ? savedSettings.SenderEmail
                : _configuration["EmailSettings:SenderEmail"];

            var senderPassword = !string.IsNullOrWhiteSpace(savedSettings.SenderPassword)
                ? savedSettings.SenderPassword
                : _configuration["EmailSettings:SenderPassword"];

            int smtpPort = 587;
            if (savedSettings.SmtpPort > 0)
            {
                smtpPort = savedSettings.SmtpPort;
            }
            else
            {
                var smtpPortStr = _configuration["EmailSettings:SmtpPort"] ?? "587";
                int.TryParse(smtpPortStr, out smtpPort);
            }

            if (smtpPort <= 0)
            {
                smtpPort = 587;
            }

            var enableSsl = savedSettings.EnableSsl;

            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword))
            {
                Console.WriteLine("[SmtpEmailService] [WARNING] SMTP credentials are not configured. Email notification skipped.");
                return;
            }

            using var mailMessage = new MailMessage();
            mailMessage.From = new MailAddress(senderEmail, "Apple Supermarket ERP");
            mailMessage.To.Add(to);
            mailMessage.Subject = subject;
            mailMessage.Body = htmlBody;
            mailMessage.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(smtpServer, smtpPort);
            smtpClient.EnableSsl = enableSsl;
            smtpClient.UseDefaultCredentials = false;
            smtpClient.Credentials = new NetworkCredential(senderEmail, senderPassword);

            await smtpClient.SendMailAsync(mailMessage);
            Console.WriteLine($"[SmtpEmailService] Email successfully sent to {to}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SmtpEmailService] [ERROR] Failed to send email: {ex.Message}");
            // Catch all exceptions to prevent background worker crashing
        }
    }
}
