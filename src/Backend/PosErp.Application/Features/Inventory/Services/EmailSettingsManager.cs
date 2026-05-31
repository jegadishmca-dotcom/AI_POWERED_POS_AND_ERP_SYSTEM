using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PosErp.Application.Features.Inventory.Services;

public class EmailSettings
{
    public string SmtpServer { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SenderEmail { get; set; } = "";
    public string SenderPassword { get; set; } = "";
    public string RecipientEmail { get; set; } = "jegadishmca@gmail.com";
    public bool EnableSsl { get; set; } = true;
}

public static class EmailSettingsManager
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "email_settings.json");
    private static readonly object LockObj = new();

    private static readonly string PepperKey = "AppPosErp_SecretPepperSmtp2026_"; // 32 chars key for AES-256
    private static readonly byte[] StaticIv = new byte[16] { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };

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
            // If it is not a valid Base64 string, return it as-is (e.g. if previously saved in plain text)
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

    public static EmailSettings GetSettings()
    {
        lock (LockObj)
        {
            if (!File.Exists(FilePath))
            {
                var defaultSettings = new EmailSettings();
                SaveSettings(defaultSettings);
                return defaultSettings;
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<EmailSettings>(json) ?? new EmailSettings();
                if (!string.IsNullOrEmpty(settings.SenderPassword))
                {
                    settings.SenderPassword = Decrypt(settings.SenderPassword);
                }
                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading email settings, using defaults: {ex.Message}");
                return new EmailSettings();
            }
        }
    }

    public static void SaveSettings(EmailSettings settings)
    {
        lock (LockObj)
        {
            try
            {
                var clone = new EmailSettings
                {
                    SmtpServer = settings.SmtpServer,
                    SmtpPort = settings.SmtpPort,
                    SenderEmail = settings.SenderEmail,
                    SenderPassword = Encrypt(settings.SenderPassword),
                    RecipientEmail = settings.RecipientEmail,
                    EnableSsl = settings.EnableSsl
                };

                string json = JsonSerializer.Serialize(clone, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save email settings: {ex.Message}");
            }
        }
    }
}
