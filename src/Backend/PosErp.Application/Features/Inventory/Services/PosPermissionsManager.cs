using System;
using System.IO;
using System.Text.Json;

namespace PosErp.Application.Features.Inventory.Services;

/// <summary>
/// POS-level permission flags controlled by the store Owner/Manager.
/// Stored as a JSON file (same pattern as InventoryRulesManager) — no DB migration required.
/// </summary>
public class PosPermissions
{
    /// <summary>
    /// When true, a cashier can delete a line item from the active billing cart
    /// without a Manager Override PIN.
    /// 
    /// AUDIT NOTE: Every deletion performed under this permission is written to
    /// the audit log (AuditLogs table) by the frontend even when no PIN is
    /// required, preserving an audit trail despite the relaxed control.
    /// </summary>
    public bool CashierCanDeleteLineItem { get; set; } = false;
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
