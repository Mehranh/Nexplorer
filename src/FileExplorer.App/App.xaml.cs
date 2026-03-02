using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using FileExplorer.App.Services;
using FileExplorer.App.Services.Settings;

namespace FileExplorer.App;

public partial class App : Application
{
    internal static readonly JsonSettingsService SettingsService = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await SettingsService.LoadAsync();
        ApplyTheme(SettingsService.Current.Appearance.Theme);
        SettingsService.SettingsChanged += s => Dispatcher.Invoke(() => ApplyTheme(s.Appearance.Theme));

        // Check for updates in background after the window is shown
        _ = CheckForUpdateAsync();
    }

    private static async Task CheckForUpdateAsync()
    {
        // Small delay so the main window is fully loaded before showing a dialog
        await Task.Delay(3000).ConfigureAwait(false);

        var svc = new UpdateService();
        var update = await svc.CheckForUpdateAsync().ConfigureAwait(false);
        if (update is null) return;

        // Must show MessageBox on the UI thread
        Current.Dispatcher.Invoke(() =>
        {
            var result = MessageBox.Show(
                $"Nexplorer v{update.Version} is available (you have v{UpdateService.CurrentVersion.ToString(3)}).\n\n" +
                $"{update.ReleaseNotes}\n\nWould you like to download it now?",
                "Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo(update.DownloadUrl) { UseShellExecute = true });
            }
        });
    }

    internal static void ApplyTheme(AppTheme theme)
    {
        var effective = theme;
        if (effective == AppTheme.System)
        {
            // Detect Windows theme: HKCU\...\Themes\Personalize AppsUseLightTheme (0 = dark)
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                var val = key?.GetValue("AppsUseLightTheme");
                effective = val is int i && i == 1 ? AppTheme.Light : AppTheme.Dark;
            }
            catch
            {
                effective = AppTheme.Dark;
            }
        }

        var res = Current.Resources;
        if (effective == AppTheme.Light)
        {
            SetBrush(res, "AppBg",           "#F5F5F5");
            SetBrush(res, "PaneBg",          "#FFFFFF");
            SetBrush(res, "TreeBg",          "#F3F3F3");
            SetBrush(res, "HeaderBg",        "#E8E8E8");
            SetBrush(res, "StatusBg",        "#F3F3F3");
            SetBrush(res, "TerminalBg",      "#FFFFFF");
            SetBrush(res, "InputBg",         "#FFFFFF");
            SetBrush(res, "BorderBrush",     "#D4D4D4");
            SetBrush(res, "TextFg",          "#1E1E1E");
            SetBrush(res, "SubTextFg",       "#6E6E6E");
            SetBrush(res, "AccentBrush",     "#0078D4");
            SetBrush(res, "HoverBg",         "#E5E5E5");
            SetBrush(res, "SelBg",           "#CCE4F7");
            SetBrush(res, "SelBgInactive",   "#E0E0E0");
            SetBrush(res, "ActivePaneBorder","#0078D4");
            SetBrush(res, "ButtonBg",        "#E0E0E0");
            SetBrush(res, "ButtonHoverBg",   "#D0D0D0");
            SetBrush(res, "ButtonPressedBg", "#C0C0C0");
            SetBrush(res, "InactiveTextFg",  "#999999");
            SetBrush(res, "DangerBorder",    "#D44040");
            SetBrush(res, "DangerHoverBg",   "#F5D0D0");
        }
        else
        {
            SetBrush(res, "AppBg",           "#1C1C1C");
            SetBrush(res, "PaneBg",          "#252526");
            SetBrush(res, "TreeBg",          "#1E1E1E");
            SetBrush(res, "HeaderBg",        "#2D2D30");
            SetBrush(res, "StatusBg",        "#1E1E1E");
            SetBrush(res, "TerminalBg",      "#0C0C0C");
            SetBrush(res, "InputBg",         "#1E1E1E");
            SetBrush(res, "BorderBrush",     "#3F3F46");
            SetBrush(res, "TextFg",          "#CCCCCC");
            SetBrush(res, "SubTextFg",       "#858585");
            SetBrush(res, "AccentBrush",     "#0078D4");
            SetBrush(res, "HoverBg",         "#2A2D2E");
            SetBrush(res, "SelBg",           "#094771");
            SetBrush(res, "SelBgInactive",   "#3A3A3A");
            SetBrush(res, "ActivePaneBorder","#007ACC");
            SetBrush(res, "ButtonBg",        "#333333");
            SetBrush(res, "ButtonHoverBg",   "#3C3C3C");
            SetBrush(res, "ButtonPressedBg", "#555555");
            SetBrush(res, "InactiveTextFg",  "#777777");
            SetBrush(res, "DangerBorder",    "#8B3030");
            SetBrush(res, "DangerHoverBg",   "#5A2020");
        }
    }

    private static void SetBrush(ResourceDictionary res, string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        res[key] = new SolidColorBrush(color);
    }
}
