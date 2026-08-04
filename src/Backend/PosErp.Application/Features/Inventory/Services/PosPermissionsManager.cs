using System;
using System.IO;
using System.Text.Json;

namespace PosErp.Application.Features.Inventory.Services;

public class PosPermissions
{
    public bool CashierCanDeleteLineItem { get; set; } = false;

    /// <summary>
    /// Receipt product language mode: "secondary" (e.g. Tamil), "primary" (English), or "both" (English + Secondary).
    /// Default is "secondary" to print Tamil names on receipt by default, but fully configurable.
    /// </summary>
    public string ReceiptProductLanguage { get; set; } = "secondary";

    /// <summary>
    /// When true, automatically translates English product names into the target language during product creation.
    /// </summary>
    public bool EnableCatalogAutoTranslation { get; set; } = true;

    /// <summary>
    /// Target language for product catalog auto-translation: "ta" (Tamil), "hi" (Hindi), "ar" (Arabic), "ms" (Malay), "es" (Spanish).
    /// </summary>
    public string CatalogTargetLanguage { get; set; } = "ta";
}

/// <summary>
/// Thread-safe, in-memory-cached manager for POS permission settings.
/// Follows the same pattern as <see cref="InventoryRulesManager"/>.
/// </summary>
public static class PosPermissionsManager
{
    // PERSISTENCE FIX: Use SYSTEM_CONFIG_DIR env var so the file lands in /app/config
    // (mounted as the pos_config Docker volume) instead of AppContext.BaseDirectory
    // (/app, which is ephemeral and wiped on container rebuild).
    // Falls back to AppContext.BaseDirectory for local dev (SYSTEM_CONFIG_DIR not set).
    // Matches the pattern in InventoryRulesManager and ConnectionStringProvider.
    private static readonly string FilePath = Path.Combine(
        Environment.GetEnvironmentVariable("SYSTEM_CONFIG_DIR") ?? AppContext.BaseDirectory,
        "pos_permissions.json");
    private static readonly object LockObj = new();

    private static PosPermissions? _cachedPermissions;

    public static PosPermissions GetPermissions()
    {
        if (_cachedPermissions != null) return _cachedPermissions;

        lock (LockObj)
        {
            if (_cachedPermissions != null) return _cachedPermissions;

            if (!File.Exists(FilePath))
            {
                var defaults = new PosPermissions();
                SavePermissions(defaults);
                return defaults;
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                _cachedPermissions = JsonSerializer.Deserialize<PosPermissions>(json) ?? new PosPermissions();
                return _cachedPermissions;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading POS permissions, using defaults: {ex.Message}");
                return new PosPermissions();
            }
        }
    }

    public static void SavePermissions(PosPermissions permissions)
    {
        lock (LockObj)
        {
            try
            {
                string json = JsonSerializer.Serialize(permissions, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
                // Invalidate cache so next read picks up new values immediately
                _cachedPermissions = permissions;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save POS permissions: {ex.Message}");
            }
        }
    }
}
