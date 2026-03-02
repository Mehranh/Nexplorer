using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileExplorer.App.Services;
using FileExplorer.App.Services.Settings;

namespace FileExplorer.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    // True when the left pane (tabs) is focused; false when right is focused
    private bool _activeIsLeft = true;
    private readonly List<string> _recentLocations = new();
    private const int MaxRecentLocations = 5;
    private PaneViewModel? _trackedLeftPane;
    private PaneViewModel? _trackedRightPane;

    /// <summary>Allows the view to trigger property-change notifications.</summary>
    public new void OnPropertyChanged(string? propertyName) => base.OnPropertyChanged(propertyName);

    public MainViewModel()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Use the most recent location from history as the starting path
        var savedRecent = App.SettingsService.Current.RecentLocations;
        var startPath = savedRecent.FirstOrDefault(Directory.Exists) ?? home;

        LeftTabs  = new PaneTabsViewModel(startPath);
        RightTabs = new PaneTabsViewModel(startPath);

        LeftPane.IsActive  = true;
        RightPane.IsActive = false;

        // Initialize the terminal panel
        TerminalPanel = new TerminalPanelViewModel(startPath, History);
        SubscribeToActiveTab();

        // Forward active-tab changes from the terminal panel
        TerminalPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TerminalPanelViewModel.ActiveTab))
                SubscribeToActiveTab();
        };

        // Re-broadcast LeftPane / RightPane property changes when tabs switch
        LeftTabs.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PaneTabsViewModel.ActiveTab))
            {
                foreach (var t in LeftTabs.Tabs) t.Pane.IsActive = false;
                LeftPane.IsActive = _activeIsLeft;

                OnPropertyChanged(nameof(LeftPane));
                if (_activeIsLeft) OnPropertyChanged(nameof(ActivePane));
                OnPropertyChanged(nameof(TerminalPrompt));
                ResubscribeRecentTracking();
            }
        };
        RightTabs.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PaneTabsViewModel.ActiveTab))
            {
                foreach (var t in RightTabs.Tabs) t.Pane.IsActive = false;
                RightPane.IsActive = !_activeIsLeft;

                OnPropertyChanged(nameof(RightPane));
                if (!_activeIsLeft) OnPropertyChanged(nameof(ActivePane));
                OnPropertyChanged(nameof(TerminalPrompt));
                ResubscribeRecentTracking();
            }
        };

        HistoryView = CollectionViewSource.GetDefaultView(History);
        HistoryView.SortDescriptions.Add(
            new SortDescription(nameof(CommandHistoryEntry.Timestamp), ListSortDirection.Descending));
        HistoryView.Filter = FilterHistory;

        _ = LeftPane.GoToAsync(startPath,  pushHistory: false);
        _ = RightPane.GoToAsync(startPath, pushHistory: false);

        // Load recent locations from persisted settings
        _recentLocations.AddRange(App.SettingsService.Current.RecentLocations);
        FolderTree.SetRecentLocations(_recentLocations);

        // Track path changes on both panes for recent locations
        ResubscribeRecentTracking();
    }

    // ─── Pane / Tab accessors ─────────────────────────────────────────────────

    public PaneTabsViewModel LeftTabs  { get; }
    public PaneTabsViewModel RightTabs { get; }
    public FolderTreeViewModel FolderTree { get; } = new();

    /// <summary>Currently active pane in the left tab strip.</summary>
    public PaneViewModel LeftPane  => LeftTabs.ActivePane;

    /// <summary>Currently active pane in the right tab strip.</summary>
    public PaneViewModel RightPane => RightTabs.ActivePane;

    /// <summary>The globally focused pane (drives terminal prompt, history, etc.).</summary>
    public PaneViewModel ActivePane => _activeIsLeft ? LeftPane : RightPane;

    /// <summary>The other (unfocused) pane – used for cross-pane copy/move.</summary>
    public PaneViewModel OtherPane  => _activeIsLeft ? RightPane : LeftPane;

    [RelayCommand]
    public void ActivateLeftPane()
    {
        _activeIsLeft = true;
        foreach (var t in RightTabs.Tabs) t.Pane.IsActive = false;
        LeftPane.IsActive = true;
        OnPropertyChanged(nameof(ActivePane));
        OnPropertyChanged(nameof(TerminalPrompt));
        SubscribeActivePanePath();
    }

    [RelayCommand]
    public void ActivateRightPane()
    {
        _activeIsLeft = false;
        foreach (var t in LeftTabs.Tabs) t.Pane.IsActive = false;
        RightPane.IsActive = true;
        OnPropertyChanged(nameof(ActivePane));
        OnPropertyChanged(nameof(TerminalPrompt));
        SubscribeActivePanePath();
    }

    private void SubscribeActivePanePath()
    {
        LeftPane.PropertyChanged  -= ActivePanePathChanged;
        RightPane.PropertyChanged -= ActivePanePathChanged;
        ActivePane.PropertyChanged += ActivePanePathChanged;
    }

    // ─── Cross-pane operations ────────────────────────────────────────────────

    /// <summary>F5 – copy selected items to the opposite pane's folder (queued).</summary>
    [RelayCommand]
    private async Task CopyToOtherPaneAsync()
    {
        var sources = ActivePane.SelectedItems.Select(i => i.FullPath).ToList();
        if (sources.Count == 0 && ActivePane.SelectedItem is not null)
            sources.Add(ActivePane.SelectedItem.FullPath);
        if (sources.Count == 0) return;

        var dest = OtherPane.CurrentPath;
        if (string.IsNullOrWhiteSpace(dest)) return;

        var job = CopyQueueService.Instance.Enqueue(sources, dest, isMove: false);
        job.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CopyJob.Status) && job.Status == CopyJobStatus.Completed)
                Application.Current.Dispatcher.Invoke(
                    () => _ = OtherPane.GoToAsync(dest, pushHistory: false));
        };
        await Task.CompletedTask;
    }

    /// <summary>F6 – move selected items to the opposite pane's folder (queued).</summary>
    [RelayCommand]
    private async Task MoveToOtherPaneAsync()
    {
        var sources = ActivePane.SelectedItems.Select(i => i.FullPath).ToList();
        if (sources.Count == 0 && ActivePane.SelectedItem is not null)
            sources.Add(ActivePane.SelectedItem.FullPath);
        if (sources.Count == 0) return;

        var dest = OtherPane.CurrentPath;
        if (string.IsNullOrWhiteSpace(dest)) return;

        var job = CopyQueueService.Instance.Enqueue(sources, dest, isMove: true);
        job.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CopyJob.Status) && job.Status == CopyJobStatus.Completed)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _ = ActivePane.GoToAsync(ActivePane.CurrentPath, pushHistory: false);
                    _ = OtherPane.GoToAsync(dest, pushHistory: false);
                });
        };
        await Task.CompletedTask;
    }

    // ─── Copy queue ───────────────────────────────────────────────────────────

    public CopyQueueService CopyQueue => CopyQueueService.Instance;

    // ─── Favorites ────────────────────────────────────────────────────────────

    [RelayCommand]
    private void AddToFavorites()
    {
        var path = ActivePane.CurrentPath;
        if (!string.IsNullOrWhiteSpace(path))
            FolderTree.AddFavorite(path);
    }

    [RelayCommand]
    private void RemoveFromFavorites()
    {
        var path = ActivePane.CurrentPath;
        if (!string.IsNullOrWhiteSpace(path))
            FolderTree.RemoveFavorite(path);
    }

    // ─── Folder compare ───────────────────────────────────────────────────────

    [ObservableProperty] private bool _showCompare;

    [RelayCommand]
    private void CompareDirectories()
    {
        ShowCompare = !ShowCompare;
        if (ShowCompare)
        {
            var results = FolderCompareService.Compare(LeftPane.CurrentPath, RightPane.CurrentPath);
            CompareResults.Clear();
            foreach (var r in results) CompareResults.Add(r);
        }
    }

    public System.Collections.ObjectModel.ObservableCollection<CompareResult> CompareResults { get; } = new();

    // ─── Git history ──────────────────────────────────────────────────────────

    public GitHistoryViewModel GitHistory { get; } = new();
    public GitTabViewModel GitTab { get; } = new();

    /// <summary>Which bottom-panel tab is active: "Terminal", "GitHistory", or "Git".</summary>
    [ObservableProperty] private string _activeBottomTab = "Terminal";

    [RelayCommand]
    private void SwitchBottomTab(string tab)
    {
        ActiveBottomTab = tab;
        if (tab == "Git")
            _ = GitTab.LoadAsync(ActivePane.CurrentPath);
    }

    [RelayCommand]
    private async Task ShowGitTabAsync()
    {
        await GitTab.LoadAsync(ActivePane.CurrentPath);
        ActiveBottomTab = "Git";
    }

    [RelayCommand]
    private async Task ShowGitHistoryAsync()
    {
        var ap = ActivePane;
        var filePath = ap.SelectedItem?.FullPath;
        var dir = ap.CurrentPath;

        if (filePath is not null && !ap.SelectedItem!.IsDirectory)
            await GitHistory.ShowHistoryAsync(dir, filePath);
        else
            await GitHistory.ShowHistoryAsync(dir);

        ActiveBottomTab = "GitHistory";
    }

    // ─── Batch rename (opens dialog) ──────────────────────────────────────────

    /// <summary>Raised to ask the view to open the batch rename window.</summary>
    public event EventHandler? BatchRenameRequested;

    [RelayCommand]
    private void BatchRename() => BatchRenameRequested?.Invoke(this, EventArgs.Empty);

    // ─── Search (opens dialog) ────────────────────────────────────────────────

    public event EventHandler? SearchRequested;

    [RelayCommand]
    private void OpenSearch() => SearchRequested?.Invoke(this, EventArgs.Empty);

    // ─── Terminal panel ────────────────────────────────────────────────────────

    public TerminalPanelViewModel TerminalPanel { get; private set; } = null!;

    // Properties on the active tab that must be forwarded to the main VM bindings
    private static readonly HashSet<string> _forwardedTabProps = new(StringComparer.Ordinal)
    {
        nameof(TerminalTabViewModel.ShowSuggestions),
        nameof(TerminalTabViewModel.InlineSuggestion),
        nameof(TerminalTabViewModel.SelectedSuggestionIndex),
        nameof(TerminalTabViewModel.CommandText),
        nameof(TerminalTabViewModel.OutputText),
        nameof(TerminalTabViewModel.IsRunning),
        nameof(TerminalTabViewModel.Prompt),
    };

    private TerminalTabViewModel? _subscribedTab;

    private void SubscribeToActiveTab()
    {
        var newTab = TerminalPanel.ActiveTab;
        if (ReferenceEquals(_subscribedTab, newTab)) return;

        if (_subscribedTab is not null)
            _subscribedTab.PropertyChanged -= OnActiveTabPropertyChanged;

        _subscribedTab = newTab;

        if (_subscribedTab is not null)
            _subscribedTab.PropertyChanged += OnActiveTabPropertyChanged;

        // Broadcast all forwarded properties when the tab switches
        foreach (var prop in _forwardedTabProps)
            OnPropertyChanged(prop);
        OnPropertyChanged(nameof(Suggestions));
        OnPropertyChanged(nameof(TerminalPrompt));
    }

    private void OnActiveTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not null && _forwardedTabProps.Contains(e.PropertyName))
        {
            OnPropertyChanged(e.PropertyName);

            if (e.PropertyName == nameof(TerminalTabViewModel.Prompt))
                OnPropertyChanged(nameof(TerminalPrompt));
        }

        // Suggestions collection itself might have been rebuilt
        if (e.PropertyName == nameof(TerminalTabViewModel.ShowSuggestions))
            OnPropertyChanged(nameof(Suggestions));
    }

    // ── Backward-compatible delegation properties ──

    public IReadOnlyList<ShellKind> ShellKinds => TerminalPanel.ShellKinds;

    public ShellKind Shell
    {
        get => TerminalPanel.ActiveTab?.Shell ?? ShellKind.PowerShell;
        set { if (TerminalPanel.ActiveTab is not null) TerminalPanel.ActiveTab.Shell = value; OnPropertyChanged(); OnPropertyChanged(nameof(TerminalPrompt)); }
    }

    public string OutputText
    {
        get => TerminalPanel.ActiveTab?.OutputText ?? string.Empty;
        set { if (TerminalPanel.ActiveTab is not null) TerminalPanel.ActiveTab.OutputText = value; OnPropertyChanged(); }
    }

    public bool IsRunning => TerminalPanel.ActiveTab?.IsRunning ?? false;

    public string CommandText
    {
        get => TerminalPanel.ActiveTab?.CommandText ?? string.Empty;
        set { if (TerminalPanel.ActiveTab is not null) TerminalPanel.ActiveTab.CommandText = value; OnPropertyChanged(); }
    }

    public string InlineSuggestion
    {
        get => TerminalPanel.ActiveTab?.InlineSuggestion ?? string.Empty;
        set { if (TerminalPanel.ActiveTab is not null) TerminalPanel.ActiveTab.InlineSuggestion = value; OnPropertyChanged(); }
    }

    public bool ShowSuggestions
    {
        get => TerminalPanel.ActiveTab?.ShowSuggestions ?? false;
        set { if (TerminalPanel.ActiveTab is not null) TerminalPanel.ActiveTab.ShowSuggestions = value; OnPropertyChanged(); }
    }

    public int SelectedSuggestionIndex
    {
        get => TerminalPanel.ActiveTab?.SelectedSuggestionIndex ?? -1;
        set { if (TerminalPanel.ActiveTab is not null) TerminalPanel.ActiveTab.SelectedSuggestionIndex = value; OnPropertyChanged(); }
    }

    public ObservableCollection<SuggestionItem> Suggestions => TerminalPanel.ActiveTab?.Suggestions ?? _emptySuggestions;
    private static readonly ObservableCollection<SuggestionItem> _emptySuggestions = new();

    public void NavigateHistoryUp()    => TerminalPanel.ActiveTab?.NavigateHistoryUp();
    public void NavigateHistoryDown()  => TerminalPanel.ActiveTab?.NavigateHistoryDown();
    public void HandleTabCompletion()  => TerminalPanel.ActiveTab?.HandleTabCompletion();
    public void DismissSuggestions()   => TerminalPanel.ActiveTab?.DismissSuggestions();

    public void AcceptSuggestion(SuggestionItem? item = null)
        => TerminalPanel.ActiveTab?.AcceptSuggestion(item);

    public string TerminalPrompt => TerminalPanel.ActiveTab?.Prompt ?? $"PS {ActivePane.CurrentPath}>";

    private void ActivePanePathChanged(object? _, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PaneViewModel.CurrentPath))
        {
            OnPropertyChanged(nameof(TerminalPrompt));
            TerminalPanel.SyncWorkingDirectory(ActivePane.CurrentPath);
        }
    }

    private void OnAnyPanePathChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PaneViewModel.CurrentPath)) return;
        if (sender is not PaneViewModel pane) return;

        var path = pane.CurrentPath;
        if (string.IsNullOrWhiteSpace(path)) return;

        // Skip drive roots (e.g. C:\, D:\)
        if (Path.GetPathRoot(path)?.Equals(path, StringComparison.OrdinalIgnoreCase) == true) return;

        // Remove if already present, then insert at front
        _recentLocations.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
        _recentLocations.Insert(0, path);

        // Keep only the most recent entries
        if (_recentLocations.Count > MaxRecentLocations)
            _recentLocations.RemoveRange(MaxRecentLocations, _recentLocations.Count - MaxRecentLocations);

        FolderTree.SetRecentLocations(_recentLocations);

        // Persist to settings
        App.SettingsService.Update(s => s with { RecentLocations = new List<string>(_recentLocations) });
    }

    private void ResubscribeRecentTracking()
    {
        if (_trackedLeftPane is not null)
            _trackedLeftPane.PropertyChanged -= OnAnyPanePathChanged;
        if (_trackedRightPane is not null)
            _trackedRightPane.PropertyChanged -= OnAnyPanePathChanged;

        _trackedLeftPane = LeftPane;
        _trackedRightPane = RightPane;

        _trackedLeftPane.PropertyChanged  += OnAnyPanePathChanged;
        _trackedRightPane.PropertyChanged += OnAnyPanePathChanged;
    }

    // ─── History ─────────────────────────────────────────────────────────────

    [ObservableProperty] private string _historyFilterText = string.Empty;

    public ICollectionView HistoryView { get; }
    public ObservableCollection<CommandHistoryEntry> History { get; } = new();

    private bool FilterHistory(object obj)
    {
        if (obj is not CommandHistoryEntry e) return false;
        var f = HistoryFilterText;
        return string.IsNullOrWhiteSpace(f)
            || e.Command.Contains(f,          StringComparison.OrdinalIgnoreCase)
            || e.WorkingDirectory.Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnHistoryFilterTextChanged(string value) => HistoryView.Refresh();

    [RelayCommand]
    private void ClearHistoryFilter() => HistoryFilterText = string.Empty;

    // ─── Run command (delegates to active terminal tab) ────────────────────────

    [RelayCommand]
    private async Task RunCommandAsync()
    {
        if (TerminalPanel.ActiveTab is null) return;

        // Sync working directory before executing
        if (!string.Equals(TerminalPanel.ActiveTab.WorkingDirectory, ActivePane.CurrentPath, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(ActivePane.CurrentPath))
        {
            TerminalPanel.ActiveTab.WorkingDirectory = ActivePane.CurrentPath;
        }

        await TerminalPanel.ActiveTab.RunCommandCommand.ExecuteAsync(null);

        // Re-broadcast changes
        OnPropertyChanged(nameof(OutputText));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CommandText));
        OnPropertyChanged(nameof(TerminalPrompt));

        // Sync the file pane if the terminal's working directory changed
        var newWd = TerminalPanel.ActiveTab.WorkingDirectory;
        if (!string.Equals(newWd, ActivePane.CurrentPath, StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(newWd))
        {
            await ActivePane.GoToAsync(newWd, pushHistory: true);
        }
    }

    [RelayCommand]
    private void LoadHistoryEntry(object? parameter)
    {
        if (parameter is not CommandHistoryEntry entry) return;
        if (TerminalPanel.ActiveTab is not null)
        {
            TerminalPanel.ActiveTab.CommandText = entry.Command;
            TerminalPanel.ActiveTab.Shell = entry.Shell;
            TerminalPanel.ActiveTab.DismissSuggestions();
            OnPropertyChanged(nameof(CommandText));
            OnPropertyChanged(nameof(Shell));
        }
        _ = ActivePane.GoToAsync(entry.WorkingDirectory, pushHistory: true);
    }
}
