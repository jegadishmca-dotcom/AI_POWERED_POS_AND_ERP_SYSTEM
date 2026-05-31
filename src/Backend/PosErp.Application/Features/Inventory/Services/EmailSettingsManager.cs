using System;
using System.IO;
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
                return JsonSerializer.Deserialize<EmailSettings>(json) ?? new EmailSettings();
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
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save email settings: {ex.Message}");
            }
        }
    }
}
