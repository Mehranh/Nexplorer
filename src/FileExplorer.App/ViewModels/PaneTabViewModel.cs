using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileExplorer.App.ViewModels;

/// <summary>Represents a single tab inside a pane's tab strip.</summary>
public sealed partial class PaneTabViewModel : ObservableObject
{
    public PaneTabViewModel(PaneViewModel pane)
    {
        Pane = pane;

        // Keep the tab header in sync when the pane navigates
        pane.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PaneViewModel.CurrentPath))
                OnPropertyChanged(nameof(Header));
        };
    }

    public PaneViewModel Pane { get; }

    /// <summary>Short name shown on the tab button – last path segment.</summary>
    public string Header
    {
        get
        {
            var p = Pane.CurrentPath?.TrimEnd(Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(p)) return "~";
            return Path.GetFileName(p) is { Length: > 0 } name ? name : p;
        }
    }

    [ObservableProperty] private bool _isActive;
}
