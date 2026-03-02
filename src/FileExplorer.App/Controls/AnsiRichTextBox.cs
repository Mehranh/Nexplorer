using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using FileExplorer.App.ViewModels;

namespace FileExplorer.App.Controls;

/// <summary>
/// A RichTextBox that renders ANSI-colored terminal output segments.
/// Binds to an ObservableCollection&lt;AnsiSegment&gt; and automatically
/// updates the document when the collection changes.
/// </summary>
public class AnsiRichTextBox : RichTextBox
{
    public static readonly DependencyProperty SegmentsProperty =
        DependencyProperty.Register(
            nameof(Segments),
            typeof(ObservableCollection<AnsiSegment>),
            typeof(AnsiRichTextBox),
            new PropertyMetadata(null, OnSegmentsChanged));

    public ObservableCollection<AnsiSegment>? Segments
    {
        get => (ObservableCollection<AnsiSegment>?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public static readonly DependencyProperty DefaultForegroundColorProperty =
        DependencyProperty.Register(
            nameof(DefaultForegroundColor),
            typeof(string),
            typeof(AnsiRichTextBox),
            new PropertyMetadata(null));

    public string DefaultForegroundColor
    {
        get
        {
            var val = (string)GetValue(DefaultForegroundColorProperty);
            if (val != null) return val;
            // Fall back to theme TextFg color
            if (Application.Current.Resources["TextFg"] is SolidColorBrush b)
                return b.Color.ToString();
            return "#CCCCCC";
        }
        set => SetValue(DefaultForegroundColorProperty, value);
    }

    public AnsiRichTextBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Document = new FlowDocument();
        Document.PagePadding = new Thickness(10, 8, 10, 8);
        BorderThickness = new Thickness(0);
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
    }

    private static void OnSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not AnsiRichTextBox box) return;

        if (e.OldValue is ObservableCollection<AnsiSegment> oldCollection)
            oldCollection.CollectionChanged -= box.OnSegmentsCollectionChanged;

        if (e.NewValue is ObservableCollection<AnsiSegment> newCollection)
        {
            newCollection.CollectionChanged += box.OnSegmentsCollectionChanged;
            box.RebuildDocument(newCollection);
        }
        else
        {
            box.Document.Blocks.Clear();
        }
    }

    private void OnSegmentsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (sender is ObservableCollection<AnsiSegment> segments)
            RebuildDocument(segments);
    }

    private void RebuildDocument(ObservableCollection<AnsiSegment> segments)
    {
        Document.Blocks.Clear();

        if (segments.Count == 0) return;

        var paragraph = new Paragraph { Margin = new Thickness(0) };

        foreach (var segment in segments)
        {
            if (string.IsNullOrEmpty(segment.Text)) continue;

            var run = new Run(segment.Text);

            // Apply foreground color
            if (!string.IsNullOrEmpty(segment.Foreground))
            {
                try
                {
                    var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(segment.Foreground));
                    brush.Freeze();
                    run.Foreground = brush;
                }
                catch { }
            }
            else
            {
                try
                {
                    var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(DefaultForegroundColor));
                    brush.Freeze();
                    run.Foreground = brush;
                }
                catch { }
            }

            // Apply background color
            if (!string.IsNullOrEmpty(segment.Background))
            {
                try
                {
                    var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(segment.Background));
                    brush.Freeze();
                    run.Background = brush;
                }
                catch { }
            }

            // Apply bold
            if (segment.Bold)
                run.FontWeight = FontWeights.Bold;

            paragraph.Inlines.Add(run);
        }

        Document.Blocks.Add(paragraph);

        // Auto-scroll to end
        ScrollToEnd();
    }
}
