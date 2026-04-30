using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Nexplorer.App.Collections;
using Nexplorer.App.Services;

namespace Nexplorer.App;

public partial class SearchWindow : Window, IDisposable
{
    // Append matches to the bound collection in batches sized to keep the UI at 60 FPS
    // even when the search engine produces tens of thousands of hits per second.
    private const int    BatchFlushSize = 256;
    private const double BatchFlushIntervalMs = 50.0;

    private CancellationTokenSource? _cts;
    private readonly RangeObservableCollection<FileInfo> _results = new();

    /// <summary>If the user double-clicks a result, its path is set here.</summary>
    public string? SelectedPath { get; private set; }

    public SearchWindow(string rootPath)
    {
        InitializeComponent();
        RootBox.Text = rootPath;
        ResultsList.ItemsSource = _results; // bound once; never reassigned
    }

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _results.Clear();
        ResultsHeader.Text = "Results";

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

        var pending = new List<FileInfo>(BatchFlushSize);
        var sw      = Stopwatch.StartNew();
        var lastFlushMs = 0L;

        void Flush()
        {
            if (pending.Count == 0) return;
            _results.AddRange(pending);
            pending.Clear();
            ResultsHeader.Text = $"Results ({_results.Count:n0})";
            lastFlushMs = sw.ElapsedMilliseconds;
        }

        try
        {
            await foreach (var fi in SearchService.SearchAsync(criteria, ct))
            {
                pending.Add(fi);

                if (pending.Count >= BatchFlushSize ||
                    sw.ElapsedMilliseconds - lastFlushMs >= BatchFlushIntervalMs)
                {
                    Flush();
                }
            }
        }
        catch (OperationCanceledException) { }

        Flush();
        ResultsHeader.Text = $"Results ({_results.Count:n0})";
        StatusLabel.Text   = $"Found {_results.Count:n0} items in {sw.Elapsed.TotalSeconds:0.0}s";
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

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
