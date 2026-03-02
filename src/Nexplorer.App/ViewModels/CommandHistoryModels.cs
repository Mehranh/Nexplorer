namespace Nexplorer.App.ViewModels;

public enum ShellKind { PowerShell, Cmd }

public sealed record CommandHistoryEntry(
    DateTimeOffset Timestamp,
    string         WorkingDirectory,
    ShellKind      Shell,
    string         Command,
    int?           ExitCode,
    bool           IsPinned       = false,
    TimeSpan?      Duration       = null,
    string?        ErrorOutput    = null);

// ─── Structured command result ────────────────────────────────────────────────

public sealed record CommandResult(
    int       ExitCode,
    string    StandardOutput,
    string    ErrorOutput,
    TimeSpan  Duration,
    string    Command)
{
    public bool   IsSuccess    => ExitCode == 0;
    public string? ErrorSummary => string.IsNullOrWhiteSpace(ErrorOutput)
        ? null
        : ErrorOutput.TrimEnd().Split('\n')[^1];
}

// ─── Execution metrics (aggregate stats) ──────────────────────────────────────

public sealed class ExecutionMetrics
{
    public int      TotalCommands   { get; private set; }
    public int      SuccessCount    { get; private set; }
    public int      FailureCount    { get; private set; }
    public TimeSpan TotalDuration   { get; private set; }
    public TimeSpan AverageDuration => TotalCommands > 0
        ? TimeSpan.FromTicks(TotalDuration.Ticks / TotalCommands)
        : TimeSpan.Zero;
    public string?  LastCommand     { get; private set; }
    public int?     LastExitCode    { get; private set; }
    public TimeSpan? LastDuration   { get; private set; }
    public TimeSpan? SlowestDuration { get; private set; }
    public string?  SlowestCommand  { get; private set; }

    public void Record(string command, int exitCode, TimeSpan duration)
    {
        TotalCommands++;
        TotalDuration += duration;
        LastCommand = command;
        LastExitCode = exitCode;
        LastDuration = duration;

        if (exitCode == 0) SuccessCount++;
        else FailureCount++;

        if (SlowestDuration is null || duration > SlowestDuration)
        {
            SlowestDuration = duration;
            SlowestCommand = command;
        }
    }
}

// ─── Suggestion model ─────────────────────────────────────────────────────────

public enum SuggestionKind
{
    History,      // from command history
    FileSystem,   // file/directory path completion
    BangCommand,  // !!, !n, !prefix re-run shortcut
}

public sealed record SuggestionItem(
    string         Text,
    SuggestionKind Kind,
    string?        Detail = null);   // e.g. working directory for history items
