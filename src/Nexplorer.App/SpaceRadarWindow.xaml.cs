using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Nexplorer.App.Controls;
using Nexplorer.App.Services;

namespace Nexplorer.App;

public partial class SpaceRadarWindow : Window
{
    private readonly string _rootPath;
    private DiskNode? _rootNode;
    private DiskNode? _currentNode;
    private readonly Stack<DiskNode> _history = new();
    private CancellationTokenSource? _cts;

    // File-type color palette
    private static readonly Dictionary<string, Color> s_extColors = new(StringComparer.OrdinalIgnoreCase)
    {
        // Media
        [".jpg"] = Color.FromRgb(0x4E, 0xC9, 0xB0), [".jpeg"] = Color.FromRgb(0x4E, 0xC9, 0xB0),
        [".png"] = Color.FromRgb(0x4E, 0xC9, 0xB0), [".gif"] = Color.FromRgb(0x4E, 0xC9, 0xB0),
        [".bmp"] = Color.FromRgb(0x4E, 0xC9, 0xB0), [".svg"] = Color.FromRgb(0x4E, 0xC9, 0xB0),
        [".webp"] = Color.FromRgb(0x4E, 0xC9, 0xB0), [".ico"] = Color.FromRgb(0x4E, 0xC9, 0xB0),
        // Video
        [".mp4"] = Color.FromRgb(0xCE, 0x91, 0x78), [".mkv"] = Color.FromRgb(0xCE, 0x91, 0x78),
        [".avi"] = Color.FromRgb(0xCE, 0x91, 0x78), [".mov"] = Color.FromRgb(0xCE, 0x91, 0x78),
        [".wmv"] = Color.FromRgb(0xCE, 0x91, 0x78), [".flv"] = Color.FromRgb(0xCE, 0x91, 0x78),
        // Audio
        [".mp3"] = Color.FromRgb(0xD1, 0x6B, 0xA5), [".flac"] = Color.FromRgb(0xD1, 0x6B, 0xA5),
        [".wav"] = Color.FromRgb(0xD1, 0x6B, 0xA5), [".aac"] = Color.FromRgb(0xD1, 0x6B, 0xA5),
        [".ogg"] = Color.FromRgb(0xD1, 0x6B, 0xA5), [".wma"] = Color.FromRgb(0xD1, 0x6B, 0xA5),
        // Archives
        [".zip"] = Color.FromRgb(0xD7, 0xBA, 0x7D), [".rar"] = Color.FromRgb(0xD7, 0xBA, 0x7D),
        [".7z"] = Color.FromRgb(0xD7, 0xBA, 0x7D), [".tar"] = Color.FromRgb(0xD7, 0xBA, 0x7D),
        [".gz"] = Color.FromRgb(0xD7, 0xBA, 0x7D), [".bz2"] = Color.FromRgb(0xD7, 0xBA, 0x7D),
        // Code
        [".cs"] = Color.FromRgb(0x56, 0x9C, 0xD6), [".js"] = Color.FromRgb(0x56, 0x9C, 0xD6),
        [".ts"] = Color.FromRgb(0x56, 0x9C, 0xD6), [".py"] = Color.FromRgb(0x56, 0x9C, 0xD6),
        [".java"] = Color.FromRgb(0x56, 0x9C, 0xD6), [".cpp"] = Color.FromRgb(0x56, 0x9C, 0xD6),
        [".h"] = Color.FromRgb(0x56, 0x9C, 0xD6), [".xaml"] = Color.FromRgb(0x56, 0x9C, 0xD6),
        [".html"] = Color.FromRgb(0x56, 0x9C, 0xD6), [".css"] = Color.FromRgb(0x56, 0x9C, 0xD6),
        // Documents
        [".pdf"] = Color.FromRgb(0xF1, 0x4C, 0x4C), [".doc"] = Color.FromRgb(0xF1, 0x4C, 0x4C),
        [".docx"] = Color.FromRgb(0xF1, 0x4C, 0x4C), [".xls"] = Color.FromRgb(0x6A, 0x99, 0x55),
        [".xlsx"] = Color.FromRgb(0x6A, 0x99, 0x55), [".ppt"] = Color.FromRgb(0xCE, 0x63, 0x35),
        [".pptx"] = Color.FromRgb(0xCE, 0x63, 0x35), [".txt"] = Color.FromRgb(0x9C, 0xDC, 0xFE),
        [".md"] = Color.FromRgb(0x9C, 0xDC, 0xFE),
        // Executables
        [".exe"] = Color.FromRgb(0xDC, 0xDC, 0xAA), [".dll"] = Color.FromRgb(0xDC, 0xDC, 0xAA),
        [".msi"] = Color.FromRgb(0xDC, 0xDC, 0xAA), [".sys"] = Color.FromRgb(0xDC, 0xDC, 0xAA),
        // Data
        [".json"] = Color.FromRgb(0xB5, 0xCE, 0xA8), [".xml"] = Color.FromRgb(0xB5, 0xCE, 0xA8),
        [".csv"] = Color.FromRgb(0xB5, 0xCE, 0xA8), [".sql"] = Color.FromRgb(0xB5, 0xCE, 0xA8),
        [".db"] = Color.FromRgb(0xB5, 0xCE, 0xA8), [".sqlite"] = Color.FromRgb(0xB5, 0xCE, 0xA8),
    };

    private static readonly Color s_folderColor = Color.FromRgb(0x3C, 0x3C, 0x3C);
    private static readonly Color s_unknownColor = Color.FromRgb(0x50, 0x50, 0x50);

    public SpaceRadarWindow(string rootPath)
    {
        _rootPath = rootPath;
        InitializeComponent();
        Loaded += async (_, _) => await StartScanAsync();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Back && _history.Count > 0)
        { Back_Click(this, e); e.Handled = true; }
        else if (e.Key == Key.F5)
        { Rescan_Click(this, e); e.Handled = true; }
        else if (e.Key == Key.Escape)
        { Close(); e.Handled = true; }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        base.OnClosed(e);
    }

    private async Task StartScanAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        ProgressPanel.Visibility = Visibility.Visible;
        ScanProgress.IsIndeterminate = true;
        TreemapHost.Children.Clear();
        EmptyLabel.Visibility = Visibility.Collapsed;
        RescanButton.IsEnabled = false;
        _history.Clear();
        BackButton.IsEnabled = false;

        var progress = new Progress<(int filesScanned, long totalBytes)>(p =>
        {
            ProgressText.Text = $"Scanning… {p.filesScanned:N0} files  ({FormatSize(p.totalBytes)})";
        });

        try
        {
            _rootNode = await DiskAnalyzerService.AnalyzeAsync(_rootPath, progress, _cts.Token);
            _currentNode = _rootNode;
            RenderTreemap(_currentNode);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusLeft.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
            RescanButton.IsEnabled = true;
        }
    }

    private void RenderTreemap(DiskNode node)
    {
        TreemapHost.Children.Clear();
        _currentNode = node;

        UpdateBreadcrumb();

        if (node.Children.Count == 0 || node.Size <= 0)
        {
            EmptyLabel.Visibility = Visibility.Visible;
            StatusLeft.Text = "Empty directory";
            StatusRight.Text = "";
            return;
        }

        EmptyLabel.Visibility = Visibility.Collapsed;

        int dirCount = 0, fileCount = 0;
        foreach (var child in node.Children)
        {
            if (child.IsDirectory) dirCount++;
            else fileCount++;

            var block = CreateBlock(child, node.Size);
            TreemapHost.Children.Add(block);
        }

        StatusLeft.Text = $"{dirCount} folders, {fileCount} files";
        StatusRight.Text = $"Total: {FormatSize(node.Size)}";
    }

    private Border CreateBlock(DiskNode node, long parentSize)
    {
        // Green → Red gradient based on relative size (small = green, large = red)
        double sizeRatio = parentSize > 0 ? (double)node.Size / parentSize : 0;
        double t = Math.Pow(Math.Clamp(sizeRatio, 0, 1), 0.35); // curve for spread
        Color baseColor = LerpColor(
            Color.FromRgb(0x2E, 0xA0, 0x43), // green (small)
            Color.FromRgb(0xD0, 0x33, 0x33), // red   (large)
            t);

        var brush = new SolidColorBrush(baseColor);
        brush.Freeze();

        var hoverBrush = new SolidColorBrush(Lighten(baseColor, 0.25));
        hoverBrush.Freeze();

        var textBrush = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xEE));
        textBrush.Freeze();

        var subBrush = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
        subBrush.Freeze();

        var nameBlock = new TextBlock
        {
            Text = node.Name,
            Foreground = textBrush,
            FontSize = 11,
            FontWeight = node.IsDirectory ? FontWeights.SemiBold : FontWeights.Normal,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(4, 2, 4, 0)
        };

        var sizeBlock = new TextBlock
        {
            Text = FormatSize(node.Size),
            Foreground = subBrush,
            FontSize = 9,
            Margin = new Thickness(4, 0, 4, 2)
        };

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
        stack.Children.Add(nameBlock);
        stack.Children.Add(sizeBlock);

        var border = new Border
        {
            Background = brush,
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0x00, 0x00, 0x00)),
            BorderThickness = new Thickness(0.5),
            Child = stack,
            Cursor = node.IsDirectory ? Cursors.Hand : Cursors.Arrow,
            ClipToBounds = true,
            Tag = node
        };

        TreemapPanel.SetWeight(border, node.Size);

        border.MouseEnter += (s, e) =>
        {
            border.Background = hoverBrush;
            ShowTooltip(node, parentSize);
        };
        border.MouseLeave += (s, e) =>
        {
            border.Background = brush;
            TooltipOverlay.Visibility = Visibility.Collapsed;
        };
        border.MouseMove += (s, e) =>
        {
            var pos = e.GetPosition(TreemapHost);
            TooltipOverlay.Margin = new Thickness(pos.X + 16, pos.Y + 16, 0, 0);
        };

        if (node.IsDirectory && node.Children.Count > 0)
        {
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 1)
                {
                    _history.Push(_currentNode!);
                    BackButton.IsEnabled = true;
                    RenderTreemap(node);
                    e.Handled = true;
                }
            };
        }

        return border;
    }

    private void ShowTooltip(DiskNode node, long parentSize)
    {
        TipName.Text = node.Name;
        TipSize.Text = FormatSize(node.Size);
        double pct = parentSize > 0 ? (double)node.Size / parentSize * 100 : 0;
        TipPercent.Text = $"{pct:F1}% of parent";

        if (node.IsDirectory)
        {
            int dirs = 0, files = 0;
            CountDescendants(node, ref dirs, ref files);
            TipChildren.Text = $"{dirs} subfolders, {files} files";
            TipChildren.Visibility = Visibility.Visible;
        }
        else
        {
            TipChildren.Visibility = Visibility.Collapsed;
        }

        TooltipOverlay.Visibility = Visibility.Visible;
    }

    private static void CountDescendants(DiskNode node, ref int dirs, ref int files)
    {
        foreach (var child in node.Children)
        {
            if (child.IsDirectory)
            {
                dirs++;
                CountDescendants(child, ref dirs, ref files);
            }
            else files++;
        }
    }

    private void UpdateBreadcrumb()
    {
        if (_currentNode is null) return;

        // Build path from root
        var parts = new List<string>();
        if (_rootNode is not null && _currentNode.FullPath.StartsWith(_rootNode.FullPath, StringComparison.OrdinalIgnoreCase))
        {
            string relative = Path.GetRelativePath(_rootNode.FullPath, _currentNode.FullPath);
            parts.Add(_rootNode.Name);
            if (relative != ".")
            {
                foreach (var p in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    parts.Add(p);
            }
        }
        else parts.Add(_currentNode.Name);

        BreadcrumbText.Text = string.Join("  ›  ", parts);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_history.Count > 0)
        {
            var prev = _history.Pop();
            BackButton.IsEnabled = _history.Count > 0;
            RenderTreemap(prev);
        }
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e)
    {
        await StartScanAsync();
    }

    private static Color GetNodeColor(DiskNode node)
    {
        if (node.IsDirectory) return s_folderColor;
        string ext = Path.GetExtension(node.Name);
        return s_extColors.TryGetValue(ext, out var c) ? c : s_unknownColor;
    }

    private static Color LerpColor(Color a, Color b, double t)
    {
        byte r = (byte)(a.R + (b.R - a.R) * t);
        byte g = (byte)(a.G + (b.G - a.G) * t);
        byte bl = (byte)(a.B + (b.B - a.B) * t);
        return Color.FromRgb(r, g, bl);
    }

    private static Color Lighten(Color c, double amount)
    {
        byte r = (byte)Math.Min(255, c.R + (255 - c.R) * amount);
        byte g = (byte)Math.Min(255, c.G + (255 - c.G) * amount);
        byte b = (byte)Math.Min(255, c.B + (255 - c.B) * amount);
        return Color.FromRgb(r, g, b);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        int unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return unit == 0 ? $"{size:F0} {units[unit]}" : $"{size:F1} {units[unit]}";
    }
}
