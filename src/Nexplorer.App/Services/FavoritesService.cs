using System.IO;
using System.Text.Json;

namespace Nexplorer.App.Services;

/// <summary>Persists folder favorites to a JSON file in %LocalAppData%.</summary>
public sealed class FavoritesService
{
    private static readonly JsonSerializerOptions s_writeIndentedOptions = new() { WriteIndented = true };

    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "Nexplorer", "favorites.json");

    public static List<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<string>>(json) ?? new();
        }
        catch { return new(); }
    }

    public static void Save(IEnumerable<string> paths)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(paths.ToList(), s_writeIndentedOptions);
            File.WriteAllText(FilePath, json);
        }
        catch { /* swallow */ }
    }
}
