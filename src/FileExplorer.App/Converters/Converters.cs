using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FileExplorer.App.ViewModels;

namespace FileExplorer.App.Converters;

// ── bool → Visibility ──────────────────────────────────────────────────────

/// <summary>true  → <see cref="Visibility.Visible"/>, false → Collapsed.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public static readonly BoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => (Visibility)v == Visibility.Visible;
}

/// <summary>true  → <see cref="Visibility.Collapsed"/>, false → Visible.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public static readonly InverseBoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => (Visibility)v != Visibility.Visible;
}

// ── Tab active state → brush ────────────────────────────────────────────────

/// <summary>bool IsActive → tab highlight brush.</summary>
[ValueConversion(typeof(bool), typeof(Brush))]
public sealed class TabActiveBrushConverter : IValueConverter
{
    public static readonly TabActiveBrushConverter Instance = new();

    private static readonly Brush ActiveBrush   = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E));
    private static readonly Brush InactiveBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x30));

    static TabActiveBrushConverter()
    {
        ActiveBrush.Freeze();
        InactiveBrush.Freeze();
    }

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is true ? ActiveBrush : InactiveBrush;

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}



/// <summary>bool (IsDirectory) → Segoe MDL2 Assets glyph</summary>
[ValueConversion(typeof(bool), typeof(string))]
public sealed class FileIconConverter : IValueConverter
{
    public static readonly FileIconConverter Instance = new();

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is true ? "\uE8B7" : "\uE8A5"; // folder / document

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>bool (IsDirectory) → folder amber / file blue brush</summary>
[ValueConversion(typeof(bool), typeof(Brush))]
public sealed class FileIconColorConverter : IValueConverter
{
    public static readonly FileIconColorConverter Instance = new();

    private static readonly Brush FolderBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0xB8, 0x6C)); // amber
    private static readonly Brush FileBrush   = new SolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE)); // light-blue

    static FileIconColorConverter()
    {
        FolderBrush.Freeze();
        FileBrush.Freeze();
    }

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is true ? FolderBrush : FileBrush;

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>int? ExitCode → green/red/gray brush for the history panel badge.</summary>
[ValueConversion(typeof(int?), typeof(Brush))]
public sealed class ExitCodeBrushConverter : IValueConverter
{
    public static readonly ExitCodeBrushConverter Instance = new();

    private static readonly Brush OkBrush   = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0x94));
    private static readonly Brush ErrBrush  = new SolidColorBrush(Color.FromRgb(0xF4, 0x47, 0x47));
    private static readonly Brush NullBrush = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60));

    static ExitCodeBrushConverter()
    {
        OkBrush.Freeze(); ErrBrush.Freeze(); NullBrush.Freeze();
    }

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is null ? NullBrush : (int)value == 0 ? OkBrush : ErrBrush;

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>ShellKind → prompt foreground brush (cyan=PS, yellow=Cmd)</summary>
[ValueConversion(typeof(ShellKind), typeof(Brush))]
public sealed class ShellPromptColorConverter : IValueConverter
{
    public static readonly ShellPromptColorConverter Instance = new();

    private static readonly Brush PsBrush  = new SolidColorBrush(Color.FromRgb(0x2D, 0xCF, 0xFF)); // cyan
    private static readonly Brush CmdBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)); // yellow

    static ShellPromptColorConverter()
    {
        PsBrush.Freeze(); CmdBrush.Freeze();
    }

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is ShellKind.Cmd ? CmdBrush : PsBrush;

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>SuggestionKind → accent brush for the popup icon.</summary>
[ValueConversion(typeof(SuggestionKind), typeof(Brush))]
public sealed class SuggestionKindColorConverter : IValueConverter
{
    public static readonly SuggestionKindColorConverter Instance = new();

    private static readonly Brush HistoryBrush = new SolidColorBrush(Color.FromRgb(0x9B, 0xDB, 0xFF));  // light blue
    private static readonly Brush FsBrush      = new SolidColorBrush(Color.FromRgb(0xDC, 0xB8, 0x6C));  // amber
    private static readonly Brush BangBrush    = new SolidColorBrush(Color.FromRgb(0xC5, 0x86, 0xC0));  // purple

    static SuggestionKindColorConverter()
    {
        HistoryBrush.Freeze(); FsBrush.Freeze(); BangBrush.Freeze();
    }

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value switch
        {
            SuggestionKind.FileSystem => FsBrush,
            SuggestionKind.BangCommand => BangBrush,
            _ => HistoryBrush
        };

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>TimeSpan? Duration → human-readable string ("1.2s", "340ms", "").</summary>
[ValueConversion(typeof(TimeSpan?), typeof(string))]
public sealed class DurationToStringConverter : IValueConverter
{
    public static readonly DurationToStringConverter Instance = new();

    public object Convert(object value, Type _, object __, CultureInfo ___)
    {
        if (value is not TimeSpan ts) return string.Empty;
        if (ts.TotalMinutes >= 1) return $"{ts.Minutes}m {ts.Seconds}.{ts.Milliseconds / 100}s";
        if (ts.TotalSeconds >= 1) return $"{ts.TotalSeconds:F1}s";
        return $"{ts.TotalMilliseconds:F0}ms";
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>bool IsRunning → "Stop" / "Run" button content</summary>
[ValueConversion(typeof(bool), typeof(string))]
public sealed class RunningToButtonTextConverter : IValueConverter
{
    public static readonly RunningToButtonTextConverter Instance = new();

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is true ? "\uE71A" : "\uE768"; // Stop / Run (MDL2 glyphs)

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

// ── Terminal split orientation → visibility ──────────────────────────────────

/// <summary>TerminalSplitOrientation != None → Visible, else Collapsed.</summary>
[ValueConversion(typeof(TerminalSplitOrientation), typeof(Visibility))]
public sealed class SplitActiveToVisibilityConverter : IValueConverter
{
    public static readonly SplitActiveToVisibilityConverter Instance = new();

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is TerminalSplitOrientation o && o != TerminalSplitOrientation.None
            ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>TerminalSplitOrientation == Horizontal → Vertical columns; Vertical → Horizontal rows.</summary>
[ValueConversion(typeof(TerminalSplitOrientation), typeof(Orientation))]
public sealed class SplitOrientationConverter : IValueConverter
{
    public static readonly SplitOrientationConverter Instance = new();

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is TerminalSplitOrientation.Horizontal
            ? Orientation.Horizontal : Orientation.Vertical;

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>Converts hex color string to SolidColorBrush.</summary>
[ValueConversion(typeof(string), typeof(Brush))]
public sealed class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    public object Convert(object value, Type _, object __, CultureInfo ___)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                brush.Freeze();
                return brush;
            }
            catch { }
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>GitBranchInfo → formatted prompt string.</summary>
[ValueConversion(typeof(Services.GitBranchInfo), typeof(string))]
public sealed class GitBranchInfoToStringConverter : IValueConverter
{
    public static readonly GitBranchInfoToStringConverter Instance = new();

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is Services.GitBranchInfo info ? $"\ue0a0 {info.FormatPrompt()}" : string.Empty;

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>GitBranchInfo → color brush (green=clean, yellow=dirty).</summary>
[ValueConversion(typeof(Services.GitBranchInfo), typeof(Brush))]
public sealed class GitBranchColorConverter : IValueConverter
{
    public static readonly GitBranchColorConverter Instance = new();

    private static readonly Brush CleanBrush = new SolidColorBrush(Color.FromRgb(0x16, 0xC6, 0x0C));
    private static readonly Brush DirtyBrush = new SolidColorBrush(Color.FromRgb(0xF9, 0xF1, 0xA5));
    private static readonly Brush NoBrush    = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60));

    static GitBranchColorConverter()
    {
        CleanBrush.Freeze(); DirtyBrush.Freeze(); NoBrush.Freeze();
    }

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is Services.GitBranchInfo info
            ? (info.IsDirty ? DirtyBrush : CleanBrush)
            : NoBrush;

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

// ── ViewMode equality → Visibility ──────────────────────────────────────────

/// <summary>ViewMode == parameter → Visible, else Collapsed.</summary>
[ValueConversion(typeof(ViewModels.ViewMode), typeof(Visibility))]
public sealed class ViewModeToVisibilityConverter : IValueConverter
{
    public static readonly ViewModeToVisibilityConverter Instance = new();

    public object Convert(object value, Type _, object parameter, CultureInfo ___)
    {
        if (value is ViewModels.ViewMode mode
            && parameter is string s
            && Enum.TryParse<ViewModels.ViewMode>(s, out var target))
            return mode == target ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>ViewMode != parameter → Visible, else Collapsed. (inverse of above)</summary>
[ValueConversion(typeof(ViewModels.ViewMode), typeof(Visibility))]
public sealed class ViewModeNotEqualToVisibilityConverter : IValueConverter
{
    public static readonly ViewModeNotEqualToVisibilityConverter Instance = new();

    public object Convert(object value, Type _, object parameter, CultureInfo ___)
    {
        if (value is ViewModels.ViewMode mode
            && parameter is string s
            && Enum.TryParse<ViewModels.ViewMode>(s, out var target))
            return mode != target ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Visible;
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

// ── Settings category key → Visibility ──────────────────────────────────────

/// <summary>
/// Compares a category key string to the ConverterParameter.
/// Returns Visible when they match, Collapsed otherwise.
/// Used by the Settings panel to show/hide category sections.
/// </summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public sealed class CategoryKeyToVisibilityConverter : IValueConverter
{
    public static readonly CategoryKeyToVisibilityConverter Instance = new();

    public object Convert(object value, Type _, object parameter, CultureInfo ___)
        => value is string key
           && parameter is string target
           && string.Equals(key, target, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
