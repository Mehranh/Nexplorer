using System.IO;
using System.Text.Json;
using Nexplorer.App.ViewModels;

namespace Nexplorer.App.Services;

public sealed class CommandHistoryStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new() { WriteIndented = false };

    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "Nexplorer", "command-history.json");

    public static List<CommandHistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<CommandHistoryEntry>>(json) ?? new();
        }
        catch { return new(); }
    }

    public static void Save(IEnumerable<CommandHistoryEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(entries.Take(500).ToList(), s_jsonOptions);
            File.WriteAllText(FilePath, json);
        }
        catch { /* best-effort */ }
    }
}
