using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Nexplorer.App.Services;
using Nexplorer.App.Services.Settings;

namespace Nexplorer.App;

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
        var result = await svc.CheckForUpdateAsync().ConfigureAwait(false);
        if (result.Status is not UpdateCheckStatus.UpdateAvailable || result.Update is null) return;
        var update = result.Update;

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

            SetBrush(res, "DiffBg",              "#FAFAFA");
            SetBrush(res, "DiffAddedHeaderBg",   "#D4EDDA");
            SetBrush(res, "DiffRemovedHeaderBg", "#F8D7DA");
            SetBrush(res, "DiffAddedFg",         "#2E7D32");
            SetBrush(res, "DiffRemovedFg",       "#C62828");
            SetBrush(res, "DiffAddedSubFg",      "#66BB6A");
            SetBrush(res, "DiffRemovedSubFg",    "#EF5350");
            SetBrush(res, "DiffUnchangedFg",     "#333333");
            SetBrush(res, "DiffLineNumFg",       "#999999");
            SetBrush(res, "GitStatusBg",         "#E8F5E9");
            SetBrush(res, "GitStatusFg",         "#2E7D32");
            SetBrush(res, "GitAvatarBg",         "#C8E6C9");
            SetBrush(res, "GitAvatarFg",         "#388E3C");
            SetBrush(res, "FolderNameFg",        "#C49A00");
            SetBrush(res, "HashFg",              "#1565C0");
            SetBrush(res, "HistorySearchLabelFg", "#00838F");
            SetBrush(res, "HistorySearchResultFg", "#F57F17");
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

            SetBrush(res, "DiffBg",              "#1A1A1A");
            SetBrush(res, "DiffAddedHeaderBg",   "#152D15");
            SetBrush(res, "DiffRemovedHeaderBg", "#2D1515");
            SetBrush(res, "DiffAddedFg",         "#4EC94E");
            SetBrush(res, "DiffRemovedFg",       "#F14C4C");
            SetBrush(res, "DiffAddedSubFg",      "#448844");
            SetBrush(res, "DiffRemovedSubFg",    "#884444");
            SetBrush(res, "DiffUnchangedFg",     "#AAAAAA");
            SetBrush(res, "DiffLineNumFg",       "#555555");
            SetBrush(res, "GitStatusBg",         "#1E2D1E");
            SetBrush(res, "GitStatusFg",         "#73C991");
            SetBrush(res, "GitAvatarBg",         "#2D4A2D");
            SetBrush(res, "GitAvatarFg",         "#7FCC7F");
            SetBrush(res, "FolderNameFg",        "#DCB86C");
            SetBrush(res, "HashFg",              "#569CD6");
            SetBrush(res, "HistorySearchLabelFg", "#61D6D6");
            SetBrush(res, "HistorySearchResultFg", "#F9F1A5");
        }
    }

    private static void SetBrush(ResourceDictionary res, string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        res[key] = new SolidColorBrush(color);
    }
}
