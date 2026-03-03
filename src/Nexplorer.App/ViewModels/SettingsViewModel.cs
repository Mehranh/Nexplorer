using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexplorer.App.Services;
using Nexplorer.App.Services.Settings;
using Microsoft.Win32;

namespace Nexplorer.App.ViewModels;

// ── Category descriptor ────────────────────────────────────────────────────

public sealed class SettingsCategory
{
    public required string Name  { get; init; }
    public required string Icon  { get; init; } // MahApps icon kind string
    public required string Key   { get; init; }
}

// ── Individual setting item (for search) ───────────────────────────────────

public sealed class SettingEntry
{
    public required string Label       { get; init; }
    public required string Category    { get; init; }
    public required string Description { get; init; }
    public bool RequiresRestart { get; init; }
}

// ── Main SettingsViewModel ─────────────────────────────────────────────────

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _service;

    public string AppVersion { get; } =
        (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
            System.Reflection.Assembly.GetExecutingAssembly()))?.InformationalVersion
        ?? System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "1.0.0";

    public SettingsViewModel(ISettingsService service)
    {
        _service = service;

        Categories = new ObservableCollection<SettingsCategory>
        {
            new() { Name = "General",           Icon = "Cog",             Key = "general" },
            new() { Name = "Performance",       Icon = "Speedometer",     Key = "performance" },
            new() { Name = "File Operations",   Icon = "FolderMove",      Key = "fileOperations" },
            new() { Name = "Command Runner",    Icon = "Console",         Key = "commandRunner" },
            new() { Name = "Appearance",        Icon = "Palette",         Key = "appearance" },
            new() { Name = "Keyboard",          Icon = "Keyboard",        Key = "keyboard" },
            new() { Name = "Advanced",          Icon = "TuneVertical",    Key = "advanced" },
            new() { Name = "About",             Icon = "InformationOutline", Key = "about" },
        };

        _selectedCategory = Categories[0];
        BuildSearchIndex();
        ApplySnapshot(_service.Current);
    }

    // ── Categories ─────────────────────────────────────────────────────────

    public ObservableCollection<SettingsCategory> Categories { get; }

    [ObservableProperty]
    private SettingsCategory _selectedCategory;

    // ── Search ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredSettings))]
    private string _searchText = string.Empty;

    private List<SettingEntry> _allSettings = new();

    public IReadOnlyList<SettingEntry> FilteredSettings =>
        string.IsNullOrWhiteSpace(SearchText)
            ? _allSettings
            : _allSettings.Where(s =>
                s.Label.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                s.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
              .ToList();

    // ── General settings (bindable) ────────────────────────────────────────

    [ObservableProperty] private StartupBehavior _startupBehavior;
    [ObservableProperty] private DefaultViewMode _defaultViewMode;
    [ObservableProperty] private ClickMode       _clickMode;
    [ObservableProperty] private bool            _confirmBeforeDelete;
    [ObservableProperty] private bool            _showHiddenFiles;
    [ObservableProperty] private bool            _showSystemFiles;
    [ObservableProperty] private DefaultSortMode _defaultSortMode;
    [ObservableProperty] private string          _language = "en-US";

    // ── Performance settings ───────────────────────────────────────────────

    [ObservableProperty] private bool _enableAggressiveCaching;
    [ObservableProperty] private int  _maxIconCacheSizeMb;
    [ObservableProperty] private int  _maxPreviewCacheSizeMb;
    [ObservableProperty] private int  _maxConcurrentFileOperations;
    [ObservableProperty] private bool _enableUsnJournalMonitoring;
    [ObservableProperty] private int  _enumerationBatchSize;
    [ObservableProperty] private int  _throttleBackgroundMetadataMs;

    // ── File operations ────────────────────────────────────────────────────

    [ObservableProperty] private ConflictResolution _defaultConflictResolution;
    [ObservableProperty] private bool _enableCopyQueue;
    [ObservableProperty] private bool _enablePauseResume;
    [ObservableProperty] private bool _moveToRecycleBin;
    [ObservableProperty] private bool _useLongPathSupport;

    // ── Command runner ─────────────────────────────────────────────────────

    [ObservableProperty] private DefaultShell _defaultShell;
    [ObservableProperty] private bool _enableCommandHistory;
    [ObservableProperty] private int  _maxHistoryEntries;
    [ObservableProperty] private bool _enableFuzzyHistorySearch;
    [ObservableProperty] private bool _enableAutoSuggestions;
    [ObservableProperty] private bool _enableCommandTemplates;

    // ── Appearance ─────────────────────────────────────────────────────────

    [ObservableProperty] private AppTheme _theme;
    [ObservableProperty] private string   _accentColor = "#0078D4";
    [ObservableProperty] private bool     _compactDensityMode;
    [ObservableProperty] private bool     _showPreviewPane;
    [ObservableProperty] private bool     _showFolderTree;
    [ObservableProperty] private bool     _enableAnimations;
    [ObservableProperty] private string   _commandLineFontFamily = "Cascadia Mono";
    [ObservableProperty] private int      _commandLineFontSize;

    // ── Keyboard ───────────────────────────────────────────────────────────

    [ObservableProperty] private bool _enableFarStyleKeybindings;

    // ── Advanced ───────────────────────────────────────────────────────────

    [ObservableProperty] private bool     _enableDiagnosticsLogging;
    [ObservableProperty] private LogLevel _logLevel;
    [ObservableProperty] private bool     _enableCrashReporting;
    [ObservableProperty] private bool     _enableExperimentalFeatures;

    // ── Property-change → auto-save ────────────────────────────────────────

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // Skip non-setting properties
        if (e.PropertyName is nameof(SelectedCategory) or nameof(SearchText)
            or nameof(FilteredSettings) or nameof(UpdateStatus) or nameof(IsCheckingForUpdate))
            return;

        PushToService();
    }

    // ── Commands ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        await _service.ResetAsync();
        ApplySnapshot(_service.Current);
    }

    [RelayCommand]
    private async Task ExportSettingsAsync()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "JSON files|*.json",
            FileName = "explorer-settings.json"
        };
        if (dlg.ShowDialog() == true)
            await _service.ExportAsync(dlg.FileName);
    }

    [RelayCommand]
    private async Task ImportSettingsAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "JSON files|*.json"
        };
        if (dlg.ShowDialog() == true)
        {
            await _service.ImportAsync(dlg.FileName);
            ApplySnapshot(_service.Current);
        }
    }

    [RelayCommand]
#pragma warning disable CA1822 // RelayCommand source generator requires instance method
    private void ClearCommandHistory()
    {
        CommandHistoryStore.Save(Array.Empty<CommandHistoryEntry>());
    }
#pragma warning restore CA1822

    [RelayCommand]
#pragma warning disable CA1822
    private void OpenLogFolder()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Explorer", "logs");
        Directory.CreateDirectory(logDir);
        Process.Start(new ProcessStartInfo(logDir) { UseShellExecute = true });
    }
#pragma warning restore CA1822

    [ObservableProperty] private string _updateStatus = string.Empty;
    [ObservableProperty] private bool _isCheckingForUpdate;

    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        IsCheckingForUpdate = true;
        UpdateStatus = "Checking…";
        try
        {
            var result = await UpdateService.CheckForUpdateAsync().ConfigureAwait(false);
            if (result.Status is UpdateCheckStatus.UpdateAvailable && result.Update is not null)
            {
                var update = result.Update;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateStatus = $"v{update.Version} available!";
                    if (NotificationService.Confirm(
                        $"Nexplorer v{update.Version} is available (you have v{UpdateService.CurrentVersion.ToString(3)}).\n\n" +
                        $"{update.ReleaseNotes}\n\nWould you like to download it now?",
                        "Update Available"))
                        Process.Start(new ProcessStartInfo(update.DownloadUrl) { UseShellExecute = true });
                });
            }
            else if (result.Status is UpdateCheckStatus.UpToDate)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    UpdateStatus = "You're up to date!");
            }
            else
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    UpdateStatus = "Check failed — try again later.");
            }
        }
        catch
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                UpdateStatus = "Check failed — try again later.");
        }
        finally
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                IsCheckingForUpdate = false);
        }
    }

    // ── Snapshot ↔ ViewModel mapping ───────────────────────────────────────

    private void ApplySnapshot(AppSettings s)
    {
        // General
        StartupBehavior      = s.General.StartupBehavior;
        DefaultViewMode      = s.General.DefaultViewMode;
        ClickMode            = s.General.ClickMode;
        ConfirmBeforeDelete  = s.General.ConfirmBeforeDelete;
        ShowHiddenFiles      = s.General.ShowHiddenFiles;
        ShowSystemFiles      = s.General.ShowSystemFiles;
        DefaultSortMode      = s.General.DefaultSortMode;
        Language             = s.General.Language;

        // Performance
        EnableAggressiveCaching      = s.Performance.EnableAggressiveCaching;
        MaxIconCacheSizeMb           = s.Performance.MaxIconCacheSizeMb;
        MaxPreviewCacheSizeMb        = s.Performance.MaxPreviewCacheSizeMb;
        MaxConcurrentFileOperations  = s.Performance.MaxConcurrentFileOperations;
        EnableUsnJournalMonitoring   = s.Performance.EnableUsnJournalMonitoring;
        EnumerationBatchSize         = s.Performance.EnumerationBatchSize;
        ThrottleBackgroundMetadataMs = s.Performance.ThrottleBackgroundMetadataMs;

        // File operations
        DefaultConflictResolution = s.FileOperations.DefaultConflictResolution;
        EnableCopyQueue           = s.FileOperations.EnableCopyQueue;
        EnablePauseResume         = s.FileOperations.EnablePauseResume;
        MoveToRecycleBin          = s.FileOperations.MoveToRecycleBin;
        UseLongPathSupport        = s.FileOperations.UseLongPathSupport;

        // Command runner
        DefaultShell             = s.CommandRunner.DefaultShell;
        EnableCommandHistory     = s.CommandRunner.EnableCommandHistory;
        MaxHistoryEntries        = s.CommandRunner.MaxHistoryEntries;
        EnableFuzzyHistorySearch = s.CommandRunner.EnableFuzzyHistorySearch;
        EnableAutoSuggestions    = s.CommandRunner.EnableAutoSuggestions;
        EnableCommandTemplates   = s.CommandRunner.EnableCommandTemplates;

        // Appearance
        Theme                  = s.Appearance.Theme;
        AccentColor            = s.Appearance.AccentColor;
        CompactDensityMode     = s.Appearance.CompactDensityMode;
        ShowPreviewPane        = s.Appearance.ShowPreviewPane;
        ShowFolderTree         = s.Appearance.ShowFolderTree;
        EnableAnimations       = s.Appearance.EnableAnimations;
        CommandLineFontFamily  = s.Appearance.CommandLineFontFamily;
        CommandLineFontSize    = s.Appearance.CommandLineFontSize;

        // Keyboard
        EnableFarStyleKeybindings = s.Keyboard.EnableFarStyleKeybindings;

        // Advanced
        EnableDiagnosticsLogging   = s.Advanced.EnableDiagnosticsLogging;
        LogLevel                   = s.Advanced.LogLevel;
        EnableCrashReporting       = s.Advanced.EnableCrashReporting;
        EnableExperimentalFeatures = s.Advanced.EnableExperimentalFeatures;
    }

    private void PushToService()
    {
        _service.Update(s => s with
        {
            General = new GeneralSettings
            {
                StartupBehavior     = StartupBehavior,
                DefaultViewMode     = DefaultViewMode,
                ClickMode           = ClickMode,
                ConfirmBeforeDelete = ConfirmBeforeDelete,
                ShowHiddenFiles     = ShowHiddenFiles,
                ShowSystemFiles     = ShowSystemFiles,
                DefaultSortMode     = DefaultSortMode,
                Language            = Language,
            },
            Performance = new PerformanceSettings
            {
                EnableAggressiveCaching     = EnableAggressiveCaching,
                MaxIconCacheSizeMb          = MaxIconCacheSizeMb,
                MaxPreviewCacheSizeMb       = MaxPreviewCacheSizeMb,
                MaxConcurrentFileOperations = MaxConcurrentFileOperations,
                EnableUsnJournalMonitoring  = EnableUsnJournalMonitoring,
                EnumerationBatchSize        = EnumerationBatchSize,
                ThrottleBackgroundMetadataMs = ThrottleBackgroundMetadataMs,
            },
            FileOperations = new FileOperationSettings
            {
                DefaultConflictResolution = DefaultConflictResolution,
                EnableCopyQueue           = EnableCopyQueue,
                EnablePauseResume         = EnablePauseResume,
                MoveToRecycleBin          = MoveToRecycleBin,
                UseLongPathSupport        = UseLongPathSupport,
            },
            CommandRunner = new CommandRunnerSettings
            {
                DefaultShell             = DefaultShell,
                EnableCommandHistory     = EnableCommandHistory,
                MaxHistoryEntries        = MaxHistoryEntries,
                EnableFuzzyHistorySearch = EnableFuzzyHistorySearch,
                EnableAutoSuggestions    = EnableAutoSuggestions,
                EnableCommandTemplates   = EnableCommandTemplates,
            },
            Appearance = new AppearanceSettings
            {
                Theme                 = Theme,
                AccentColor           = AccentColor,
                CompactDensityMode    = CompactDensityMode,
                ShowPreviewPane       = ShowPreviewPane,
                ShowFolderTree        = ShowFolderTree,
                EnableAnimations      = EnableAnimations,
                CommandLineFontFamily = CommandLineFontFamily,
                CommandLineFontSize   = CommandLineFontSize,
            },
            Keyboard = new KeyboardSettings
            {
                EnableFarStyleKeybindings = EnableFarStyleKeybindings,
                CustomBindings            = s.Keyboard.CustomBindings,
            },
            Advanced = new AdvancedSettings
            {
                EnableDiagnosticsLogging   = EnableDiagnosticsLogging,
                LogLevel                   = LogLevel,
                EnableCrashReporting       = EnableCrashReporting,
                EnableExperimentalFeatures = EnableExperimentalFeatures,
            },
        });
    }

    // ── Search index ───────────────────────────────────────────────────────

    private void BuildSearchIndex()
    {
        _allSettings = new List<SettingEntry>
        {
            // General
            new() { Label = "Startup Behavior",     Category = "General", Description = "Restore session or open default path" },
            new() { Label = "Default View Mode",     Category = "General", Description = "Details, List, or Compact view" },
            new() { Label = "Click Mode",            Category = "General", Description = "Single-click or double-click to open" },
            new() { Label = "Confirm Before Delete", Category = "General", Description = "Ask confirmation before deleting files" },
            new() { Label = "Show Hidden Files",     Category = "General", Description = "Show files with Hidden attribute" },
            new() { Label = "Show System Files",     Category = "General", Description = "Show files with System attribute" },
            new() { Label = "Default Sort",          Category = "General", Description = "Natural sort, folder-first ordering" },
            new() { Label = "Language",              Category = "General", Description = "UI language (future support)", RequiresRestart = true },

            // Performance
            new() { Label = "Aggressive Caching",         Category = "Performance", Description = "Cache directory listings aggressively" },
            new() { Label = "Icon Cache Size",             Category = "Performance", Description = "Maximum icon cache size in MB" },
            new() { Label = "Preview Cache Size",          Category = "Performance", Description = "Maximum preview cache size in MB" },
            new() { Label = "Concurrent Operations",       Category = "Performance", Description = "Max concurrent file operations" },
            new() { Label = "USN Journal Monitoring",      Category = "Performance", Description = "Use USN Journal for change detection", RequiresRestart = true },
            new() { Label = "Enumeration Batch Size",      Category = "Performance", Description = "Items loaded per batch during navigation" },
            new() { Label = "Background Metadata Throttle",Category = "Performance", Description = "Throttle (ms) for background metadata loading" },

            // File Operations
            new() { Label = "Conflict Resolution",   Category = "File Operations", Description = "Default action on file name conflicts" },
            new() { Label = "Copy Queue",             Category = "File Operations", Description = "Queue file copies instead of parallel" },
            new() { Label = "Pause / Resume",         Category = "File Operations", Description = "Allow pausing and resuming copy operations" },
            new() { Label = "Recycle Bin",             Category = "File Operations", Description = "Move to Recycle Bin by default" },
            new() { Label = "Long Path Support",       Category = "File Operations", Description = "Enable paths longer than 260 chars" },

            // Command Runner
            new() { Label = "Default Shell",          Category = "Command Runner", Description = "PowerShell or cmd" },
            new() { Label = "Command History",        Category = "Command Runner", Description = "Track executed commands" },
            new() { Label = "Max History Entries",    Category = "Command Runner", Description = "Number of history entries to keep" },
            new() { Label = "Fuzzy History Search",   Category = "Command Runner", Description = "Enable fuzzy matching in history" },
            new() { Label = "Auto Suggestions",       Category = "Command Runner", Description = "Suggest files and folders while typing" },
            new() { Label = "Command Templates",      Category = "Command Runner", Description = "Enable reusable command templates" },

            // Appearance
            new() { Label = "Theme",                  Category = "Appearance", Description = "Light, Dark, or follow System theme" },
            new() { Label = "Accent Color",           Category = "Appearance", Description = "Primary accent color (#hex)" },
            new() { Label = "Compact Density",        Category = "Appearance", Description = "Reduce spacing between items" },
            new() { Label = "Preview Pane",           Category = "Appearance", Description = "Show preview pane by default" },
            new() { Label = "Folder Tree",            Category = "Appearance", Description = "Show folder tree panel" },
            new() { Label = "Animations",             Category = "Appearance", Description = "Enable UI animations" },
            new() { Label = "Font Family",            Category = "Appearance", Description = "Command line font family", RequiresRestart = true },
            new() { Label = "Font Size",              Category = "Appearance", Description = "Command line font size" },

            // Keyboard
            new() { Label = "Far-style Keybindings",  Category = "Keyboard", Description = "Enable Far Manager compatible shortcuts" },

            // Advanced
            new() { Label = "Diagnostics Logging",    Category = "Advanced", Description = "Write diagnostic logs to disk" },
            new() { Label = "Log Level",               Category = "Advanced", Description = "Minimum log severity to capture" },
            new() { Label = "Crash Reporting",         Category = "Advanced", Description = "Send anonymous crash reports" },
            new() { Label = "Experimental Features",   Category = "Advanced", Description = "Enable experimental / preview features" },
        };
    }
}
