using System.IO;
using System.Text.Json;
using Nexplorer.App.ViewModels;

namespace Nexplorer.App.Services;

/// <summary>
/// Manages terminal profiles (shell configurations) and terminal themes.
/// </summary>
public sealed class TerminalProfileService
{
    private static readonly string BasePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexplorer");

    private static readonly string ProfilesPath = Path.Combine(BasePath, "terminal-profiles.json");
    private static readonly string ThemesPath    = Path.Combine(BasePath, "terminal-themes.json");

    private readonly List<TerminalProfile> _profiles = new();
    private readonly List<TerminalTheme>   _themes   = new();

    public IReadOnlyList<TerminalProfile> Profiles => _profiles;
    public IReadOnlyList<TerminalTheme>   Themes   => _themes;

    public TerminalProfileService()
    {
        LoadProfiles();
        LoadThemes();
        EnsureDefaults();
    }

    // ─── Profiles ─────────────────────────────────────────────────────────────

    public TerminalProfile GetDefault()
        => _profiles.FirstOrDefault(p => p.Shell == ShellKind.PowerShell) ?? _profiles[0];

    public TerminalProfile? GetByName(string name)
        => _profiles.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    // ─── Themes ───────────────────────────────────────────────────────────────

    public TerminalTheme GetDefaultTheme()
        => _themes.FirstOrDefault(t => t.Id == "dark-plus") ?? _themes[0];

    public TerminalTheme? GetThemeById(string id)
        => _themes.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    // ─── Persistence ──────────────────────────────────────────────────────────

    private void LoadProfiles()
    {
        try
        {
            if (!File.Exists(ProfilesPath)) return;
            var json = File.ReadAllText(ProfilesPath);
            var loaded = JsonSerializer.Deserialize<List<TerminalProfile>>(json);
            if (loaded is not null) _profiles.AddRange(loaded);
        }
        catch { /* best-effort */ }
    }

    private void LoadThemes()
    {
        try
        {
            if (!File.Exists(ThemesPath)) return;
            var json = File.ReadAllText(ThemesPath);
            var loaded = JsonSerializer.Deserialize<List<TerminalTheme>>(json);
            if (loaded is not null) _themes.AddRange(loaded);
        }
        catch { /* best-effort */ }
    }

    public void SaveProfiles()
    {
        try
        {
            Directory.CreateDirectory(BasePath);
            File.WriteAllText(ProfilesPath,
                JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void EnsureDefaults()
    {
        // ── Default profiles ──
        if (_profiles.Count == 0)
        {
            _profiles.Add(new TerminalProfile(
                Name: "PowerShell",
                Shell: ShellKind.PowerShell,
                ExecutablePath: "powershell.exe",
                Arguments: "-NoLogo -NoProfile -NonInteractive -Command",
                IconGlyph: "\uE756"));

            _profiles.Add(new TerminalProfile(
                Name: "CMD",
                Shell: ShellKind.Cmd,
                ExecutablePath: "cmd.exe",
                Arguments: "/c",
                IconGlyph: "\uE756"));

            // Try to detect pwsh (PowerShell 7+)
            var pwshPath = FindExecutable("pwsh.exe");
            if (pwshPath is not null)
            {
                _profiles.Add(new TerminalProfile(
                    Name: "PowerShell 7",
                    Shell: ShellKind.PowerShell,
                    ExecutablePath: pwshPath,
                    Arguments: "-NoLogo -NoProfile -NonInteractive -Command",
                    IconGlyph: "\uE756"));
            }

            SaveProfiles();
        }

        // ── Default themes ──
        if (_themes.Count == 0)
        {
            _themes.Add(new TerminalTheme(
                Id: "dark-plus", Name: "Dark+ (Default)",
                Background: "#0C0C0C", Foreground: "#CCCCCC",
                Black: "#0C0C0C", BrightBlack: "#767676",
                Red: "#C50F1F", BrightRed: "#E74856",
                Green: "#13A10E", BrightGreen: "#16C60C",
                Yellow: "#C19C00", BrightYellow: "#F9F1A5",
                Blue: "#0037DA", BrightBlue: "#3B78FF",
                Magenta: "#881798", BrightMagenta: "#B4009E",
                Cyan: "#3A96DD", BrightCyan: "#61D6D6",
                White: "#CCCCCC", BrightWhite: "#F2F2F2",
                CursorColor: "#9CDCFE", SelectionBg: "#094771"));

            _themes.Add(new TerminalTheme(
                Id: "one-dark", Name: "One Dark",
                Background: "#282C34", Foreground: "#ABB2BF",
                Black: "#282C34", BrightBlack: "#5C6370",
                Red: "#E06C75", BrightRed: "#E06C75",
                Green: "#98C379", BrightGreen: "#98C379",
                Yellow: "#E5C07B", BrightYellow: "#E5C07B",
                Blue: "#61AFEF", BrightBlue: "#61AFEF",
                Magenta: "#C678DD", BrightMagenta: "#C678DD",
                Cyan: "#56B6C2", BrightCyan: "#56B6C2",
                White: "#ABB2BF", BrightWhite: "#FFFFFF",
                CursorColor: "#528BFF", SelectionBg: "#3E4451"));

            _themes.Add(new TerminalTheme(
                Id: "solarized-dark", Name: "Solarized Dark",
                Background: "#002B36", Foreground: "#839496",
                Black: "#073642", BrightBlack: "#586E75",
                Red: "#DC322F", BrightRed: "#CB4B16",
                Green: "#859900", BrightGreen: "#586E75",
                Yellow: "#B58900", BrightYellow: "#657B83",
                Blue: "#268BD2", BrightBlue: "#839496",
                Magenta: "#D33682", BrightMagenta: "#6C71C4",
                Cyan: "#2AA198", BrightCyan: "#93A1A1",
                White: "#EEE8D5", BrightWhite: "#FDF6E3",
                CursorColor: "#839496", SelectionBg: "#073642"));

            _themes.Add(new TerminalTheme(
                Id: "monokai", Name: "Monokai",
                Background: "#272822", Foreground: "#F8F8F2",
                Black: "#272822", BrightBlack: "#75715E",
                Red: "#F92672", BrightRed: "#F92672",
                Green: "#A6E22E", BrightGreen: "#A6E22E",
                Yellow: "#F4BF75", BrightYellow: "#F4BF75",
                Blue: "#66D9EF", BrightBlue: "#66D9EF",
                Magenta: "#AE81FF", BrightMagenta: "#AE81FF",
                Cyan: "#A1EFE4", BrightCyan: "#A1EFE4",
                White: "#F8F8F2", BrightWhite: "#F9F8F5",
                CursorColor: "#F8F8F0", SelectionBg: "#49483E"));

            _themes.Add(new TerminalTheme(
                Id: "dracula", Name: "Dracula",
                Background: "#282A36", Foreground: "#F8F8F2",
                Black: "#21222C", BrightBlack: "#6272A4",
                Red: "#FF5555", BrightRed: "#FF6E6E",
                Green: "#50FA7B", BrightGreen: "#69FF94",
                Yellow: "#F1FA8C", BrightYellow: "#FFFFA5",
                Blue: "#BD93F9", BrightBlue: "#D6ACFF",
                Magenta: "#FF79C6", BrightMagenta: "#FF92DF",
                Cyan: "#8BE9FD", BrightCyan: "#A4FFFF",
                White: "#F8F8F2", BrightWhite: "#FFFFFF",
                CursorColor: "#F8F8F2", SelectionBg: "#44475A"));

            try
            {
                Directory.CreateDirectory(BasePath);
                File.WriteAllText(ThemesPath,
                    JsonSerializer.Serialize(_themes, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }
    }

    private static string? FindExecutable(string exeName)
    {
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
        foreach (var dir in pathDirs)
        {
            var full = Path.Combine(dir, exeName);
            if (File.Exists(full)) return full;
        }
        return null;
    }
}
