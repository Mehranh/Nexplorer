using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Nexplorer.App.Services;
using Nexplorer.App.ViewModels;

namespace Nexplorer.App.Converters;

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

    public object Convert(object value, Type _, object __, CultureInfo ___)
    {
        var res = Application.Current.Resources;
        if (value is true)
            return res["PaneBg"] as Brush ?? Brushes.White;
        return res["HeaderBg"] as Brush ?? Brushes.LightGray;
    }

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

    private static readonly Brush FolderBrushDark = new SolidColorBrush(Color.FromRgb(0xDC, 0xB8, 0x6C));
    private static readonly Brush FileBrushDark   = new SolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE));
    private static readonly Brush FolderBrushLight = new SolidColorBrush(Color.FromRgb(0xC4, 0x9A, 0x00));
    private static readonly Brush FileBrushLight   = new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5));

    static FileIconColorConverter()
    {
        FolderBrushDark.Freeze(); FileBrushDark.Freeze();
        FolderBrushLight.Freeze(); FileBrushLight.Freeze();
    }

    public object Convert(object value, Type _, object __, CultureInfo ___)
    {
        var isLight = IsLightTheme();
        if (value is true)
            return isLight ? FolderBrushLight : FolderBrushDark;
        return isLight ? FileBrushLight : FileBrushDark;
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();

    internal static bool IsLightTheme()
    {
        if (Application.Current.Resources["AppBg"] is SolidColorBrush bg)
        {
            var c = bg.Color;
            return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 128;
        }
        return false;
    }
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

    private static readonly Brush PsBrushDark  = new SolidColorBrush(Color.FromRgb(0x2D, 0xCF, 0xFF));
    private static readonly Brush CmdBrushDark = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
    private static readonly Brush PsBrushLight  = new SolidColorBrush(Color.FromRgb(0x00, 0x6B, 0xBD));
    private static readonly Brush CmdBrushLight = new SolidColorBrush(Color.FromRgb(0xB8, 0x86, 0x00));

    static ShellPromptColorConverter()
    {
        PsBrushDark.Freeze(); CmdBrushDark.Freeze();
        PsBrushLight.Freeze(); CmdBrushLight.Freeze();
    }

    public object Convert(object value, Type _, object __, CultureInfo ___)
    {
        var isLight = FileIconColorConverter.IsLightTheme();
        return value is ShellKind.Cmd
            ? (isLight ? CmdBrushLight : CmdBrushDark)
            : (isLight ? PsBrushLight : PsBrushDark);
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

/// <summary>SuggestionKind → accent brush for the popup icon.</summary>
[ValueConversion(typeof(SuggestionKind), typeof(Brush))]
public sealed class SuggestionKindColorConverter : IValueConverter
{
    public static readonly SuggestionKindColorConverter Instance = new();

    private static readonly Brush HistoryBrushDark = new SolidColorBrush(Color.FromRgb(0x9B, 0xDB, 0xFF));
    private static readonly Brush FsBrushDark      = new SolidColorBrush(Color.FromRgb(0xDC, 0xB8, 0x6C));
    private static readonly Brush BangBrushDark    = new SolidColorBrush(Color.FromRgb(0xC5, 0x86, 0xC0));
    private static readonly Brush HistoryBrushLight = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));
    private static readonly Brush FsBrushLight      = new SolidColorBrush(Color.FromRgb(0xC4, 0x9A, 0x00));
    private static readonly Brush BangBrushLight    = new SolidColorBrush(Color.FromRgb(0x7B, 0x1F, 0xA2));

    static SuggestionKindColorConverter()
    {
        HistoryBrushDark.Freeze(); FsBrushDark.Freeze(); BangBrushDark.Freeze();
        HistoryBrushLight.Freeze(); FsBrushLight.Freeze(); BangBrushLight.Freeze();
    }

    public object Convert(object value, Type _, object __, CultureInfo ___)
    {
        var isLight = FileIconColorConverter.IsLightTheme();
        return value switch
        {
            SuggestionKind.FileSystem => isLight ? FsBrushLight : FsBrushDark,
            SuggestionKind.BangCommand => isLight ? BangBrushLight : BangBrushDark,
            _ => isLight ? HistoryBrushLight : HistoryBrushDark
        };
    }
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

    private static readonly Brush CleanBrushDark = new SolidColorBrush(Color.FromRgb(0x16, 0xC6, 0x0C));
    private static readonly Brush DirtyBrushDark = new SolidColorBrush(Color.FromRgb(0xF9, 0xF1, 0xA5));
    private static readonly Brush NoBrushDark    = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60));
    private static readonly Brush CleanBrushLight = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
    private static readonly Brush DirtyBrushLight = new SolidColorBrush(Color.FromRgb(0xC4, 0x9A, 0x00));
    private static readonly Brush NoBrushLight    = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));

    static GitBranchColorConverter()
    {
        CleanBrushDark.Freeze(); DirtyBrushDark.Freeze(); NoBrushDark.Freeze();
        CleanBrushLight.Freeze(); DirtyBrushLight.Freeze(); NoBrushLight.Freeze();
    }

    public object Convert(object value, Type _, object __, CultureInfo ___)
    {
        var isLight = FileIconColorConverter.IsLightTheme();
        if (value is Services.GitBranchInfo info)
            return info.IsDirty
                ? (isLight ? DirtyBrushLight : DirtyBrushDark)
                : (isLight ? CleanBrushLight : CleanBrushDark);
        return isLight ? NoBrushLight : NoBrushDark;
    }

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

// ── NotificationType → accent colour ────────────────────────────────────────

[ValueConversion(typeof(NotificationType), typeof(Brush))]
public sealed class NotificationTypeToBrushConverter : IValueConverter
{
    private static readonly Brush InfoBrush    = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
    private static readonly Brush SuccessBrush = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
    private static readonly Brush WarnBrush    = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
    private static readonly Brush ErrorBrush   = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));

    static NotificationTypeToBrushConverter()
    {
        InfoBrush.Freeze(); SuccessBrush.Freeze(); WarnBrush.Freeze(); ErrorBrush.Freeze();
    }

    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is NotificationType t ? t switch
        {
            NotificationType.Success => SuccessBrush,
            NotificationType.Warning => WarnBrush,
            NotificationType.Error   => ErrorBrush,
            _                        => InfoBrush,
        } : InfoBrush;

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

[ValueConversion(typeof(NotificationType), typeof(string))]
public sealed class NotificationTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type _, object __, CultureInfo ___)
        => value is NotificationType t ? t switch
        {
            NotificationType.Success => "CheckCircleOutline",
            NotificationType.Warning => "AlertOutline",
            NotificationType.Error   => "CloseCircleOutline",
            _                        => "InformationOutline",
        } : "InformationOutline";

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
