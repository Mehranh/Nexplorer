using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexplorer.App.Collections;
using Nexplorer.App.Services;
using Nexplorer.Core;

namespace Nexplorer.App.ViewModels;

public enum ViewMode
{
    Details,
    LargeIcons,
    SmallIcons,
    List,
    Tiles
}

public sealed partial class PaneViewModel : ObservableObject
{
    private CancellationTokenSource? _navigateCts;

    private readonly Stack<string> _backStack    = new();
    private readonly Stack<string> _forwardStack = new();

    public PaneViewModel(string initialPath)
    {
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ApplySortDescriptions();
        _currentPath = initialPath;
    }

    // ─── Core properties ──────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathParts))]
    private string _currentPath = string.Empty;

    [ObservableProperty] private string             _statusText  = string.Empty;
    [ObservableProperty] private FileItemViewModel? _selectedItem;
    [ObservableProperty] private bool               _isActive;

    public bool CanNavigateBack    => _backStack.Count    > 0;
    public bool CanNavigateForward => _forwardStack.Count > 0;

    public IReadOnlyList<string> PathParts =>
        CurrentPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

    // ─── Items & view ─────────────────────────────────────────────────────────

    public RangeObservableCollection<FileItemViewModel> Items    { get; } = new();
    public ICollectionView                              ItemsView { get; }

    // ─── Multi-selection (set by code-behind from ListView.SelectionChanged) ──

    private List<FileItemViewModel> _selectedItems = new();
    public  List<FileItemViewModel>  SelectedItems
    {
        get => _selectedItems;
        set { _selectedItems = value; UpdateStatusFromSelection(); NotifySelectionCommands(); }
    }

    /// <summary>True when at least one item is selected.</summary>
    public bool HasSelection => SelectedItem is not null || _selectedItems.Count > 0;

    partial void OnSelectedItemChanged(FileItemViewModel? value) => NotifySelectionCommands();

    private void NotifySelectionCommands()
    {
        OnPropertyChanged(nameof(HasSelection));
        OpenSelectedCommand.NotifyCanExecuteChanged();
        OpenWithCommand.NotifyCanExecuteChanged();
        CutSelectedCommand.NotifyCanExecuteChanged();
        CopySelectedCommand.NotifyCanExecuteChanged();
        CopyPathCommand.NotifyCanExecuteChanged();
        BeginRenameCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
    }
    // ─── View mode ───────────────────────────────────────────────────────────────────

    [ObservableProperty] private ViewMode _viewMode = ViewMode.Details;

    [RelayCommand]
    private void SetViewMode(string mode)
    {
        if (Enum.TryParse<ViewMode>(mode, out var parsed))
            ViewMode = parsed;
    }
    // ─── Sorting ──────────────────────────────────────────────────────────────

    [ObservableProperty] private string _sortColumn    = nameof(FileItemViewModel.Name);
    [ObservableProperty] private bool   _sortDescending = false;

    // ─── Quick Filter ─────────────────────────────────────────────────────────

    [ObservableProperty] private string  _filterText    = string.Empty;
    [ObservableProperty] private bool    _isFilterVisible;

    partial void OnFilterTextChanged(string value)
    {
        ItemsView.Filter = string.IsNullOrWhiteSpace(value)
            ? null
            : obj => obj is FileItemViewModel item
                  && item.Name.Contains(value, StringComparison.OrdinalIgnoreCase);
        ItemsView.Refresh();
        var total   = Items.Count;
        var visible = ItemsView.Cast<object>().Count();
        if (!string.IsNullOrWhiteSpace(value))
            StatusText = $"{visible:n0} of {total:n0} items (filtered)";
        else
            StatusText = $"{total:n0} {(total == 1 ? "item" : "items")}";
    }

    [RelayCommand]
    private void ToggleFilter()
    {
        IsFilterVisible = !IsFilterVisible;
        if (!IsFilterVisible)
        {
            FilterText = string.Empty;
        }
    }

    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = string.Empty;
        IsFilterVisible = false;
    }

    // ─── Preview pane toggle ──────────────────────────────────────────────────

    [ObservableProperty] private bool _isPreviewVisible;

    [RelayCommand]
    private void TogglePreview() => IsPreviewVisible = !IsPreviewVisible;

    // ─── File watcher (auto-refresh) ──────────────────────────────────────────

    private FileSystemWatcher? _watcher;

    private void StartWatcher(string path)
    {
        StopWatcher();
        try
        {
            _watcher = new FileSystemWatcher(path)
            {
                NotifyFilter        = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents   = true,
            };
            _watcher.Created += OnFsChanged;
            _watcher.Deleted += OnFsChanged;
            _watcher.Renamed += OnFsChanged;
        }
        catch { /* insufficient permissions */ }
    }

    private void StopWatcher()
    {
        if (_watcher is null) return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnFsChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: cancel any pending refresh
        _refreshTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _refreshTimer = new Timer(_ =>
        {
            Application.Current.Dispatcher.Invoke(() => _ = GoToAsync(CurrentPath, pushHistory: false));
        }, null, 500, Timeout.Infinite);
    }

    private Timer? _refreshTimer;

    [RelayCommand]
    public void SortBy(string column)
    {
        if (SortColumn == column)
            SortDescending = !SortDescending;
        else
        {
            SortColumn     = column;
            SortDescending = false;
        }
        ApplySortDescriptions();
    }

    private void ApplySortDescriptions()
    {
        var dir = SortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        ItemsView.SortDescriptions.Clear();
        ItemsView.SortDescriptions.Add(
            new SortDescription(nameof(FileItemViewModel.IsDirectory), ListSortDirection.Descending));
        ItemsView.SortDescriptions.Add(new SortDescription(SortColumn, dir));
    }

    // ─── Navigation commands ─────────────────────────────────────────────────

    [RelayCommand]
    private Task NavigateAsync() => GoToAsync(CurrentPath, pushHistory: false);

    [RelayCommand(CanExecute = nameof(CanNavigateBack))]
    private Task NavigateBackAsync()
    {
        if (!_backStack.TryPop(out var prev)) return Task.CompletedTask;
        _forwardStack.Push(CurrentPath);
        return GoToAsync(prev, pushHistory: false);
    }

    [RelayCommand(CanExecute = nameof(CanNavigateForward))]
    private Task NavigateForwardAsync()
    {
        if (!_forwardStack.TryPop(out var next)) return Task.CompletedTask;
        _backStack.Push(CurrentPath);
        return GoToAsync(next, pushHistory: false);
    }

    [RelayCommand]
    private Task NavigateUpAsync()
    {
        var parent = Directory.GetParent(CurrentPath)?.FullName;
        return parent is null ? Task.CompletedTask : GoToAsync(parent, pushHistory: true);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenSelected()
    {
        if (SelectedItem is null) return;
        if (SelectedItem.IsDirectory)
            _ = GoToAsync(SelectedItem.FullPath, pushHistory: true);
        else
            TryShellExecute(SelectedItem.FullPath, "open");
    }

    // ─── Core navigation ──────────────────────────────────────────────────────

    public async Task GoToAsync(string path, bool pushHistory)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            StatusText = "Folder not found";
            return;
        }

        if (pushHistory && !string.IsNullOrWhiteSpace(CurrentPath))
        {
            _backStack.Push(CurrentPath);
            _forwardStack.Clear();
            OnPropertyChanged(nameof(CanNavigateBack));
            OnPropertyChanged(nameof(CanNavigateForward));
            NavigateBackCommand.NotifyCanExecuteChanged();
            NavigateForwardCommand.NotifyCanExecuteChanged();
        }

        CurrentPath   = path;
        SelectedItems = new();
        StartWatcher(path);

        _navigateCts?.Cancel();
        _navigateCts = new CancellationTokenSource();
        var token = _navigateCts.Token;

        StatusText = "Loading…";
        Items.Clear();

        try
        {
            var batch = new List<FileItemViewModel>(256);
            var sw    = Stopwatch.StartNew();

            await Task.Run(() =>
            {
                foreach (var item in DirectoryEnumerator.Enumerate(path, token))
                {
                    token.ThrowIfCancellationRequested();
                    batch.Add(new FileItemViewModel(item));

                    if (batch.Count >= 256)
                    {
                        var toAdd = batch.ToArray(); batch.Clear();
                        Application.Current.Dispatcher.Invoke(() => Items.AddRange(toAdd));
                    }
                }
                if (batch.Count > 0)
                {
                    var toAdd = batch.ToArray();
                    Application.Current.Dispatcher.Invoke(() => Items.AddRange(toAdd));
                }
            }, token);

            sw.Stop();
            StatusText = $"{Items.Count:n0} {(Items.Count == 1 ? "item" : "items")}  ·  {sw.ElapsedMilliseconds} ms";
        }
        catch (OperationCanceledException) { StatusText = "Canceled"; }
        catch (Exception ex)               { StatusText = ex.Message; }
    }

    // ─── Rename ───────────────────────────────────────────────────────────────

    /// <summary>Raised when the view should focus the rename TextBox for an item.</summary>
    public event EventHandler<FileItemViewModel>? RenameStarted;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void BeginRename()
    {
        var item = SelectedItem;
        if (item is null) return;
        item.BeginRename();
        RenameStarted?.Invoke(this, item);
    }

    public void CommitRename(FileItemViewModel item)
    {
        if (!item.IsEditing) return;
        var newName = item.EditingName.Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name)
        {
            item.CancelRename();
            return;
        }
        try
        {
            FileOperationService.Rename(item.FullPath, newName);
            item.CommitRename(newName);
        }
        catch (Exception ex)
        {
            item.CancelRename();
            StatusText = ex.Message;
        }
    }

    // ─── New folder ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task NewFolderAsync()
    {
        string createdPath;
        try { createdPath = FileOperationService.CreateFolder(CurrentPath); }
        catch (Exception ex) { StatusText = ex.Message; return; }

        await GoToAsync(CurrentPath, pushHistory: false);

        var folderName = Path.GetFileName(createdPath);
        var newItem    = Items.FirstOrDefault(i => i.IsDirectory && i.Name == folderName);
        if (newItem is null) return;

        SelectedItem = newItem;
        await Task.Delay(60);
        newItem.BeginRename();
        RenameStarted?.Invoke(this, newItem);
    }

    // ─── New file ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task NewFileAsync()
    {
        string createdPath;
        try { createdPath = FileOperationService.CreateFile(CurrentPath); }
        catch (Exception ex) { StatusText = ex.Message; return; }

        await GoToAsync(CurrentPath, pushHistory: false);

        var fileName = Path.GetFileName(createdPath);
        var newItem  = Items.FirstOrDefault(i => !i.IsDirectory && i.Name == fileName);
        if (newItem is null) return;

        SelectedItem = newItem;
        await Task.Delay(60);
        newItem.BeginRename();
        RenameStarted?.Invoke(this, newItem);
    }

    // ─── Delete ───────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteSelected()
    {
        var targets = SelectedItems.Count > 0 ? SelectedItems.ToList()
                    : SelectedItem is not null  ? new List<FileItemViewModel> { SelectedItem }
                    : new List<FileItemViewModel>();
        if (targets.Count == 0) return;

        var msg = targets.Count == 1
            ? $"Move '{targets[0].Name}' to Recycle Bin?"
            : $"Move {targets.Count} items to Recycle Bin?";

        if (!NotificationService.Instance.Confirm(msg, "Delete"))
            return;

        FileOperationService.RecycleAll(targets.Select(f => f.FullPath));
        _ = GoToAsync(CurrentPath, pushHistory: false);
    }

    // ─── Cut / Copy / Paste ───────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CutSelected()
    {
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;
        FileClipboardService.Set(paths, ClipboardAction.Cut);
        StatusText = $"Cut {paths.Count} item(s)";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopySelected()
    {
        var paths = GetSelectedPaths();
        if (paths.Count == 0) return;
        FileClipboardService.Set(paths, ClipboardAction.Copy);
        System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, paths));
        StatusText = $"Copied {paths.Count} item(s)";
    }

    [RelayCommand]
    private async Task PasteAsync()
    {
        if (!FileClipboardService.HasFiles) return;
        var (paths, action) = FileClipboardService.Current!.Value;
        StatusText = "Pasting…";
        var progress = new Progress<string>(msg => StatusText = msg);
        try
        {
            if (action == ClipboardAction.Cut)
            {
                await FileOperationService.MoveAsync(paths, CurrentPath, progress);
                FileClipboardService.Clear();
            }
            else
            {
                await FileOperationService.CopyAsync(paths, CurrentPath, progress);
            }
        }
        catch (Exception ex) { StatusText = ex.Message; return; }
        await GoToAsync(CurrentPath, pushHistory: false);
    }

    // ─── Copy path ────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyPath()
    {
        var paths = GetSelectedPaths();
        if (paths.Count == 0 && !string.IsNullOrWhiteSpace(CurrentPath))
            paths = new List<string> { CurrentPath };
        if (paths.Count == 0) return;
        System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, paths));
        StatusText = "Path copied";
    }

    // ─── Open terminal here ───────────────────────────────────────────────────

    [RelayCommand]
    private void OpenTerminalHere()
    {
        foreach (var (exe, args) in new[]
        {
            ("wt.exe",         $"-d \"{CurrentPath}\""),
            ("powershell.exe", $"-NoExit -Command \"Set-Location '{CurrentPath}'\""),
        })
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = exe, Arguments = args, UseShellExecute = true });
                return;
            }
            catch { /* try next */ }
        }
    }

    // ─── Properties dialog ────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenProperties()
    {
        var target = SelectedItem?.FullPath ?? CurrentPath;
        TryShellExecute(target, "properties");
    }

    // ─── Open with ────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void OpenWith()
    {
        if (SelectedItem is null || SelectedItem.IsDirectory) return;
        TryShellExecute(SelectedItem.FullPath, "openas");
    }

    // ─── Select all ──────────────────────────────────────────────────────────

    /// <summary>Raised when the view should select all items (Ctrl+A).</summary>
    public event EventHandler? SelectAllRequested;

    [RelayCommand]
    public void SelectAll() => SelectAllRequested?.Invoke(this, EventArgs.Empty);

    // ─── Invert selection ────────────────────────────────────────────────────

    /// <summary>Raised when the view should invert its selection.</summary>
    public event EventHandler? InvertSelectionRequested;

    [RelayCommand]
    public void InvertSelection() => InvertSelectionRequested?.Invoke(this, EventArgs.Empty);

    // ─── Status helper ────────────────────────────────────────────────────────

    private void UpdateStatusFromSelection()
    {
        if (_selectedItems.Count == 0)
        {
            var word = Items.Count == 1 ? "item" : "items";
            StatusText = $"{Items.Count:n0} {word}";
            return;
        }

        long bytes = _selectedItems
            .Where(i => !i.IsDirectory && i.SizeBytes.HasValue)
            .Sum(i => i.SizeBytes!.Value);

        var sz = bytes switch
        {
            0                        => string.Empty,
            < 1_024                  => $"  ·  {bytes} B",
            < 1_024 * 1_024          => $"  ·  {bytes / 1024.0:0.#} KB",
            < 1_024L * 1_024 * 1_024 => $"  ·  {bytes / (1024.0 * 1024):0.#} MB",
            _                        => $"  ·  {bytes / (1024.0 * 1024 * 1024):0.##} GB",
        };

        StatusText = $"{_selectedItems.Count:n0} selected{sz}  of  {Items.Count:n0} items";
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private List<string> GetSelectedPaths()
    {
        var list = SelectedItems.Select(i => i.FullPath).ToList();
        if (list.Count == 0 && SelectedItem is not null)
            list.Add(SelectedItem.FullPath);
        return list;
    }

    private static void TryShellExecute(string path, string verb)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = path,
                Verb            = verb,
                UseShellExecute = true,
            });
        }
        catch { /* swallow */ }
    }
}
