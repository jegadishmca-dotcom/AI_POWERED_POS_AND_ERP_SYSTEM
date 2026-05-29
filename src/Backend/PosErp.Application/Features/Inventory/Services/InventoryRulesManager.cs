using System;
using System.IO;
using System.Text.Json;

namespace PosErp.Application.Features.Inventory.Services;

public class InventoryRules
{
    public bool PreventNegativeStock { get; set; } = true;
    public bool MandatoryBatchTracking { get; set; } = true;
    public bool RowLevelLocking { get; set; } = true;
}

public static class InventoryRulesManager
{
    private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "inventory_rules.json");
    private static readonly object LockObj = new();

    public static InventoryRules GetRules()
    {
        lock (LockObj)
        {
            if (!File.Exists(FilePath))
            {
                var defaultRules = new InventoryRules();
                SaveRules(defaultRules);
                return defaultRules;
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<InventoryRules>(json) ?? new InventoryRules();
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save inventory rules: {ex.Message}");
            }
        }
    }
}
