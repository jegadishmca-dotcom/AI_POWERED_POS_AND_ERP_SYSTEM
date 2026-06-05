using Microsoft.Extensions.Configuration;
using PosErp.Application.Interfaces;
using PosErp.Application.Features.Inventory.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace PosErp.Infrastructure.Services;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly IEmailSettingsManager _emailSettingsManager;
    private static readonly HttpClient _httpClient = new HttpClient();

    public SmtpEmailService(IConfiguration configuration, IEmailSettingsManager emailSettingsManager)
    {
        _configuration = configuration;
        _emailSettingsManager = emailSettingsManager;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        try
        {
            var savedSettings = _emailSettingsManager.GetSettings();
            var deliveryMethod = savedSettings.DeliveryMethod?.ToUpper() ?? "POSTMARK";

            if (deliveryMethod == "POSTMARK")
            {
                await SendViaPostmarkAsync(savedSettings, to, subject, htmlBody);
            }
            else if (deliveryMethod == "MAILGUN")
            {
                await SendViaMailgunAsync(savedSettings, to, subject, htmlBody);
            }
            else
            {
                await SendViaSmtpAsync(savedSettings, to, subject, htmlBody);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SmtpEmailService] [ERROR] Failed to send email: {ex.Message}");
            throw; // Propagate up to SettingsController connection test so the test error shows in UI
        }
    }

    private async Task SendViaPostmarkAsync(EmailSettings settings, string to, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(settings.PostmarkToken))
        {
            throw new InvalidOperationException("Postmark Server Token is not configured.");
        }
        if (string.IsNullOrWhiteSpace(settings.SenderEmail))
        {
            throw new InvalidOperationException("Sender email account is not configured.");
        }

        var payload = new
        {
            From = settings.SenderEmail,
            To = to,
            Subject = subject,
            HtmlBody = htmlBody
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.postmarkapp.com/email");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Postmark-Server-Token", settings.PostmarkToken);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Postmark HTTP API returned status {response.StatusCode}: {errContent}");
        }
        Console.WriteLine($"[SmtpEmailService] Email successfully sent to {to} via Postmark HTTP API.");
    }

    private async Task SendViaMailgunAsync(EmailSettings settings, string to, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(settings.MailgunApiKey))
        {
            throw new InvalidOperationException("Mailgun API Key is not configured.");
        }
        if (string.IsNullOrWhiteSpace(settings.MailgunDomain))
        {
            throw new InvalidOperationException("Mailgun Domain is not configured.");
        }
        if (string.IsNullOrWhiteSpace(settings.SenderEmail))
        {
            throw new InvalidOperationException("Sender email account is not configured.");
        }

        var domain = settings.MailgunDomain.Trim();
        var url = $"https://api.mailgun.net/v3/{domain}/messages";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        
        // Basic authentication: api:api_key
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"api:{settings.MailgunApiKey}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

        var values = new Dictionary<string, string>
        {
            { "from", settings.SenderEmail },
            { "to", to },
            { "subject", subject },
            { "html", htmlBody }
        };

        request.Content = new FormUrlEncodedContent(values);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Mailgun HTTP API returned status {response.StatusCode}: {errContent}");
        }
        Console.WriteLine($"[SmtpEmailService] Email successfully sent to {to} via Mailgun HTTP API.");
    }

    private async Task SendViaSmtpAsync(EmailSettings savedSettings, string to, string subject, string htmlBody)
    {
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
            throw new InvalidOperationException("SMTP credentials are not configured.");
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
        Console.WriteLine($"[SmtpEmailService] Email successfully sent to {to} via SMTP Server.");
    }
}
