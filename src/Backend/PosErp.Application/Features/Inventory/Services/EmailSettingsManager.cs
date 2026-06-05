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
                cmd.CommandText = "SELECT smtp_server, smtp_port, sender_email, sender_password, recipient_email, enable_ssl, trigger_interval_minutes FROM email_settings WHERE id = 'global'";
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
                    INSERT INTO email_settings (id, smtp_server, smtp_port, sender_email, sender_password, recipient_email, enable_ssl, trigger_interval_minutes)
                    VALUES ('global', @smtp_server, @smtp_port, @sender_email, @sender_password, @recipient_email, @enable_ssl, @trigger_interval_minutes)
                    ON CONFLICT (id) DO UPDATE SET
                        smtp_server = EXCLUDED.smtp_server,
                        smtp_port = EXCLUDED.smtp_port,
                        sender_email = EXCLUDED.sender_email,
                        sender_password = EXCLUDED.sender_password,
                        recipient_email = EXCLUDED.recipient_email,
                        enable_ssl = EXCLUDED.enable_ssl,
                        trigger_interval_minutes = EXCLUDED.trigger_interval_minutes;";

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
