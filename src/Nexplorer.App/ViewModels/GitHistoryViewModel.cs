using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexplorer.App.Collections;
using Nexplorer.App.Services;

namespace Nexplorer.App.ViewModels;

/// <summary>
/// ViewModel for the Git History bottom panel.
/// </summary>
public sealed partial class GitHistoryViewModel : ObservableObject, IDisposable
{
    private CancellationTokenSource? _loadCts;
    private bool _suppressBranchChange;

    public ObservableCollection<GitLogEntry> Entries { get; } = new();
    public ObservableCollection<string> Branches { get; } = new();
    public ObservableCollection<string> FilteredBranches { get; } = new();
    public ObservableCollection<GitChangedFile> ChangedFiles { get; } = new();

    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _title = "Git History";
    [ObservableProperty] private string _commitDiff = string.Empty;
    [ObservableProperty] private bool _isDiffVisible;
    [ObservableProperty] private bool _isFileDiffOpen;
    [ObservableProperty] private string _diffFileName = string.Empty;
    public RangeObservableCollection<DiffLine> DiffOldLines { get; } = new();
    public RangeObservableCollection<DiffLine> DiffNewLines { get; } = new();
    [ObservableProperty] private GitLogEntry? _selectedEntry;
    [ObservableProperty] private string? _currentDirectory;
    [ObservableProperty] private string? _currentFilePath;
    [ObservableProperty] private bool _isGitRepo;
    [ObservableProperty] private string? _selectedBranch;
    [ObservableProperty] private string _branchFilter = string.Empty;
    [ObservableProperty] private bool _isBranchDropdownOpen;

    partial void OnBranchFilterChanged(string value)
    {
        ApplyBranchFilter();
    }

    private void ApplyBranchFilter()
    {
        FilteredBranches.Clear();
        var filter = BranchFilter?.Trim() ?? string.Empty;
        foreach (var b in Branches)
        {
            if (string.IsNullOrEmpty(filter) ||
                b.Contains(filter, StringComparison.OrdinalIgnoreCase))
                FilteredBranches.Add(b);
        }
    }

    partial void OnSelectedEntryChanged(GitLogEntry? value)
    {
        // Close any open file diff when switching commits
        ResetFileDiff();

        if (value is not null)
        {
            _ = LoadCommitDiffAsync(value.Hash);
            _ = LoadChangedFilesAsync(value.Hash);
        }
        else
        {
            CommitDiff = string.Empty;
            ChangedFiles.Clear();
            IsDiffVisible = false;
        }
    }

    partial void OnSelectedBranchChanged(string? value)
    {
        if (_suppressBranchChange) return;
        if (CurrentDirectory is not null && value is not null)
            _ = LoadLogForBranchAsync();
    }

    private async Task LoadLogForBranchAsync()
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        IsLoading = true;
        SelectedEntry = null;
        CommitDiff = string.Empty;
        IsDiffVisible = false;

        try
        {
            var entries = await GitHistoryService.GetLogAsync(
                CurrentDirectory!, CurrentFilePath, 200, SelectedBranch, ct);
            ct.ThrowIfCancellationRequested();

            Entries.Clear();
            foreach (var entry in entries)
                Entries.Add(entry);

            Title = CurrentFilePath is not null
                ? $"Git History — {System.IO.Path.GetFileName(CurrentFilePath)} ({SelectedBranch})"
                : $"Git History — {SelectedBranch}";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Entries.Clear();
            Title = $"Git History — Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Opens git history for the given directory, optionally for a specific file.
    /// </summary>
    public async Task ShowHistoryAsync(string directory, string? filePath = null)
    {
        // Cancel any in-flight load
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        CurrentDirectory = directory;
        CurrentFilePath = filePath;
        IsGitRepo = GitHistoryService.IsGitRepository(directory);

        if (!IsGitRepo)
        {
            Title = "Git History — Not a Git repository";
            Entries.Clear();
            IsVisible = true;
            IsLoading = false;
            return;
        }

        Title = filePath is not null
            ? $"Git History — {System.IO.Path.GetFileName(filePath)}"
            : "Git History";

        IsVisible = true;
        IsLoading = true;
        SelectedEntry = null;
        CommitDiff = string.Empty;
        IsDiffVisible = false;

        try
        {
            // Load branches
            var branches = await GitHistoryService.GetBranchesAsync(directory, ct);
            ct.ThrowIfCancellationRequested();

            _suppressBranchChange = true;
            Branches.Clear();
            foreach (var b in branches)
                Branches.Add(b);
            SelectedBranch = branches.Count > 0 ? branches[0] : null;
            _suppressBranchChange = false;
            BranchFilter = string.Empty;
            ApplyBranchFilter();

            // Load log for the current (default) branch
            var entries = await GitHistoryService.GetLogAsync(
                directory, filePath, 200, SelectedBranch, ct);
            ct.ThrowIfCancellationRequested();

            Entries.Clear();
            foreach (var entry in entries)
                Entries.Add(entry);

            if (SelectedBranch is not null)
                Title = filePath is not null
                    ? $"Git History — {System.IO.Path.GetFileName(filePath)} ({SelectedBranch})"
                    : $"Git History — {SelectedBranch}";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Entries.Clear();
            Title = $"Git History — Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadChangedFilesAsync(string hash)
    {
        if (string.IsNullOrWhiteSpace(CurrentDirectory)) return;

        ChangedFiles.Clear();
        try
        {
            var files = await GitHistoryService.GetCommitFilesAsync(CurrentDirectory, hash);
            foreach (var f in files)
                ChangedFiles.Add(f);
        }
        catch { /* ignore */ }
    }

    private async Task LoadCommitDiffAsync(string hash)
    {
        if (string.IsNullOrWhiteSpace(CurrentDirectory)) return;

        IsDiffVisible = true;
        CommitDiff = "Loading…";

        try
        {
            var diff = await GitHistoryService.GetCommitDiffAsync(CurrentDirectory, hash);
            CommitDiff = diff;
        }
        catch (Exception ex)
        {
            CommitDiff = $"Error loading diff: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
        _loadCts?.Cancel();
        SelectedEntry = null;
        CommitDiff = string.Empty;
        IsDiffVisible = false;
        ResetFileDiff();
    }

    [RelayCommand]
    private void CloseDiff()
    {
        IsDiffVisible = false;
        CommitDiff = string.Empty;
        SelectedEntry = null;
        ResetFileDiff();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (CurrentDirectory is not null)
            await ShowHistoryAsync(CurrentDirectory, CurrentFilePath);
    }

    [RelayCommand]
    private void CopyHash()
    {
        if (SelectedEntry is not null)
            Clipboard.SetText(SelectedEntry.Hash);
    }

    [RelayCommand]
    private void ShowFileDiff(GitChangedFile? file)
    {
        if (file is null || SelectedEntry is null || CurrentDirectory is null) return;
        _ = LoadSideBySideDiffAsync(SelectedEntry.Hash, file);
    }

    private async Task LoadSideBySideDiffAsync(string hash, GitChangedFile file)
    {
        if (string.IsNullOrWhiteSpace(CurrentDirectory)) return;

        DiffFileName = file.Path;
        IsFileDiffOpen = true;
        DiffOldLines.Clear();
        DiffNewLines.Clear();

        try
        {
            string oldText, newText;

            if (file.Status == FileChangeStatus.Added)
            {
                oldText = string.Empty;
                newText = await GitHistoryService.GetFileAtCommitAsync(CurrentDirectory, hash, file.Path) ?? string.Empty;
            }
            else if (file.Status == FileChangeStatus.Deleted)
            {
                oldText = await GitHistoryService.GetFileAtCommitAsync(CurrentDirectory, $"{hash}~1", file.Path) ?? string.Empty;
                newText = string.Empty;
            }
            else
            {
                oldText = await GitHistoryService.GetFileAtCommitAsync(CurrentDirectory, $"{hash}~1", file.Path) ?? string.Empty;
                newText = await GitHistoryService.GetFileAtCommitAsync(CurrentDirectory, hash, file.Path) ?? string.Empty;
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

    private void ResetFileDiff()
    {
        IsFileDiffOpen = false;
        DiffOldLines.Clear();
        DiffNewLines.Clear();
        DiffFileName = string.Empty;
    }

    [RelayCommand]
    private void CloseFileDiff() => ResetFileDiff();

    [RelayCommand]
    private void SelectBranch(string? branch)
    {
        if (branch is null) return;
        IsBranchDropdownOpen = false;
        BranchFilter = string.Empty;
        SelectedBranch = branch;
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}
