using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace FileExplorer.App.Services;

public sealed record UpdateInfo(string Version, string DownloadUrl, string ReleaseNotes);

public sealed class UpdateService
{
    private const string VersionUrl =
        "https://mehranh.github.io/Nexplorer/version.json";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            // Cache-busting query param to avoid stale CDN responses
            var url = $"{VersionUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var json = await Http.GetStringAsync(url).ConfigureAwait(false);
            var info = JsonSerializer.Deserialize<UpdateInfo>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (info is null) return null;

            var remote = Version.Parse(info.Version);
            return remote > CurrentVersion ? info : null;
        }
        catch
        {
            return null; // network error — silently skip
        }
    }
}
