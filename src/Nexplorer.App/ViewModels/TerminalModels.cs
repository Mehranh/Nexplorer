namespace Nexplorer.App.ViewModels;

// ─── Terminal profile ─────────────────────────────────────────────────────────

/// <summary>A named terminal profile (e.g. "PowerShell", "CMD", custom).</summary>
public sealed record TerminalProfile(
    string    Name,
    ShellKind Shell,
    string    ExecutablePath,
    string    Arguments,
    string?   StartupDirectory = null,
    string?   ThemeId          = null,
    string?   IconGlyph        = null);

// ─── Terminal theme (color scheme) ────────────────────────────────────────────

public sealed record TerminalTheme(
    string Id,
    string Name,
    string Background,
    string Foreground,
    string Black,      string BrightBlack,
    string Red,        string BrightRed,
    string Green,      string BrightGreen,
    string Yellow,     string BrightYellow,
    string Blue,       string BrightBlue,
    string Magenta,    string BrightMagenta,
    string Cyan,       string BrightCyan,
    string White,      string BrightWhite,
    string CursorColor,
    string SelectionBg);

// ─── Command alias ────────────────────────────────────────────────────────────

public sealed record CommandAlias(string Name, string Expansion, ShellKind? Shell = null);

// ─── Background job ───────────────────────────────────────────────────────────

public enum JobStatus { Running, Completed, Failed, Cancelled }

public sealed class BackgroundJob
{
    public int       Id              { get; init; }
    public string    Command         { get; init; } = string.Empty;
    public string    WorkingDirectory{ get; init; } = string.Empty;
    public ShellKind Shell           { get; init; }
    public JobStatus Status          { get; set; } = JobStatus.Running;
    public int?      ExitCode        { get; set; }
    public string    Output          { get; set; } = string.Empty;
    public DateTimeOffset StartedAt  { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset? EndedAt   { get; set; }

    public System.Diagnostics.Process? Process { get; set; }
}

// ─── Environment variable entry (for display) ────────────────────────────────

public sealed record EnvVarEntry(string Name, string Value);

// ─── ANSI colored text segment ────────────────────────────────────────────────

public sealed record AnsiSegment(string Text, string? Foreground = null, string? Background = null, bool Bold = false);

// ─── Split orientation ────────────────────────────────────────────────────────

public enum TerminalSplitOrientation { None, Horizontal, Vertical }
