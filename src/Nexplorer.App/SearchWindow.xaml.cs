using System.IO;
using System.Windows;
using System.Windows.Input;
using Nexplorer.App.Services;

namespace Nexplorer.App;

public partial class SearchWindow : Window
{
    private CancellationTokenSource? _cts;
    private readonly List<FileInfo> _results = new();

    /// <summary>If the user double-clicks a result, its path is set here.</summary>
    public string? SelectedPath { get; private set; }

    public SearchWindow(string rootPath)
    {
        InitializeComponent();
        RootBox.Text = rootPath;
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _results.Clear();
        ResultsList.ItemsSource = null;

        var criteria = new SearchCriteria
        {
            Query     = QueryBox.Text,
            RootPath  = RootBox.Text,
            Recursive = RecursiveCheck.IsChecked == true,
            UseRegex  = RegexCheck.IsChecked == true,
            MinSize   = long.TryParse(MinSizeBox.Text, out var min) ? min * 1024 : null,
            MaxSize   = long.TryParse(MaxSizeBox.Text, out var max) ? max * 1024 : null,
            ModifiedAfter  = DateAfterPicker.SelectedDate,
            ModifiedBefore = DateBeforePicker.SelectedDate,
        };

        SearchButton.IsEnabled = false;
        StatusLabel.Text = "Searching…";

        try
        {
            await foreach (var fi in SearchService.SearchAsync(criteria, _cts.Token))
            {
                _results.Add(fi);

                // Update UI periodically
                if (_results.Count % 50 == 0)
                {
                    ResultsHeader.Text = $"Results ({_results.Count:n0})";
                    ResultsList.ItemsSource = null;
                    ResultsList.ItemsSource = _results.ToList();
                }
            }
        }
        catch (OperationCanceledException) { }

        ResultsList.ItemsSource = _results.ToList();
        ResultsHeader.Text = $"Results ({_results.Count:n0})";
        StatusLabel.Text   = $"Found {_results.Count:n0} items";
        SearchButton.IsEnabled = true;
    }

    private void ResultsList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is FileInfo fi)
        {
            var dirPath = fi.Attributes.HasFlag(FileAttributes.Directory)
                ? fi.FullName
                : fi.DirectoryName;
            SelectedPath = dirPath;
            DialogResult = true;
            Close();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        DialogResult = false;
        Close();
    }
}
