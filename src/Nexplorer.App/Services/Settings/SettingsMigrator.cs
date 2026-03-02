using System.Text.Json;

namespace Nexplorer.App.Services.Settings;

/// <summary>
/// Migrates persisted settings JSON from older versions to the current schema.
/// Each migration is a pure function: old JSON → new JSON.
/// </summary>
public static class SettingsMigrator
{
    /// <summary>
    /// Apply all necessary migrations from the stored version to <see cref="SettingsDefaults.CurrentVersion"/>.
    /// Returns <c>null</c> if the document is already current or cannot be parsed.
    /// </summary>
    public static string? MigrateIfNeeded(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        int version = root.TryGetProperty("version", out var v) ? v.GetInt32() : 1;
        if (version >= SettingsDefaults.CurrentVersion)
            return null; // already up-to-date

        // Deserialize into a mutable dictionary for stepwise patching
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
                   ?? new Dictionary<string, JsonElement>();

        while (version < SettingsDefaults.CurrentVersion)
        {
            dict = version switch
            {
                1 => MigrateV1ToV2(dict),
                _ => dict
            };
            version++;
        }

        dict["version"] = JsonSerializer.SerializeToElement(SettingsDefaults.CurrentVersion);
        return JsonSerializer.Serialize(dict, JsonSettingsService.SerializerOptions);
    }

    // ── v1 → v2 example: add performance.throttleBackgroundMetadataMs ──────

    private static Dictionary<string, JsonElement> MigrateV1ToV2(Dictionary<string, JsonElement> dict)
    {
        // Example: ensure 'performance' section exists with new default field
        if (dict.TryGetValue("performance", out var perfElement))
        {
            var perf = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(perfElement.GetRawText())
                       ?? new Dictionary<string, JsonElement>();

            perf.TryAdd("throttleBackgroundMetadataMs",
                         JsonSerializer.SerializeToElement(100));
            perf.TryAdd("enableUsnJournalMonitoring",
                         JsonSerializer.SerializeToElement(false));

            dict["performance"] = JsonSerializer.SerializeToElement(perf);
        }

        // Example: rename old key
        if (dict.TryGetValue("commandRunner", out var crElement))
        {
            var cr = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(crElement.GetRawText())
                     ?? new Dictionary<string, JsonElement>();

            // If v1 had "shellType" instead of "defaultShell", rename it
            if (cr.Remove("shellType", out var shellVal))
            {
                cr.TryAdd("defaultShell", shellVal);
            }

            dict["commandRunner"] = JsonSerializer.SerializeToElement(cr);
        }

        return dict;
    }
}
