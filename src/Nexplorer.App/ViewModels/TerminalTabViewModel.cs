using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexplorer.App.Services;

namespace Nexplorer.App.ViewModels;

/// <summary>
/// ViewModel for a single terminal tab. Each tab has its own shell type,
/// working directory, output buffer, history, and state.
/// </summary>
public sealed partial class TerminalTabViewModel : ObservableObject
{
    private static int s_nextId;
    private readonly AliasService _aliasService;
    private readonly CommandHistoryStore _historyStore;
    private readonly string _shellExecutablePath;
    private readonly string _shellArguments;
    private readonly TabCycleState _tabState = new();
    private int _historyIndex = -1;
    private string _commandSnapshot = string.Empty;
    private string _inlineSuggestionFull = string.Empty;
    private bool _suppressSuggestionRefresh;

    public TerminalTabViewModel(
        ShellKind shell,
        string workingDirectory,
        AliasService aliasService,
        CommandHistoryStore historyStore,
        ObservableCollection<CommandHistoryEntry> sharedHistory,
        TerminalTheme? theme = null,
        string? shellExecutablePath = null,
        string? shellArguments = null)
    {
        Id = Interlocked.Increment(ref s_nextId);
        _shell = shell;
        _workingDirectory = workingDirectory;
        _aliasService = aliasService;
        _historyStore = historyStore;
        _shellExecutablePath = string.IsNullOrWhiteSpace(shellExecutablePath)
            ? GetDefaultExecutable(shell)
            : shellExecutablePath;
        _shellArguments = string.IsNullOrWhiteSpace(shellArguments)
            ? GetDefaultArguments(shell)
            : shellArguments;
        SharedHistory = sharedHistory;
        Theme = theme;

        Header = shell == ShellKind.PowerShell ? "PS" : "CMD";
        UpdateGitBranch();
        UpdatePrompt();
    }

    // ─── Identity ─────────────────────────────────────────────────────────────

    public int Id { get; }
    public string ShellExecutablePath => _shellExecutablePath;
    public string ShellArguments => _shellArguments;

    [ObservableProperty] private string _header;
    [ObservableProperty] private bool _isActive;
    public TerminalTheme? Theme { get; set; }

    // ─── Shell & working directory ────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Prompt))]
    private ShellKind _shell;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Prompt))]
    private string _workingDirectory;

    [ObservableProperty] private string? _gitBranch;
    [ObservableProperty] private GitBranchInfo? _gitBranchInfo;

    // ─── Output ───────────────────────────────────────────────────────────────

    /// <summary>Raw output text (may contain ANSI codes).</summary>
    [ObservableProperty] private string _outputText = string.Empty;

    /// <summary>Parsed ANSI segments for rich rendering.</summary>
    public ObservableCollection<AnsiSegment> OutputSegments { get; } = new();

    // ─── Command input ────────────────────────────────────────────────────────

    private string _commandText = string.Empty;
    public string CommandText
    {
        get => _commandText;
        set
        {
            if (SetProperty(ref _commandText, value) && !_suppressSuggestionRefresh)
            {
                _historyIndex = -1;
                _tabState.Reset();
                RefreshSuggestions(value);
            }
        }
    }

    [ObservableProperty] private string _inlineSuggestion = string.Empty;
    [ObservableProperty] private bool _showSuggestions;
    [ObservableProperty] private int _selectedSuggestionIndex = -1;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _prompt = string.Empty;

    // ─── Last command result & execution metrics ──────────────────────────────

    [ObservableProperty] private CommandResult? _lastResult;
    public ExecutionMetrics Metrics { get; } = new();

    // ─── History search (Ctrl+R) ──────────────────────────────────────────────

    [ObservableProperty] private bool _isHistorySearchActive;
    [ObservableProperty] private string _historySearchText = string.Empty;
    [ObservableProperty] private string? _historySearchResult;
    private int _historySearchIndex;

    // ─── Background jobs ──────────────────────────────────────────────────────

    public ObservableCollection<BackgroundJob> BackgroundJobs { get; } = new();
    private int _nextJobId;

    // ─── Environment variables ────────────────────────────────────────────────

    [ObservableProperty] private bool _showEnvVars;
    public ObservableCollection<EnvVarEntry> EnvironmentVariables { get; } = new();

    // ─── Suggestions ──────────────────────────────────────────────────────────

    public ObservableCollection<SuggestionItem> Suggestions { get; } = new();
    public ObservableCollection<CommandHistoryEntry> SharedHistory { get; }

    // ─── Prompt ───────────────────────────────────────────────────────────────

    private void UpdatePrompt()
    {
        var gitPart = GitBranchInfo is not null
            ? $" \ue0a0 {GitBranchInfo.FormatPrompt()}"
            : GitBranch is not null ? $" \ue0a0 {GitBranch}" : string.Empty;

        Prompt = Shell == ShellKind.PowerShell
            ? $"PS {WorkingDirectory}{gitPart}>"
            : $"{WorkingDirectory}{gitPart}>";
    }

    partial void OnShellChanged(ShellKind value)
    {
        Header = value == ShellKind.PowerShell ? "PS" : "CMD";
        UpdatePrompt();
    }

    partial void OnWorkingDirectoryChanged(string value)
    {
        UpdateGitBranch();
        UpdatePrompt();
    }

    private void UpdateGitBranch()
    {
        Task.Run(() =>
        {
            var info = GitBranchService.GetBranchInfo(WorkingDirectory);
            Application.Current?.Dispatcher.Invoke(() =>
            {
                GitBranch = info?.Branch;
                GitBranchInfo = info;
                UpdatePrompt();
            });
        });
    }

    // ─── Auto-complete / suggestions ──────────────────────────────────────────

    private void RefreshSuggestions(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            Suggestions.Clear();
            ShowSuggestions = false;
            InlineSuggestion = string.Empty;
            _inlineSuggestionFull = string.Empty;
            return;
        }

        var items = CompletionService.GetSuggestions(
            input, WorkingDirectory, SharedHistory, maxItems: 12);

        Suggestions.Clear();
        foreach (var s in items) Suggestions.Add(s);

        SelectedSuggestionIndex = -1;
        ShowSuggestions = Suggestions.Count > 0;

        var ghost = Suggestions.FirstOrDefault(s =>
            s.Kind == SuggestionKind.History
            && s.Text.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            ?? Suggestions.FirstOrDefault(s =>
                s.Text.StartsWith(input, StringComparison.OrdinalIgnoreCase));

        _inlineSuggestionFull = ghost?.Text ?? string.Empty;
        InlineSuggestion = BuildGhostText(input, _inlineSuggestionFull);
    }

    private static string BuildGhostText(string input, string fullSuggestion)
    {
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(fullSuggestion))
            return string.Empty;

        if (!fullSuggestion.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        if (fullSuggestion.Length <= input.Length)
            return string.Empty;

        return new string(' ', input.Length) + fullSuggestion[input.Length..];
    }

    // ─── History navigation ───────────────────────────────────────────────────

    public void NavigateHistoryUp()
    {
        if (SharedHistory.Count == 0) return;
        if (_historyIndex < 0) _commandSnapshot = CommandText;
        _historyIndex = Math.Min(_historyIndex + 1, SharedHistory.Count - 1);
        ApplyHistoryEntry(_historyIndex);
    }

    public void NavigateHistoryDown()
    {
        if (_historyIndex < 0) return;
        _historyIndex--;
        _suppressSuggestionRefresh = true;
        if (_historyIndex < 0) { CommandText = _commandSnapshot; RefreshSuggestions(CommandText); }
        else ApplyHistoryEntry(_historyIndex);
        _suppressSuggestionRefresh = false;
    }

    private void ApplyHistoryEntry(int index)
    {
        _suppressSuggestionRefresh = true;
        CommandText = SharedHistory[index].Command;
        InlineSuggestion = string.Empty;
        ShowSuggestions = false;
        _suppressSuggestionRefresh = false;
    }

    // ─── Tab completion ───────────────────────────────────────────────────────

    public void HandleTabCompletion()
    {
        _suppressSuggestionRefresh = true;
        var next = CompletionService.CycleTabCompletion(CommandText, WorkingDirectory, _tabState);
        CommandText = next;
        ShowSuggestions = false;
        InlineSuggestion = string.Empty;
        _inlineSuggestionFull = string.Empty;
        _suppressSuggestionRefresh = false;
    }

    public void AcceptSuggestion(SuggestionItem? item = null)
    {
        var text = item?.Text
                ?? (SelectedSuggestionIndex >= 0 && SelectedSuggestionIndex < Suggestions.Count
                        ? Suggestions[SelectedSuggestionIndex].Text : null)
            ?? (string.IsNullOrEmpty(_inlineSuggestionFull) ? null : _inlineSuggestionFull)
            ?? (string.IsNullOrEmpty(InlineSuggestion) ? null : InlineSuggestion.TrimStart());
        if (text is null) return;

        _suppressSuggestionRefresh = true;
        CommandText = text;
        ShowSuggestions = false;
        InlineSuggestion = string.Empty;
        _inlineSuggestionFull = string.Empty;
        _historyIndex = -1;
        _tabState.Reset();
        _suppressSuggestionRefresh = false;
    }

    public void DismissSuggestions()
    {
        ShowSuggestions = false;
        InlineSuggestion = string.Empty;
        _inlineSuggestionFull = string.Empty;
    }

    // ─── Ctrl+R reverse history search ────────────────────────────────────────

    [RelayCommand]
    private void ToggleHistorySearch()
    {
        IsHistorySearchActive = !IsHistorySearchActive;
        if (IsHistorySearchActive)
        {
            HistorySearchText = string.Empty;
            HistorySearchResult = null;
            _historySearchIndex = 0;
        }
    }

    public void UpdateHistorySearch(string searchText)
    {
        HistorySearchText = searchText;
        _historySearchIndex = 0;
        FindNextHistoryMatch();
    }

    public void FindNextHistoryMatch()
    {
        if (string.IsNullOrEmpty(HistorySearchText))
        {
            HistorySearchResult = null;
            return;
        }

        for (int i = _historySearchIndex; i < SharedHistory.Count; i++)
        {
            if (SharedHistory[i].Command.Contains(HistorySearchText, StringComparison.OrdinalIgnoreCase))
            {
                HistorySearchResult = SharedHistory[i].Command;
                _historySearchIndex = i + 1;
                return;
            }
        }

        // Wrap around
        for (int i = 0; i < _historySearchIndex && i < SharedHistory.Count; i++)
        {
            if (SharedHistory[i].Command.Contains(HistorySearchText, StringComparison.OrdinalIgnoreCase))
            {
                HistorySearchResult = SharedHistory[i].Command;
                _historySearchIndex = i + 1;
                return;
            }
        }

        HistorySearchResult = null;
    }

    public void AcceptHistorySearch()
    {
        if (HistorySearchResult is not null)
        {
            _suppressSuggestionRefresh = true;
            CommandText = HistorySearchResult;
            _suppressSuggestionRefresh = false;
        }
        IsHistorySearchActive = false;
    }

    public void CancelHistorySearch()
    {
        IsHistorySearchActive = false;
        HistorySearchText = string.Empty;
        HistorySearchResult = null;
    }

    // ─── Environment variables ────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleEnvVars()
    {
        ShowEnvVars = !ShowEnvVars;
        if (ShowEnvVars)
            RefreshEnvironmentVariables();
    }

    private void RefreshEnvironmentVariables()
    {
        EnvironmentVariables.Clear();
        var vars = Environment.GetEnvironmentVariables();
        var sorted = vars.Keys.Cast<string>().OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        foreach (var key in sorted)
        {
            var value = vars[key]?.ToString() ?? string.Empty;
            EnvironmentVariables.Add(new EnvVarEntry(key, value));
        }
    }

    // ─── Command execution ────────────────────────────────────────────────────

    private const string CwdMarker = "::__CWD__::";

    [RelayCommand]
    private async Task RunCommandAsync()
    {
        var rawCmd = CommandText.Trim();
        if (string.IsNullOrWhiteSpace(rawCmd)) return;

        var wd = WorkingDirectory;
        if (string.IsNullOrWhiteSpace(wd) || !Directory.Exists(wd))
            wd = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        DismissSuggestions();
        _historyIndex = -1;
        _tabState.Reset();

        // Handle bang-commands (!! / !n / !prefix) — resolve to actual command
        var resolvedCmd = ResolveBangCommand(rawCmd);
        if (resolvedCmd is not null)
            rawCmd = resolvedCmd;

        // Handle built-in commands
        if (await HandleBuiltInCommandAsync(rawCmd, wd)) return;

        // Expand aliases
        var cmd = _aliasService.ExpandAliases(rawCmd, Shell);

        // Expand ~ in arguments
        cmd = ExpandTildeInCommand(cmd);

        // Check for background job syntax (command ending with &)
        if (cmd.TrimEnd().EndsWith('&'))
        {
            RunBackgroundJob(cmd.TrimEnd().TrimEnd('&').TrimEnd(), wd);
            return;
        }

        OutputText = string.Empty;
        OutputSegments.Clear();
        IsRunning = true;

        var wrappedCmd = WrapCommandForCwdTracking(Shell, cmd);
        var startInfo = BuildStartInfo(wrappedCmd, wd);
        int? exitCode = null;
        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();
        var sbAll = new StringBuilder();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                lock (sbAll) { sbOut.AppendLine(e.Data); sbAll.AppendLine(e.Data); }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OutputText = sbAll.ToString();
                    UpdateOutputSegments(sbAll.ToString());
                });
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                lock (sbAll) { sbErr.AppendLine(e.Data); sbAll.AppendLine(e.Data); }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OutputText = sbAll.ToString();
                    UpdateOutputSegments(sbAll.ToString());
                });
            };

            if (!process.Start()) { OutputText = "Failed to start process."; return; }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            exitCode = process.ExitCode;
        }
        catch (Exception ex) { sbAll.AppendLine(ex.Message); sbErr.AppendLine(ex.Message); }
        finally
        {
            stopwatch.Stop();
            var duration = stopwatch.Elapsed;
            var newWd = ExtractFinalCwd(sbAll);
            var errText = sbErr.ToString().Trim();

            // Build structured result
            var result = new CommandResult(
                ExitCode: exitCode ?? -1,
                StandardOutput: sbOut.ToString(),
                ErrorOutput: errText,
                Duration: duration,
                Command: rawCmd);
            LastResult = result;

            // Track metrics
            Metrics.Record(rawCmd, result.ExitCode, duration);

            OutputText = sbAll.ToString();
            UpdateOutputSegments(sbAll.ToString());

            // Append structured error summary if command failed
            if (!result.IsSuccess)
            {
                AppendErrorSummary(result);
            }

            // Append execution duration
            AppendDurationInfo(result);

            IsRunning = false;

            if (newWd is not null && !string.Equals(newWd, wd, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(newWd))
            {
                WorkingDirectory = newWd;
            }

            var entry = new CommandHistoryEntry(
                Timestamp: DateTimeOffset.Now,
                WorkingDirectory: newWd ?? wd,
                Shell: Shell,
                Command: rawCmd,
                ExitCode: exitCode,
                Duration: duration,
                ErrorOutput: string.IsNullOrWhiteSpace(errText) ? null : errText);

            SharedHistory.Insert(0, entry);
            CommandHistoryStore.Save(SharedHistory);

            _suppressSuggestionRefresh = true;
            CommandText = string.Empty;
            _suppressSuggestionRefresh = false;
        }
    }

    // ─── Structured error output ──────────────────────────────────────────────

    private void AppendErrorSummary(CommandResult result)
    {
        OutputSegments.Add(new AnsiSegment("\n", null));
        OutputSegments.Add(new AnsiSegment($"✗ Command failed (exit code {result.ExitCode})\n", "#E74856", Bold: true));
        if (result.ErrorSummary is not null)
        {
            OutputSegments.Add(new AnsiSegment($"  Error: {result.ErrorSummary}\n", "#E74856"));
        }
        OutputText += $"\n✗ Command failed (exit code {result.ExitCode})\n";
        if (result.ErrorSummary is not null)
            OutputText += $"  Error: {result.ErrorSummary}\n";
    }

    private void AppendDurationInfo(CommandResult result)
    {
        var durationText = FormatDuration(result.Duration);
        var color = result.IsSuccess ? "#3A96DD" : "#E74856";
        OutputSegments.Add(new AnsiSegment($"  ⏱ {durationText}\n", color));
        OutputText += $"  ⏱ {durationText}\n";
    }

    private static string FormatDuration(TimeSpan ts) =>
        ts.TotalMinutes >= 1 ? $"{ts.Minutes}m {ts.Seconds}.{ts.Milliseconds / 100}s"
        : ts.TotalSeconds >= 1 ? $"{ts.TotalSeconds:F1}s"
        : $"{ts.TotalMilliseconds:F0}ms";

    // ─── Bang-command resolution ──────────────────────────────────────────────

    private string? ResolveBangCommand(string input)
    {
        if (!input.StartsWith('!') || SharedHistory.Count == 0) return null;

        if (input == "!!")
            return SharedHistory[0].Command;

        // !n — re-run nth (1-based) history entry
        if (input.Length > 1 && int.TryParse(input[1..], out int idx))
        {
            if (idx >= 1 && idx <= SharedHistory.Count)
                return SharedHistory[idx - 1].Command;
            return null;
        }

        // !prefix — last command starting with prefix
        if (input.Length > 1)
        {
            var prefix = input[1..];
            var match = SharedHistory.FirstOrDefault(h =>
                h.Command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            return match?.Command;
        }

        return null;
    }

    // ─── Tilde expansion in commands ──────────────────────────────────────────

    private static string ExpandTildeInCommand(string cmd)
    {
        // Only expand ~ when it's a standalone token or starts a path token
        var parts = cmd.Split(' ');
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = CompletionService.ExpandTilde(parts[i]);
        }
        return string.Join(' ', parts);
    }

    // ─── Built-in commands ────────────────────────────────────────────────────

    private async Task<bool> HandleBuiltInCommandAsync(string cmd, string wd)
    {
        var parts = cmd.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var verb = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        switch (verb)
        {
            case "alias" when string.IsNullOrEmpty(arg):
                ShowAliases();
                return true;

            case "alias" when arg.Contains('='):
                var eqIdx = arg.IndexOf('=');
                var aliasName = arg[..eqIdx].Trim();
                var aliasValue = arg[(eqIdx + 1)..].Trim().Trim('"', '\'');
                _aliasService.AddAlias(aliasName, aliasValue, Shell);
                OutputText = $"Alias added: {aliasName} → {aliasValue}\n";
                OutputSegments.Clear();
                OutputSegments.Add(new AnsiSegment($"Alias added: {aliasName} → {aliasValue}\n", "#16C60C"));
                FinalizeCommandEntry(cmd, wd, 0);
                return true;

            case "unalias" when !string.IsNullOrEmpty(arg):
                _aliasService.RemoveAlias(arg);
                OutputText = $"Alias removed: {arg}\n";
                OutputSegments.Clear();
                OutputSegments.Add(new AnsiSegment($"Alias removed: {arg}\n", "#E74856"));
                FinalizeCommandEntry(cmd, wd, 0);
                return true;

            case "env" or "printenv":
                ShowEnvironmentVariables(arg);
                FinalizeCommandEntry(cmd, wd, 0);
                return true;

            case "export" when !string.IsNullOrEmpty(arg) && arg.Contains('='):
                SetEnvironmentVariable(arg);
                FinalizeCommandEntry(cmd, wd, 0);
                return true;

            case "jobs":
                ShowJobs();
                FinalizeCommandEntry(cmd, wd, 0);
                return true;

            case "kill" when !string.IsNullOrEmpty(arg):
                KillJob(arg);
                FinalizeCommandEntry(cmd, wd, 0);
                return true;

            case "metrics":
                ShowMetrics();
                FinalizeCommandEntry(cmd, wd, 0);
                return true;

            case "clear" or "cls":
                OutputText = string.Empty;
                OutputSegments.Clear();
                _suppressSuggestionRefresh = true;
                CommandText = string.Empty;
                _suppressSuggestionRefresh = false;
                return true;

            case "theme" when !string.IsNullOrEmpty(arg):
                // Handled by panel VM through event
                ThemeChangeRequested?.Invoke(this, arg);
                _suppressSuggestionRefresh = true;
                CommandText = string.Empty;
                _suppressSuggestionRefresh = false;
                return true;

            default:
                return false;
        }
    }

    /// <summary>Raised when the tab requests a theme change.</summary>
    public event EventHandler<string>? ThemeChangeRequested;

    private void FinalizeCommandEntry(string cmd, string wd, int exitCode)
    {
        var entry = new CommandHistoryEntry(
            Timestamp: DateTimeOffset.Now,
            WorkingDirectory: wd,
            Shell: Shell,
            Command: cmd,
            ExitCode: exitCode);
        SharedHistory.Insert(0, entry);
        CommandHistoryStore.Save(SharedHistory);
        _suppressSuggestionRefresh = true;
        CommandText = string.Empty;
        _suppressSuggestionRefresh = false;
    }

    private void ShowAliases()
    {
        var aliases = _aliasService.GetAliases(Shell);
        OutputSegments.Clear();
        var sb = new StringBuilder();

        if (aliases.Count == 0)
        {
            sb.AppendLine("No aliases defined.");
            OutputSegments.Add(new AnsiSegment("No aliases defined.\n", "#858585"));
        }
        else
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{"Alias",-20} {"Expansion",-40} {"Shell",-12}");
            sb.AppendLine(new string('─', 72));
            OutputSegments.Add(new AnsiSegment($"{"Alias",-20} {"Expansion",-40} {"Shell",-12}\n", "#61D6D6", Bold: true));
            OutputSegments.Add(new AnsiSegment(new string('─', 72) + "\n", "#3F3F46"));

            foreach (var a in aliases)
            {
                var line = $"{a.Name,-20} {a.Expansion,-40} {(a.Shell?.ToString() ?? "any"),-12}";
                sb.AppendLine(line);
                OutputSegments.Add(new AnsiSegment($"{a.Name,-20} ", "#F9F1A5"));
                OutputSegments.Add(new AnsiSegment($"{a.Expansion,-40} ", "#CCCCCC"));
                OutputSegments.Add(new AnsiSegment($"{(a.Shell?.ToString() ?? "any"),-12}\n", "#858585"));
            }
        }
        OutputText = sb.ToString();
    }

    private void ShowEnvironmentVariables(string filter)
    {
        var vars = Environment.GetEnvironmentVariables();
        var sb = new StringBuilder();
        OutputSegments.Clear();

        var keys = vars.Keys.Cast<string>()
            .Where(k => string.IsNullOrEmpty(filter) || k.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            var val = vars[key]?.ToString() ?? string.Empty;
            sb.AppendLine(CultureInfo.InvariantCulture, $"{key}={val}");
            OutputSegments.Add(new AnsiSegment($"{key}", "#61D6D6"));
            OutputSegments.Add(new AnsiSegment("=", "#858585"));
            OutputSegments.Add(new AnsiSegment($"{val}\n", "#CCCCCC"));
        }
        OutputText = sb.ToString();
    }

    private void SetEnvironmentVariable(string arg)
    {
        var eqIdx = arg.IndexOf('=');
        var name = arg[..eqIdx].Trim();
        var value = arg[(eqIdx + 1)..].Trim().Trim('"', '\'');
        Environment.SetEnvironmentVariable(name, value);

        OutputSegments.Clear();
        OutputText = $"Set {name}={value}\n";
        OutputSegments.Add(new AnsiSegment("Set ", "#858585"));
        OutputSegments.Add(new AnsiSegment($"{name}", "#61D6D6"));
        OutputSegments.Add(new AnsiSegment("=", "#858585"));
        OutputSegments.Add(new AnsiSegment($"{value}\n", "#16C60C"));
    }

    private void ShowJobs()
    {
        OutputSegments.Clear();
        var sb = new StringBuilder();

        if (BackgroundJobs.Count == 0)
        {
            sb.AppendLine("No background jobs.");
            OutputSegments.Add(new AnsiSegment("No background jobs.\n", "#858585"));
        }
        else
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"{"ID",-5} {"Status",-12} {"Command",-40} {"Started",-20}");
            sb.AppendLine(new string('─', 77));
            OutputSegments.Add(new AnsiSegment($"{"ID",-5} {"Status",-12} {"Command",-40} {"Started",-20}\n", "#61D6D6", Bold: true));
            OutputSegments.Add(new AnsiSegment(new string('─', 77) + "\n", "#3F3F46"));

            foreach (var job in BackgroundJobs)
            {
                var statusColor = job.Status switch
                {
                    JobStatus.Running => "#16C60C",
                    JobStatus.Completed => "#3A96DD",
                    JobStatus.Failed => "#E74856",
                    _ => "#858585"
                };

                sb.AppendLine(CultureInfo.InvariantCulture, $"{job.Id,-5} {job.Status,-12} {job.Command,-40} {job.StartedAt:HH:mm:ss,-20}");
                OutputSegments.Add(new AnsiSegment($"{job.Id,-5} ", "#F9F1A5"));
                OutputSegments.Add(new AnsiSegment($"{job.Status,-12} ", statusColor));
                OutputSegments.Add(new AnsiSegment($"{job.Command,-40} ", "#CCCCCC"));
                OutputSegments.Add(new AnsiSegment($"{job.StartedAt:HH:mm:ss}\n", "#858585"));
            }
        }
        OutputText = sb.ToString();
    }

    // ─── Execution metrics display ────────────────────────────────────────────

    private void ShowMetrics()
    {
        OutputSegments.Clear();
        var sb = new StringBuilder();
        var m = Metrics;

        sb.AppendLine("Execution Metrics");
        sb.AppendLine(new string('─', 50));
        OutputSegments.Add(new AnsiSegment("Execution Metrics\n", "#61D6D6", Bold: true));
        OutputSegments.Add(new AnsiSegment(new string('─', 50) + "\n", "#3F3F46"));

        void AddRow(string label, string value, string valueColor)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {label,-24} {value}");
            OutputSegments.Add(new AnsiSegment($"  {label,-24} ", "#858585"));
            OutputSegments.Add(new AnsiSegment($"{value}\n", valueColor));
        }

        AddRow("Total commands:", m.TotalCommands.ToString(CultureInfo.InvariantCulture), "#CCCCCC");
        AddRow("Successful:", m.SuccessCount.ToString(CultureInfo.InvariantCulture), "#16C60C");
        AddRow("Failed:", m.FailureCount.ToString(CultureInfo.InvariantCulture), m.FailureCount > 0 ? "#E74856" : "#CCCCCC");

        var rate = m.TotalCommands > 0
            ? $"{(double)m.SuccessCount / m.TotalCommands * 100:F1}%"
            : "N/A";
        AddRow("Success rate:", rate, "#3A96DD");
        AddRow("Total time:", FormatDuration(m.TotalDuration), "#CCCCCC");
        AddRow("Average time:", FormatDuration(m.AverageDuration), "#CCCCCC");

        if (m.SlowestCommand is not null)
        {
            AddRow("Slowest:", $"{FormatDuration(m.SlowestDuration!.Value)} ({m.SlowestCommand})", "#F9F1A5");
        }

        if (m.LastCommand is not null)
        {
            var exitColor = m.LastExitCode == 0 ? "#16C60C" : "#E74856";
            AddRow("Last command:", m.LastCommand, "#CCCCCC");
            AddRow("Last exit code:", m.LastExitCode?.ToString(CultureInfo.InvariantCulture) ?? "?", exitColor);
            if (m.LastDuration.HasValue)
                AddRow("Last duration:", FormatDuration(m.LastDuration.Value), "#CCCCCC");
        }

        OutputText = sb.ToString();
    }

    // ─── Background jobs ──────────────────────────────────────────────────────

    private void RunBackgroundJob(string cmd, string wd)
    {
        var jobId = Interlocked.Increment(ref _nextJobId);
        var job = new BackgroundJob
        {
            Id = jobId,
            Command = cmd,
            WorkingDirectory = wd,
            Shell = Shell,
        };

        BackgroundJobs.Add(job);

        OutputSegments.Clear();
        OutputText = $"[{jobId}] Started background job: {cmd}\n";
        OutputSegments.Add(new AnsiSegment($"[{jobId}] ", "#F9F1A5"));
        OutputSegments.Add(new AnsiSegment($"Started background job: {cmd}\n", "#16C60C"));

        _suppressSuggestionRefresh = true;
        CommandText = string.Empty;
        _suppressSuggestionRefresh = false;

        Task.Run(async () =>
        {
            var sb = new StringBuilder();
            try
            {
                var expandedCmd = _aliasService.ExpandAliases(cmd, Shell);
                var startInfo = BuildStartInfo(expandedCmd, wd);
                using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

                job.Process = process;

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data is not null) lock (sb) sb.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data is not null) lock (sb) sb.AppendLine(e.Data);
                };

                if (!process.Start())
                {
                    job.Status = JobStatus.Failed;
                    job.EndedAt = DateTimeOffset.Now;
                    return;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                job.ExitCode = process.ExitCode;
                job.Status = process.ExitCode == 0 ? JobStatus.Completed : JobStatus.Failed;
            }
            catch
            {
                job.Status = JobStatus.Failed;
            }
            finally
            {
                job.Output = sb.ToString();
                job.EndedAt = DateTimeOffset.Now;
                job.Process = null;

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    // Notify that job completed – only if this tab is active
                    if (IsActive)
                    {
                        var statusText = job.Status == JobStatus.Completed ? "completed" : "failed";
                        var color = job.Status == JobStatus.Completed ? "#16C60C" : "#E74856";
                        OutputSegments.Add(new AnsiSegment($"\n[{job.Id}] Job {statusText}: {job.Command}\n", color));
                        OutputText += $"\n[{job.Id}] Job {statusText}: {job.Command}\n";
                    }
                });
            }
        });
    }

    private void KillJob(string idStr)
    {
        OutputSegments.Clear();
        if (!int.TryParse(idStr, out int jobId))
        {
            OutputText = $"Invalid job ID: {idStr}\n";
            OutputSegments.Add(new AnsiSegment($"Invalid job ID: {idStr}\n", "#E74856"));
            return;
        }

        var job = BackgroundJobs.FirstOrDefault(j => j.Id == jobId);
        if (job is null)
        {
            OutputText = $"Job {jobId} not found.\n";
            OutputSegments.Add(new AnsiSegment($"Job {jobId} not found.\n", "#E74856"));
            return;
        }

        try
        {
            job.Process?.Kill(true);
            job.Status = JobStatus.Cancelled;
            job.EndedAt = DateTimeOffset.Now;
            OutputText = $"Job {jobId} killed.\n";
            OutputSegments.Add(new AnsiSegment($"Job {jobId} killed.\n", "#F9F1A5"));
        }
        catch (Exception ex)
        {
            OutputText = $"Failed to kill job {jobId}: {ex.Message}\n";
            OutputSegments.Add(new AnsiSegment($"Failed to kill job {jobId}: {ex.Message}\n", "#E74856"));
        }
    }

    // ─── ANSI output rendering ────────────────────────────────────────────────

    private void UpdateOutputSegments(string rawOutput)
    {
        var segments = AnsiParser.Parse(rawOutput);
        OutputSegments.Clear();
        foreach (var seg in segments)
            OutputSegments.Add(seg);
    }

    // ─── Process helpers ──────────────────────────────────────────────────────

    private static string WrapCommandForCwdTracking(ShellKind shell, string command)
    {
        if (shell == ShellKind.Cmd)
            return $"{command} & echo {CwdMarker}%CD%";
        return $"{command}; Write-Host '{CwdMarker}' (Get-Location).Path";
    }

    private static string? ExtractFinalCwd(StringBuilder sb)
    {
        var text = sb.ToString();
        var markerIdx = text.LastIndexOf(CwdMarker, StringComparison.Ordinal);
        if (markerIdx < 0) return null;

        var afterMarker = text[(markerIdx + CwdMarker.Length)..];
        var lineEnd = afterMarker.IndexOfAny(['\r', '\n']);
        var cwdRaw = (lineEnd >= 0 ? afterMarker[..lineEnd] : afterMarker).Trim();

        var lineStart = text.LastIndexOf('\n', markerIdx);
        lineStart = lineStart < 0 ? markerIdx : lineStart;
        var nextLine = text.IndexOf('\n', markerIdx);
        var removeEnd = nextLine >= 0 ? nextLine + 1 : text.Length;

        sb.Remove(lineStart, removeEnd - lineStart);

        return string.IsNullOrWhiteSpace(cwdRaw) ? null : cwdRaw;
    }

    private ProcessStartInfo BuildStartInfo(string command, string wd)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _shellExecutablePath,
            Arguments = string.IsNullOrWhiteSpace(_shellArguments)
                ? command
                : _shellArguments + " " + command,
            WorkingDirectory = wd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        if (Shell == ShellKind.PowerShell)
            startInfo.Environment["TERM"] = "xterm-256color";

        return startInfo;
    }

    private static string GetDefaultExecutable(ShellKind shell)
        => shell == ShellKind.Cmd ? "cmd.exe" : "powershell.exe";

    private static string GetDefaultArguments(ShellKind shell)
        => shell == ShellKind.Cmd ? "/c" : "-NoLogo -Command";
}
