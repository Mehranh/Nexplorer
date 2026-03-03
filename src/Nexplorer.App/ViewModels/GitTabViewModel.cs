using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexplorer.App.Collections;
using Nexplorer.App.Services;

namespace Nexplorer.App.ViewModels;

/// <summary>
/// ViewModel for the Git source-control bottom-panel tab.
/// </summary>
public sealed partial class GitTabViewModel : ObservableObject
{
    public ObservableCollection<GitStatusEntry> StagedFiles { get; } = new();
    public ObservableCollection<GitStatusEntry> ChangedFiles { get; } = new();

    [ObservableProperty] private string? _currentDirectory;
    [ObservableProperty] private bool _isGitRepo;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _currentBranch;
    [ObservableProperty] private string _commitMessage = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _hasStatusMessage;

    // ── Side-by-side diff ────────────────────────────────────────────────

    [ObservableProperty] private bool _isDiffOpen;
    [ObservableProperty] private string _diffFileName = string.Empty;
    public RangeObservableCollection<DiffLine> DiffOldLines { get; } = new();
    public RangeObservableCollection<DiffLine> DiffNewLines { get; } = new();
    [ObservableProperty] private GitStatusEntry? _selectedFile;

    partial void OnSelectedFileChanged(GitStatusEntry? value)
    {
        if (value is not null)
            _ = LoadFileDiffAsync(value);
        else
            CloseDiff();
    }

    // ── Counts for badges ────────────────────────────────────────────────

    public int StagedCount => StagedFiles.Count;
    public int ChangedCount => ChangedFiles.Count;
    public int TotalCount => StagedCount + ChangedCount;

    private void NotifyCounts()
    {
        OnPropertyChanged(nameof(StagedCount));
        OnPropertyChanged(nameof(ChangedCount));
        OnPropertyChanged(nameof(TotalCount));
    }

    // ── Load / Refresh ───────────────────────────────────────────────────

    public async Task LoadAsync(string directory)
    {
        CurrentDirectory = directory;
        IsGitRepo = GitHistoryService.IsGitRepository(directory);

        if (!IsGitRepo)
        {
            StagedFiles.Clear();
            ChangedFiles.Clear();
            CurrentBranch = null;
            NotifyCounts();
            return;
        }

        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (CurrentDirectory is null || !IsGitRepo) return;

        IsLoading = true;
        try
        {
            CurrentBranch = await GitService.GetCurrentBranchAsync(CurrentDirectory);

            var entries = await GitService.GetStatusAsync(CurrentDirectory);

            StagedFiles.Clear();
            ChangedFiles.Clear();

            foreach (var e in entries)
            {
                if (e.Area == GitFileArea.Staged)
                    StagedFiles.Add(e);
                else
                    ChangedFiles.Add(e);
            }

            NotifyCounts();
        }
        catch { /* git unavailable */ }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Side-by-side diff ────────────────────────────────────────────────

    private async Task LoadFileDiffAsync(GitStatusEntry entry)
    {
        if (CurrentDirectory is null) return;

        DiffFileName = entry.Path;
        IsDiffOpen = true;
        DiffOldLines.Clear();
        DiffNewLines.Clear();

        try
        {
            string oldText, newText;

            if (entry.Status == GitChangeKind.Untracked)
            {
                oldText = string.Empty;
                var fullPath = Path.Combine(CurrentDirectory, entry.Path);
                newText = File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath) : string.Empty;
            }
            else if (entry.Status == GitChangeKind.Deleted)
            {
                oldText = await GitService.GetFileAtHeadAsync(CurrentDirectory, entry.Path) ?? string.Empty;
                newText = string.Empty;
            }
            else if (entry.Area == GitFileArea.Staged)
            {
                oldText = await GitService.GetFileAtHeadAsync(CurrentDirectory, entry.Path) ?? string.Empty;
                newText = await GitService.GetFileAtIndexAsync(CurrentDirectory, entry.Path) ?? string.Empty;
            }
            else
            {
                oldText = await GitService.GetFileAtHeadAsync(CurrentDirectory, entry.Path) ?? string.Empty;
                var fullPath = Path.Combine(CurrentDirectory, entry.Path);
                newText = File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath) : string.Empty;
            }

            var (oldLines, newLines) = await Task.Run(() => DiffService.ComputeSideBySide(oldText, newText));
            DiffOldLines.AddRange(oldLines);
            DiffNewLines.AddRange(newLines);
        }
        catch (Exception ex)
        {
            DiffOldLines.Add(new DiffLine($"Error: {ex.Message}", DiffLineKind.Removed, null));
        }
    }

    [RelayCommand]
    private void CloseDiff()
    {
        IsDiffOpen = false;
        DiffOldLines.Clear();
        DiffNewLines.Clear();
        DiffFileName = string.Empty;
        SelectedFile = null;
    }

    // ── Stage / Unstage ──────────────────────────────────────────────────

    [RelayCommand]
    private async Task StageFileAsync(GitStatusEntry? entry)
    {
        if (entry is null || CurrentDirectory is null) return;
        await GitService.StageFileAsync(CurrentDirectory, entry.Path);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task UnstageFileAsync(GitStatusEntry? entry)
    {
        if (entry is null || CurrentDirectory is null) return;
        await GitService.UnstageFileAsync(CurrentDirectory, entry.Path);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task StageAllAsync()
    {
        if (CurrentDirectory is null) return;
        await GitService.StageAllAsync(CurrentDirectory);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task UnstageAllAsync()
    {
        if (CurrentDirectory is null) return;
        await GitService.UnstageAllAsync(CurrentDirectory);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DiscardFileAsync(GitStatusEntry? entry)
    {
        if (entry is null || CurrentDirectory is null) return;
        if (!NotificationService.Confirm(
            $"Discard changes to \"{entry.Path}\"?\nThis cannot be undone.",
            "Discard Changes")) return;

        if (entry.Status == GitChangeKind.Untracked)
            await GitService.CleanUntrackedFileAsync(CurrentDirectory, entry.Path);
        else
            await GitService.DiscardFileAsync(CurrentDirectory, entry.Path);
        await RefreshAsync();
    }

    // ── Multi-select revert ──────────────────────────────────────────────

    private List<GitStatusEntry> _selectedChangedFiles = new();
    public List<GitStatusEntry> SelectedChangedFiles
    {
        get => _selectedChangedFiles;
        set { _selectedChangedFiles = value; OnPropertyChanged(nameof(HasSelectedChanges)); }
    }

    public bool HasSelectedChanges => _selectedChangedFiles.Count > 0;

    [RelayCommand]
    private async Task DiscardSelectedAsync()
    {
        if (CurrentDirectory is null || _selectedChangedFiles.Count == 0) return;

        var count = _selectedChangedFiles.Count;
        if (!NotificationService.Confirm(
            $"Discard changes to {count} file(s)?\nThis cannot be undone.",
            "Discard Changes")) return;

        foreach (var entry in _selectedChangedFiles.ToList())
        {
            if (entry.Status == GitChangeKind.Untracked)
                await GitService.CleanUntrackedFileAsync(CurrentDirectory, entry.Path);
            else
                await GitService.DiscardFileAsync(CurrentDirectory, entry.Path);
        }
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task DiscardAllAsync()
    {
        if (CurrentDirectory is null || ChangedFiles.Count == 0) return;

        if (!NotificationService.Confirm(
            $"Discard ALL {ChangedFiles.Count} change(s)?\nThis cannot be undone.",
            "Discard All Changes")) return;

        await GitService.DiscardAllAsync(CurrentDirectory);
        await RefreshAsync();
    }

    // ── Commit ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CommitAsync()
    {
        if (CurrentDirectory is null || string.IsNullOrWhiteSpace(CommitMessage)) return;
        if (StagedFiles.Count == 0)
        {
            ShowStatus("No staged files to commit.");
            return;
        }

        IsLoading = true;
        try
        {
            var result = await GitService.CommitAsync(CurrentDirectory, CommitMessage.Trim());
            CommitMessage = string.Empty;
            ShowStatus(result?.Trim() ?? "Committed.");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            ShowStatus($"Commit failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Push / Pull / Fetch / Stash ──────────────────────────────────────

    [RelayCommand]
    private async Task PushAsync()
    {
        if (CurrentDirectory is null) return;
        IsLoading = true;
        try
        {
            var result = await GitService.PushAsync(CurrentDirectory);
            ShowStatus(result?.Trim() ?? "Pushed.");
            await RefreshAsync();
        }
        catch (Exception ex) { ShowStatus($"Push failed: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task PullAsync()
    {
        if (CurrentDirectory is null) return;
        IsLoading = true;
        try
        {
            var result = await GitService.PullAsync(CurrentDirectory);
            ShowStatus(result?.Trim() ?? "Pulled.");
            await RefreshAsync();
        }
        catch (Exception ex) { ShowStatus($"Pull failed: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task FetchAsync()
    {
        if (CurrentDirectory is null) return;
        IsLoading = true;
        try
        {
            var result = await GitService.FetchAsync(CurrentDirectory);
            ShowStatus(string.IsNullOrWhiteSpace(result) ? "Fetched." : result.Trim());
            await RefreshAsync();
        }
        catch (Exception ex) { ShowStatus($"Fetch failed: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task StashAsync()
    {
        if (CurrentDirectory is null) return;
        IsLoading = true;
        try
        {
            var result = await GitService.StashAsync(CurrentDirectory);
            ShowStatus(result?.Trim() ?? "Stashed.");
            await RefreshAsync();
        }
        catch (Exception ex) { ShowStatus($"Stash failed: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task StashPopAsync()
    {
        if (CurrentDirectory is null) return;
        IsLoading = true;
        try
        {
            var result = await GitService.StashPopAsync(CurrentDirectory);
            ShowStatus(result?.Trim() ?? "Stash popped.");
            await RefreshAsync();
        }
        catch (Exception ex) { ShowStatus($"Stash pop failed: {ex.Message}"); }
        finally { IsLoading = false; }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void ShowStatus(string msg)
    {
        StatusMessage = msg;
        HasStatusMessage = true;
    }

    [RelayCommand]
    private void DismissStatus()
    {
        HasStatusMessage = false;
        StatusMessage = string.Empty;
    }
}
