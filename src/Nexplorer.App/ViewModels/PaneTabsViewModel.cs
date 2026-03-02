using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nexplorer.App.ViewModels;

/// <summary>
/// Manages the tab strip for one side of the dual-pane layout.
/// Exposes <see cref="ActivePane"/> as the currently visible <see cref="PaneViewModel"/>.
/// </summary>
public sealed partial class PaneTabsViewModel : ObservableObject
{
    private readonly string _initialPath;

    public PaneTabsViewModel(string initialPath)
    {
        _initialPath = initialPath;

        // Start with one tab
        var pane = new PaneViewModel(initialPath);
        var tab  = new PaneTabViewModel(pane) { IsActive = true };
        Tabs.Add(tab);
        _activeTab = tab;
    }

    public ObservableCollection<PaneTabViewModel> Tabs { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActivePane))]
    private PaneTabViewModel? _activeTab;

    /// <summary>Shortcut to the currently active pane (never null after construction).</summary>
    public PaneViewModel ActivePane => ActiveTab!.Pane;

    // ─── Tab commands ─────────────────────────────────────────────────────

    [RelayCommand]
    public void AddTab(string? path = null)
    {
        var startPath = path ?? ActivePane.CurrentPath ?? _initialPath;
        var pane      = new PaneViewModel(startPath);
        var tab       = new PaneTabViewModel(pane);
        Tabs.Add(tab);
        _ = pane.GoToAsync(startPath, pushHistory: false);
        SelectTab(tab);
    }

    [RelayCommand]
    public void CloseTab(PaneTabViewModel tab)
    {
        if (Tabs.Count <= 1) return;           // keep at least one tab

        var idx    = Tabs.IndexOf(tab);
        var wasActive = tab.IsActive;
        Tabs.Remove(tab);

        if (wasActive)
        {
            var next = Tabs[Math.Clamp(idx, 0, Tabs.Count - 1)];
            SelectTab(next);
        }
    }

    [RelayCommand]
    public void SelectTab(PaneTabViewModel tab)
    {
        foreach (var t in Tabs) t.IsActive = false;
        tab.IsActive = true;
        ActiveTab    = tab;
        OnPropertyChanged(nameof(ActivePane));
    }

    /// <summary>Duplicates the active tab and selects the duplicate.</summary>
    [RelayCommand]
    public void DuplicateTab()
        => AddTab(ActivePane.CurrentPath);
}
