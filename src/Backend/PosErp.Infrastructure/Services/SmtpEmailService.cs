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
            var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPortStr = _configuration["EmailSettings:SmtpPort"] ?? "587";
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var senderPassword = _configuration["EmailSettings:SenderPassword"];

            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword))
            {
                Console.WriteLine("[SmtpEmailService] [WARNING] SMTP credentials are not configured. Email notification skipped.");
                return;
            }

            int.TryParse(smtpPortStr, out int smtpPort);
            if (smtpPort <= 0)
            {
                smtpPort = 587;
            }

            using var mailMessage = new MailMessage();
            mailMessage.From = new MailAddress(senderEmail, "Apple Supermarket ERP");
            mailMessage.To.Add(to);
            mailMessage.Subject = subject;
            mailMessage.Body = htmlBody;
            mailMessage.IsBodyHtml = true;

            using var smtpClient = new SmtpClient(smtpServer, smtpPort);
            smtpClient.EnableSsl = true;
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
