using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexplorer.App.Services;

namespace Nexplorer.App.ViewModels;

/// <summary>
/// A single item displayed in the Command Palette list.
/// </summary>
public sealed partial class PaletteItemViewModel : ObservableObject
{
    public PaletteCommand Command { get; }
    public string Name => Command.Name;
    public string Category => Command.Category;
    public string? Shortcut => Command.Shortcut;
    public string IconKind => Command.IconKind;
    public int Score { get; set; }
    public List<int> MatchedIndices { get; set; } = [];

    [ObservableProperty] private bool _isSelected;

    public PaletteItemViewModel(PaletteCommand command) => Command = command;
}

/// <summary>
/// VS Code-style Command Palette ViewModel.
/// Provides fuzzy-searchable access to every application command.
/// </summary>
public sealed partial class CommandPaletteViewModel : ObservableObject
{
    private readonly List<PaletteCommand> _allCommands = [];
    private readonly List<string> _recentCommandIds = [];
    private const int MaxRecent = 8;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _selectedIndex;

    public ObservableCollection<PaletteItemViewModel> FilteredCommands { get; } = [];

    /// <summary>Raised when a command is executed so the view can close the overlay.</summary>
    public event EventHandler? CommandExecuted;

    public void RegisterCommands(IEnumerable<PaletteCommand> commands)
    {
        _allCommands.Clear();
        _allCommands.AddRange(commands);
    }

    public void Open()
    {
        SearchText = string.Empty;
        IsOpen = true;
        SelectedIndex = 0;
        UpdateFilter();
    }

    public void Close()
    {
        IsOpen = false;
        SearchText = string.Empty;
    }

    partial void OnSearchTextChanged(string value) => UpdateFilter();

    private void UpdateFilter()
    {
        FilteredCommands.Clear();

        var query = SearchText.Trim();

        if (string.IsNullOrEmpty(query))
        {
            // Show recent commands first, then all alphabetically
            var recentSet = new HashSet<string>(_recentCommandIds);
            var recents = _recentCommandIds
                .Select(id => _allCommands.Find(c => c.Id == id))
                .Where(c => c is not null)
                .Select(c => new PaletteItemViewModel(c!) { Score = 10000 })
                .ToList();

            var rest = _allCommands
                .Where(c => !recentSet.Contains(c.Id))
                .OrderBy(c => c.Category)
                .ThenBy(c => c.Name)
                .Select(c => new PaletteItemViewModel(c) { Score = 0 });

            foreach (var item in recents)
                FilteredCommands.Add(item);
            foreach (var item in rest)
                FilteredCommands.Add(item);
        }
        else
        {
            // Fuzzy match and rank
            var matches = new List<PaletteItemViewModel>();
            foreach (var cmd in _allCommands)
            {
                // Match against name and category
                var (nameMatch, nameScore, nameIndices) = FuzzyMatcher.Match(query, cmd.Name);
                var (catMatch, catScore, _) = FuzzyMatcher.Match(query, $"{cmd.Category}: {cmd.Name}");

                if (nameMatch || catMatch)
                {
                    var best = nameScore >= catScore ? (nameScore, nameIndices) : (catScore, new List<int>());
                    matches.Add(new PaletteItemViewModel(cmd)
                    {
                        Score = best.Item1,
                        MatchedIndices = best.Item2
                    });
                }
            }

            foreach (var item in matches.OrderByDescending(m => m.Score))
                FilteredCommands.Add(item);
        }

        SelectedIndex = FilteredCommands.Count > 0 ? 0 : -1;
        UpdateSelection();
    }

    [RelayCommand]
    private void MoveUp()
    {
        if (FilteredCommands.Count == 0) return;
        SelectedIndex = (SelectedIndex - 1 + FilteredCommands.Count) % FilteredCommands.Count;
        UpdateSelection();
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (FilteredCommands.Count == 0) return;
        SelectedIndex = (SelectedIndex + 1) % FilteredCommands.Count;
        UpdateSelection();
    }

    [RelayCommand]
    private void ExecuteSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= FilteredCommands.Count) return;

        var item = FilteredCommands[SelectedIndex];
        TrackRecent(item.Command.Id);
        Close();
        CommandExecuted?.Invoke(this, EventArgs.Empty);

        // Execute on the next dispatcher frame so the palette overlay collapses first
        System.Windows.Application.Current.Dispatcher.InvokeAsync(
            item.Command.Execute,
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void TrackRecent(string commandId)
    {
        _recentCommandIds.Remove(commandId);
        _recentCommandIds.Insert(0, commandId);
        if (_recentCommandIds.Count > MaxRecent)
            _recentCommandIds.RemoveAt(_recentCommandIds.Count - 1);
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < FilteredCommands.Count; i++)
            FilteredCommands[i].IsSelected = i == SelectedIndex;
    }
}
