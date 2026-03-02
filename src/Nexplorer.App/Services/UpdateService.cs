using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nexplorer.App.Services;

public sealed record UpdateInfo(string Version, string DownloadUrl, string ReleaseNotes);

public enum UpdateCheckStatus
{
    UpdateAvailable,
    UpToDate,
    Failed
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, UpdateInfo? Update = null);

public sealed class UpdateService
{
    private static readonly string[] VersionUrls =
    [
        "https://mehranh.github.io/Nexplorer/version.json",
        "https://raw.githubusercontent.com/Mehranh/Nexplorer/main/docs/version.json"
    ];

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    /// <summary>
    /// Compares two versions using only Major.Minor.Build, ignoring the Revision
    /// component which differs between parsed 3-part strings (Revision = -1) and
    /// assembly versions (Revision = 0).
    /// </summary>
    internal static bool IsNewerVersion(Version remote, Version current) =>
        NormalizeVersion(remote) > NormalizeVersion(current);

    private static Version NormalizeVersion(Version v) =>
        new(v.Major, v.Minor, Math.Max(v.Build, 0));

    public async Task<UpdateCheckResult> CheckForUpdateAsync()
    {
        try
        {
            var json = await TryFetchVersionJsonAsync().ConfigureAwait(false);
            if (json is null)
            {
                return new UpdateCheckResult(UpdateCheckStatus.Failed);
            }

            var info = JsonSerializer.Deserialize<UpdateInfo>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (info is null) return new UpdateCheckResult(UpdateCheckStatus.Failed);

            if (!TryParseVersion(info.Version, out var remote))
            {
                return new UpdateCheckResult(UpdateCheckStatus.Failed);
            }

            return IsNewerVersion(remote, CurrentVersion)
                ? new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, info)
                : new UpdateCheckResult(UpdateCheckStatus.UpToDate);
        }
        catch
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed);
        }
    }

    private static async Task<string?> TryFetchVersionJsonAsync()
    {
        foreach (var baseUrl in VersionUrls)
        {
            try
            {
                var url = baseUrl.Contains("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase)
                    ? baseUrl
                    : $"{baseUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

                var response = await Http.GetAsync(url).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch
            {
                // try next source
            }
        }

        return null;
    }

    private static bool TryParseVersion(string? rawVersion, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            return false;
        }

        var normalized = rawVersion.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        var coreVersion = Regex.Match(normalized, @"^\d+(?:\.\d+){1,3}").Value;
        if (string.IsNullOrWhiteSpace(coreVersion))
        {
            return false;
        }

        if (!Version.TryParse(coreVersion, out var parsed) || parsed is null)
        {
            return false;
        }

        version = parsed;
        return true;
    }
}
