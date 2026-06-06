using System;
using System.IO;
using System.Text.Json;

namespace PosErp.Application.Features.Inventory.Services;

public class InventoryRules
{
    public bool PreventNegativeStock { get; set; } = false;
    public bool MandatoryBatchTracking { get; set; } = true;
    public bool RowLevelLocking { get; set; } = true;
}

public static class InventoryRulesManager
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "inventory_rules.json");
    private static readonly object LockObj = new();

    // CQ-01 FIX: Cache the rules in-memory so we don't read from disk on every stock ledger write.
    // Rules are only re-read from disk when SaveRules() is called or on first access.
    private static InventoryRules? _cachedRules;

    public static InventoryRules GetRules()
    {
        // Fast path: return cached value without locking
        if (_cachedRules != null) return _cachedRules;

        lock (LockObj)
        {
            // Double-check after acquiring lock
            if (_cachedRules != null) return _cachedRules;

            if (!File.Exists(FilePath))
            {
                var defaultRules = new InventoryRules();
                SaveRules(defaultRules);
                return defaultRules;
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                _cachedRules = JsonSerializer.Deserialize<InventoryRules>(json) ?? new InventoryRules();
                return _cachedRules;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading inventory rules, using defaults: {ex.Message}");
                return new InventoryRules();
            }
        }
    }

    public static void SaveRules(InventoryRules rules)
    {
        lock (LockObj)
        {
            try
            {
                string json = JsonSerializer.Serialize(rules, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
                // Invalidate the cache so next read picks up the new values
                _cachedRules = rules;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save inventory rules: {ex.Message}");
            }
        }
    }
}
