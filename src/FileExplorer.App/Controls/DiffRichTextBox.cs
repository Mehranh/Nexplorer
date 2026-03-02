using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using FileExplorer.App.Services;

namespace FileExplorer.App.Controls;

/// <summary>
/// A RichTextBox that renders diff lines with colored backgrounds:
/// green for added, red for removed, transparent for unchanged.
/// </summary>
public class DiffRichTextBox : RichTextBox
{
    private static readonly Brush AddedBg = new SolidColorBrush(Color.FromArgb(40, 78, 201, 78));
    private static readonly Brush RemovedBg = new SolidColorBrush(Color.FromArgb(40, 241, 76, 76));
    private static readonly Brush AddedFg = new SolidColorBrush(Color.FromRgb(0x73, 0xC9, 0x91));
    private static readonly Brush RemovedFg = new SolidColorBrush(Color.FromRgb(0xF1, 0x8C, 0x8C));
    private static readonly Brush UnchangedFg = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
    private static readonly Brush LineNumFg = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
    private static readonly Brush EmptyBg;

    // Synchronized scrolling support
    private static readonly ConcurrentDictionary<string, List<DiffRichTextBox>> SyncGroups = new();
    private ScrollViewer? _scrollViewer;
    private readonly DiffScrollSyncController _syncController = new();

    public static readonly DependencyProperty SyncScrollGroupProperty =
        DependencyProperty.Register(
            nameof(SyncScrollGroup),
            typeof(string),
            typeof(DiffRichTextBox),
            new PropertyMetadata(null, OnSyncScrollGroupChanged));

    public string? SyncScrollGroup
    {
        get => (string?)GetValue(SyncScrollGroupProperty);
        set => SetValue(SyncScrollGroupProperty, value);
    }

    public static readonly DependencyProperty SharedScrollBarProperty =
        DependencyProperty.Register(
            nameof(SharedScrollBar),
            typeof(ScrollBar),
            typeof(DiffRichTextBox),
            new PropertyMetadata(null, OnSharedScrollBarChanged));

    public ScrollBar? SharedScrollBar
    {
        get => (ScrollBar?)GetValue(SharedScrollBarProperty);
        set => SetValue(SharedScrollBarProperty, value);
    }

    static DiffRichTextBox()
    {
        AddedBg.Freeze();
        RemovedBg.Freeze();
        AddedFg.Freeze();
        RemovedFg.Freeze();
        UnchangedFg.Freeze();
        LineNumFg.Freeze();
        EmptyBg = new SolidColorBrush(Color.FromArgb(15, 128, 128, 128));
        EmptyBg.Freeze();
    }

    public static readonly DependencyProperty DiffLinesProperty =
        DependencyProperty.Register(
            nameof(DiffLines),
            typeof(ObservableCollection<DiffLine>),
            typeof(DiffRichTextBox),
            new PropertyMetadata(null, OnDiffLinesChanged));

    public ObservableCollection<DiffLine>? DiffLines
    {
        get => (ObservableCollection<DiffLine>?)GetValue(DiffLinesProperty);
        set => SetValue(DiffLinesProperty, value);
    }

    public DiffRichTextBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        BorderThickness = new Thickness(0);
        Padding = new Thickness(0);
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
        FontFamily = new FontFamily("Cascadia Code,Consolas,Courier New");
        FontSize = 11;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        Document.PageWidth = 5000; // prevent word wrap

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyTemplate();
        _scrollViewer = FindScrollViewer(this);
        if (_scrollViewer != null)
            _scrollViewer.ScrollChanged += OnScrollChanged;
        RegisterInGroup(SyncScrollGroup);
        // Re-subscribe in case OnUnloaded removed the handler.
        if (SharedScrollBar != null)
        {
            SharedScrollBar.ValueChanged -= OnSharedScrollBarValueChanged;
            SharedScrollBar.ValueChanged += OnSharedScrollBarValueChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_scrollViewer != null)
            _scrollViewer.ScrollChanged -= OnScrollChanged;
        UnregisterFromGroup(SyncScrollGroup);
        if (SharedScrollBar != null)
            SharedScrollBar.ValueChanged -= OnSharedScrollBarValueChanged;
    }

    private static void OnSyncScrollGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DiffRichTextBox box || !box.IsLoaded) return;
        box.UnregisterFromGroup(e.OldValue as string);
        box.RegisterInGroup(e.NewValue as string);
    }

    private void RegisterInGroup(string? group)
    {
        if (group == null) return;
        var list = SyncGroups.GetOrAdd(group, _ => new List<DiffRichTextBox>());
        lock (list) { if (!list.Contains(this)) list.Add(this); }
    }

    private void UnregisterFromGroup(string? group)
    {
        if (group == null) return;
        if (SyncGroups.TryGetValue(group, out var list))
            lock (list) { list.Remove(this); }
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_scrollViewer == null) return;

        // Update shared middle scrollbar sizing and value
        if (SharedScrollBar != null && !_syncController.IsSyncing)
        {
            if (_syncController.BeginSync())
            {
                try
                {
                    var update = DiffScrollSyncController.ComputeScrollBarUpdate(
                        _scrollViewer.VerticalOffset,
                        _scrollViewer.ExtentHeight,
                        _scrollViewer.ViewportHeight);
                    SharedScrollBar.Maximum = update.Maximum;
                    SharedScrollBar.ViewportSize = update.ViewportSize;
                    SharedScrollBar.SmallChange = update.SmallChange;
                    SharedScrollBar.LargeChange = update.LargeChange;
                    SharedScrollBar.Value = update.Value;
                }
                finally
                {
                    _syncController.EndSync();
                }
            }
        }

        if (_syncController.IsSyncing) return;
        var group = SyncScrollGroup;
        if (group == null || !SyncGroups.TryGetValue(group, out var list)) return;

        List<DiffRichTextBox> peers;
        lock (list) { peers = list.ToList(); }

        foreach (var peer in peers)
        {
            if (peer == this || peer._scrollViewer == null) continue;
            if (!peer._syncController.BeginSync()) continue;
            try
            {
                peer._scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset);
                peer._scrollViewer.ScrollToHorizontalOffset(_scrollViewer.HorizontalOffset);
            }
            finally
            {
                peer._syncController.EndSync();
            }
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject obj)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i);
            if (child is ScrollViewer sv) return sv;
            var result = FindScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }

    private static void OnSharedScrollBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DiffRichTextBox box) return;
        if (e.OldValue is ScrollBar oldSb)
            oldSb.ValueChanged -= box.OnSharedScrollBarValueChanged;
        if (e.NewValue is ScrollBar newSb)
            newSb.ValueChanged += box.OnSharedScrollBarValueChanged;
    }

    private void OnSharedScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncController.IsSyncing) return;
        if (_scrollViewer == null)
        {
            ApplyTemplate();
            _scrollViewer = FindScrollViewer(this);
            if (_scrollViewer != null)
                _scrollViewer.ScrollChanged += OnScrollChanged;
        }
        if (_scrollViewer == null) return;
        if (!_syncController.BeginSync()) return;
        try
        {
            _scrollViewer.ScrollToVerticalOffset(e.NewValue);
        }
        finally
        {
            _syncController.EndSync();
        }
    }

    private static void OnDiffLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DiffRichTextBox box) return;

        if (e.OldValue is ObservableCollection<DiffLine> oldCol)
            oldCol.CollectionChanged -= box.OnCollectionChanged;

        if (e.NewValue is ObservableCollection<DiffLine> newCol)
        {
            newCol.CollectionChanged += box.OnCollectionChanged;
            box.Rebuild(newCol);
        }
        else
        {
            box.Document.Blocks.Clear();
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is ObservableCollection<DiffLine> lines)
            Rebuild(lines);
    }

    private void Rebuild(ObservableCollection<DiffLine> lines)
    {
        var doc = new FlowDocument
        {
            PageWidth = 5000,
            FontFamily = FontFamily,
            FontSize = FontSize,
            PagePadding = new Thickness(0),
        };

        foreach (var line in lines)
        {
            var para = new Paragraph
            {
                Margin = new Thickness(0),
                Padding = new Thickness(4, 1, 4, 1),
                LineHeight = 18,
            };

            // Line number
            var lineNum = line.LineNumber?.ToString("D4") ?? "    ";
            para.Inlines.Add(new Run(lineNum + "  ") { Foreground = LineNumFg });

            switch (line.Kind)
            {
                case DiffLineKind.Added:
                    para.Background = AddedBg;
                    para.Inlines.Add(new Run(line.Text) { Foreground = AddedFg });
                    break;
                case DiffLineKind.Removed:
                    para.Background = RemovedBg;
                    para.Inlines.Add(new Run(line.Text) { Foreground = RemovedFg });
                    break;
                default:
                    if (string.IsNullOrEmpty(line.Text) && line.LineNumber is null)
                        para.Background = EmptyBg;
                    para.Inlines.Add(new Run(line.Text) { Foreground = UnchangedFg });
                    break;
            }

            doc.Blocks.Add(para);
        }

        Document = doc;
    }
}
