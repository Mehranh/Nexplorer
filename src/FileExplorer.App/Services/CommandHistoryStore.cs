using System.IO;
using System.Text.Json;
using FileExplorer.App.ViewModels;

namespace FileExplorer.App.Services;

public sealed class CommandHistoryStore
{
    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "Nexplorer", "command-history.json");

    public List<CommandHistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<CommandHistoryEntry>>(json) ?? new();
        }
        catch { return new(); }
    }

    public void Save(IEnumerable<CommandHistoryEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(entries.Take(500).ToList(),
                new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(FilePath, json);
        }
        catch { /* best-effort */ }
    }
}
