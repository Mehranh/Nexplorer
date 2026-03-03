using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexplorer.App.Services;

namespace Nexplorer.App.ViewModels;

/// <summary>
/// Manages the terminal panel: multiple tabs, split panes, and profiles.
/// </summary>
public sealed partial class TerminalPanelViewModel : ObservableObject
{
    private readonly AliasService _aliasService = new();
    private readonly CommandHistoryStore _historyStore = new();
    private readonly TerminalProfileService _profileService = new();

    public TerminalPanelViewModel(
        string initialDirectory,
        ObservableCollection<CommandHistoryEntry> sharedHistory)
    {
        SharedHistory = sharedHistory;

        // Load history from store if shared history is empty
        if (SharedHistory.Count == 0)
        {
            foreach (var entry in CommandHistoryStore.Load())
                SharedHistory.Add(entry);
        }

        // Create the first terminal tab from default profile
        var defaultProfile = _profileService.GetDefault();
        var firstTab = CreateTab(
            defaultProfile.Shell,
            initialDirectory,
            defaultProfile.ExecutablePath,
            defaultProfile.Arguments);
        firstTab.Header = defaultProfile.Name;
        Tabs.Add(firstTab);
        ActiveTab = firstTab;
    }

    // ─── Tabs ─────────────────────────────────────────────────────────────────

    public ObservableCollection<TerminalTabViewModel> Tabs { get; } = new();

    [ObservableProperty] private TerminalTabViewModel? _activeTab;

    /// <summary>Secondary tab for split-pane view.</summary>
    [ObservableProperty] private TerminalTabViewModel? _splitTab;

    [ObservableProperty] private TerminalSplitOrientation _splitOrientation = TerminalSplitOrientation.None;

    // ─── Shared state ─────────────────────────────────────────────────────────

    public ObservableCollection<CommandHistoryEntry> SharedHistory { get; }
    public AliasService AliasService => _aliasService;

    public void SaveHistory() => CommandHistoryStore.Save(SharedHistory);

    // ─── Profiles ─────────────────────────────────────────────────────────────

    public IReadOnlyList<TerminalProfile> Profiles => _profileService.Profiles;

    public IReadOnlyList<ShellKind> ShellKinds { get; } =
        Enum.GetValues<ShellKind>();

    // ─── Tab management ───────────────────────────────────────────────────────

    private TerminalTabViewModel CreateTab(
        ShellKind shell,
        string workingDirectory,
        string? shellExecutablePath = null,
        string? shellArguments = null)
    {
        var tab = new TerminalTabViewModel(
            shell,
            workingDirectory,
            _aliasService,
            _historyStore,
            SharedHistory,
            shellExecutablePath: shellExecutablePath,
            shellArguments: shellArguments);
        return tab;
    }

    partial void OnActiveTabChanged(TerminalTabViewModel? value)
    {
        foreach (var t in Tabs) t.IsActive = false;
        if (value is not null) value.IsActive = true;
    }

    [RelayCommand]
    private void AddTab()
    {
        var wd = ActiveTab?.WorkingDirectory
                 ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var shell = ActiveTab?.Shell ?? ShellKind.PowerShell;
        var tab = CreateTab(shell, wd, ActiveTab?.ShellExecutablePath, ActiveTab?.ShellArguments);
        Tabs.Add(tab);
        ActiveTab = tab;
    }

    [RelayCommand]
    private void AddTabWithProfile(TerminalProfile? profile)
    {
        if (profile is null) { AddTab(); return; }

        var wd = profile.StartupDirectory
                 ?? ActiveTab?.WorkingDirectory
                 ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var tab = CreateTab(profile.Shell, wd, profile.ExecutablePath, profile.Arguments);
        tab.Header = profile.Name;
        Tabs.Add(tab);
        ActiveTab = tab;
    }

    [RelayCommand]
    private void CloseTab(TerminalTabViewModel? tab)
    {
        if (tab is null || Tabs.Count <= 1) return;

        var idx = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (SplitTab == tab) { SplitTab = null; SplitOrientation = TerminalSplitOrientation.None; }
        if (ActiveTab == tab || ActiveTab is null)
            ActiveTab = Tabs[Math.Min(idx, Tabs.Count - 1)];
    }

    [RelayCommand]
    private void DuplicateTab()
    {
        if (ActiveTab is null) return;
        var tab = CreateTab(
            ActiveTab.Shell,
            ActiveTab.WorkingDirectory,
            ActiveTab.ShellExecutablePath,
            ActiveTab.ShellArguments);
        Tabs.Add(tab);
        ActiveTab = tab;
    }

    [RelayCommand]
    private void SelectTab(TerminalTabViewModel? tab)
    {
        if (tab is not null)
            ActiveTab = tab;
    }

    // ─── Split panes ──────────────────────────────────────────────────────────

    [RelayCommand]
    private void SplitHorizontal()
    {
        if (ActiveTab is null) return;

        if (SplitOrientation != TerminalSplitOrientation.None)
        {
            // Toggle off
            SplitTab = null;
            SplitOrientation = TerminalSplitOrientation.None;
            return;
        }

        var tab = CreateTab(ActiveTab.Shell, ActiveTab.WorkingDirectory);
        Tabs.Add(tab);
        SplitTab = tab;
        SplitOrientation = TerminalSplitOrientation.Horizontal;
    }

    [RelayCommand]
    private void SplitVertical()
    {
        if (ActiveTab is null) return;

        if (SplitOrientation != TerminalSplitOrientation.None)
        {
            SplitTab = null;
            SplitOrientation = TerminalSplitOrientation.None;
            return;
        }

        var tab = CreateTab(ActiveTab.Shell, ActiveTab.WorkingDirectory);
        Tabs.Add(tab);
        SplitTab = tab;
        SplitOrientation = TerminalSplitOrientation.Vertical;
    }

    [RelayCommand]
    private void CloseSplit()
    {
        if (SplitTab is not null)
        {
            Tabs.Remove(SplitTab);
            SplitTab = null;
        }
        SplitOrientation = TerminalSplitOrientation.None;
    }

    // ─── Navigate tab working directory (called from pane) ────────────────────

    public void SyncWorkingDirectory(string path)
    {
        if (ActiveTab is not null &&
            !string.Equals(ActiveTab.WorkingDirectory, path, StringComparison.OrdinalIgnoreCase))
        {
            ActiveTab.WorkingDirectory = path;
        }
    }
}
