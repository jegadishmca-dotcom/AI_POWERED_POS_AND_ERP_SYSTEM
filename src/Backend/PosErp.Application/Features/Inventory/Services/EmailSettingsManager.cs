using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Data;
using Microsoft.EntityFrameworkCore;
using PosErp.Application.Interfaces;

namespace PosErp.Application.Features.Inventory.Services;

public class EmailSettings
{
    public string SmtpServer { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SenderEmail { get; set; } = "fortabletuse999@gmail.com";
    public string SenderPassword { get; set; } = "";
    public string RecipientEmail { get; set; } = "jegadishmca@gmail.com";
    public bool EnableSsl { get; set; } = true;
    public int TriggerIntervalMinutes { get; set; } = 0;
    public string DeliveryMethod { get; set; } = "POSTMARK";
    public string MailgunDomain { get; set; } = "";
    public string MailgunApiKey { get; set; } = "";
    public string PostmarkToken { get; set; } = "";
    public string ResendApiKey { get; set; } = "";
    public int ExpiryAlertThresholdDays { get; set; } = 30; // L2 FIX: configurable threshold
    public string DeveloperAlertEmail { get; set; } = "";
}

public interface IEmailSettingsManager
{
    EmailSettings GetSettings();
    void SaveSettings(EmailSettings settings);
}

public class EmailSettingsManager : IEmailSettingsManager
{
    private readonly IApplicationDbContext _context;

    // Pad PepperKey to 32 bytes (exactly 32 characters) for AES-256
    private static readonly string PepperKey = "AppPosErp_SecretPepperSmtp2026_X";
    private static readonly byte[] StaticIv = new byte[16] { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };

    public EmailSettingsManager(IApplicationDbContext context)
    {
        _context = context;
    }

    private static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        try
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(PepperKey);
            aes.IV = StaticIv;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }
            return Convert.ToBase64String(ms.ToArray());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EncryptionError] Failed to encrypt string: {ex.Message}");
            return plainText;
        }
    }

    private static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";
        try
        {
            Span<byte> buffer = new Span<byte>(new byte[cipherText.Length]);
            if (!Convert.TryFromBase64String(cipherText, buffer, out int _))
            {
                return cipherText;
            }

            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(PepperKey);
            aes.IV = StaticIv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(Convert.FromBase64String(cipherText));
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DecryptionError] Failed to decrypt string, returning as-is: {ex.Message}");
            return cipherText;
        }
    }

    public EmailSettings GetSettings()
    {
        var settings = new EmailSettings();
        try
        {
            var dbContext = _context as DbContext;
            if (dbContext == null) return settings;

            var conn = dbContext.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT smtp_server, smtp_port, sender_email, sender_password, recipient_email, enable_ssl, trigger_interval_minutes, delivery_method, mailgun_domain, mailgun_api_key, postmark_token, resend_api_key, expiry_alert_threshold_days, developer_alert_email FROM email_settings WHERE id = 'global'";
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        settings.SmtpServer = reader.IsDBNull(0) ? "smtp.gmail.com" : reader.GetString(0);
                        settings.SmtpPort = reader.IsDBNull(1) ? 587 : reader.GetInt32(1);
                        settings.SenderEmail = reader.IsDBNull(2) ? "fortabletuse999@gmail.com" : reader.GetString(2);
                        
                        var encryptedPassword = reader.IsDBNull(3) ? "" : reader.GetString(3);
                        settings.SenderPassword = Decrypt(encryptedPassword);
                        
                        settings.RecipientEmail = reader.IsDBNull(4) ? "jegadishmca@gmail.com" : reader.GetString(4);
                        settings.EnableSsl = reader.IsDBNull(5) ? true : reader.GetBoolean(5);
                        settings.TriggerIntervalMinutes = reader.IsDBNull(6) ? 0 : reader.GetInt32(6);
                        settings.DeliveryMethod = reader.IsDBNull(7) ? "POSTMARK" : reader.GetString(7);
                        settings.MailgunDomain = reader.IsDBNull(8) ? "" : reader.GetString(8);
                        
                        var encryptedMgKey = reader.IsDBNull(9) ? "" : reader.GetString(9);
                        settings.MailgunApiKey = Decrypt(encryptedMgKey);
                        
                        var encryptedPmToken = reader.IsDBNull(10) ? "" : reader.GetString(10);
                        settings.PostmarkToken = Decrypt(encryptedPmToken);
 
                        var encryptedRsKey = reader.IsDBNull(11) ? "" : reader.GetString(11);
                        settings.ResendApiKey = Decrypt(encryptedRsKey);
 
                        settings.ExpiryAlertThresholdDays = reader.IsDBNull(12) ? 30 : reader.GetInt32(12);
                        settings.DeveloperAlertEmail = reader.IsDBNull(13) ? "" : reader.GetString(13);
                    }
                }
            }

            if (!wasOpen) conn.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailSettingsManager] Error reading settings from DB: {ex.Message}");
        }
        return settings;
    }

    public void SaveSettings(EmailSettings settings)
    {
        try
        {
            var dbContext = _context as DbContext;
            if (dbContext == null) return;

            var conn = dbContext.Database.GetDbConnection();
            var wasOpen = conn.State == ConnectionState.Open;
            if (!wasOpen) conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    INSERT INTO email_settings (id, smtp_server, smtp_port, sender_email, sender_password, recipient_email, enable_ssl, trigger_interval_minutes, delivery_method, mailgun_domain, mailgun_api_key, postmark_token, resend_api_key, expiry_alert_threshold_days, developer_alert_email)
                    VALUES ('global', @smtp_server, @smtp_port, @sender_email, @sender_password, @recipient_email, @enable_ssl, @trigger_interval_minutes, @delivery_method, @mailgun_domain, @mailgun_api_key, @postmark_token, @resend_api_key, @expiry_alert_threshold_days, @developer_alert_email)
                    ON CONFLICT (id) DO UPDATE SET
                        smtp_server = EXCLUDED.smtp_server,
                        smtp_port = EXCLUDED.smtp_port,
                        sender_email = EXCLUDED.sender_email,
                        sender_password = EXCLUDED.sender_password,
                        recipient_email = EXCLUDED.recipient_email,
                        enable_ssl = EXCLUDED.enable_ssl,
                        trigger_interval_minutes = EXCLUDED.trigger_interval_minutes,
                        delivery_method = EXCLUDED.delivery_method,
                        mailgun_domain = EXCLUDED.mailgun_domain,
                        mailgun_api_key = EXCLUDED.mailgun_api_key,
                        postmark_token = EXCLUDED.postmark_token,
                        resend_api_key = EXCLUDED.resend_api_key,
                        expiry_alert_threshold_days = EXCLUDED.expiry_alert_threshold_days,
                        developer_alert_email = EXCLUDED.developer_alert_email;";

                var pSmtpServer = cmd.CreateParameter();
                pSmtpServer.ParameterName = "@smtp_server";
                pSmtpServer.Value = settings.SmtpServer ?? "";
                cmd.Parameters.Add(pSmtpServer);

                var pSmtpPort = cmd.CreateParameter();
                pSmtpPort.ParameterName = "@smtp_port";
                pSmtpPort.Value = settings.SmtpPort;
                cmd.Parameters.Add(pSmtpPort);

                var pSenderEmail = cmd.CreateParameter();
                pSenderEmail.ParameterName = "@sender_email";
                pSenderEmail.Value = settings.SenderEmail ?? "";
                cmd.Parameters.Add(pSenderEmail);

                var pSenderPassword = cmd.CreateParameter();
                pSenderPassword.ParameterName = "@sender_password";
                pSenderPassword.Value = Encrypt(settings.SenderPassword);
                cmd.Parameters.Add(pSenderPassword);

                var pRecipientEmail = cmd.CreateParameter();
                pRecipientEmail.ParameterName = "@recipient_email";
                pRecipientEmail.Value = settings.RecipientEmail ?? "";
                cmd.Parameters.Add(pRecipientEmail);

                var pEnableSsl = cmd.CreateParameter();
                pEnableSsl.ParameterName = "@enable_ssl";
                pEnableSsl.Value = settings.EnableSsl;
                cmd.Parameters.Add(pEnableSsl);

                var pTriggerIntervalMinutes = cmd.CreateParameter();
                pTriggerIntervalMinutes.ParameterName = "@trigger_interval_minutes";
                pTriggerIntervalMinutes.Value = settings.TriggerIntervalMinutes;
                cmd.Parameters.Add(pTriggerIntervalMinutes);

                var pDeliveryMethod = cmd.CreateParameter();
                pDeliveryMethod.ParameterName = "@delivery_method";
                pDeliveryMethod.Value = settings.DeliveryMethod ?? "POSTMARK";
                cmd.Parameters.Add(pDeliveryMethod);

                var pMailgunDomain = cmd.CreateParameter();
                pMailgunDomain.ParameterName = "@mailgun_domain";
                pMailgunDomain.Value = settings.MailgunDomain ?? "";
                cmd.Parameters.Add(pMailgunDomain);

                var pMailgunApiKey = cmd.CreateParameter();
                pMailgunApiKey.ParameterName = "@mailgun_api_key";
                pMailgunApiKey.Value = Encrypt(settings.MailgunApiKey);
                cmd.Parameters.Add(pMailgunApiKey);

                var pPostmarkToken = cmd.CreateParameter();
                pPostmarkToken.ParameterName = "@postmark_token";
                pPostmarkToken.Value = Encrypt(settings.PostmarkToken);
                cmd.Parameters.Add(pPostmarkToken);

                var pResendApiKey = cmd.CreateParameter();
                pResendApiKey.ParameterName = "@resend_api_key";
                pResendApiKey.Value = Encrypt(settings.ResendApiKey);
                cmd.Parameters.Add(pResendApiKey);

                var pExpiryThreshold = cmd.CreateParameter();
                pExpiryThreshold.ParameterName = "@expiry_alert_threshold_days";
                pExpiryThreshold.Value = settings.ExpiryAlertThresholdDays;
                cmd.Parameters.Add(pExpiryThreshold);
 
                var pDeveloperEmail = cmd.CreateParameter();
                pDeveloperEmail.ParameterName = "@developer_alert_email";
                pDeveloperEmail.Value = settings.DeveloperAlertEmail ?? "";
                cmd.Parameters.Add(pDeveloperEmail);

                cmd.ExecuteNonQuery();
            }

            if (!wasOpen) conn.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EmailSettingsManager] Error saving settings to DB: {ex.Message}");
        }
    }
}
