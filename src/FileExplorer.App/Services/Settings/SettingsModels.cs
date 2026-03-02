using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using FileExplorer.App.Services;

namespace FileExplorer.App.Services.Settings;

// ── Enums ──────────────────────────────────────────────────────────────────

public enum StartupBehavior { RestoreSession, OpenDefaultPath }
public enum DefaultViewMode { Details, List, Compact }
public enum ClickMode { SingleClick, DoubleClick }
public enum DefaultSortMode { NaturalSort, FolderFirst }
public enum DefaultShell { PowerShell, Cmd }
public enum AppTheme { Light, Dark, System }
public enum LogLevel { Error, Warning, Info, Debug }

// ── Immutable persisted record ─────────────────────────────────────────────

/// <summary>
/// The full persisted settings model. Immutable record for safe serialization.
/// Use <see cref="SettingsDefaults.CreateDefault"/> for factory defaults.
/// </summary>
public sealed record AppSettings
{
    [JsonPropertyName("version")]
    public int Version { get; init; } = SettingsDefaults.CurrentVersion;

    [JsonPropertyName("general")]
    public GeneralSettings General { get; init; } = new();

    [JsonPropertyName("performance")]
    public PerformanceSettings Performance { get; init; } = new();

    [JsonPropertyName("fileOperations")]
    public FileOperationSettings FileOperations { get; init; } = new();

    [JsonPropertyName("commandRunner")]
    public CommandRunnerSettings CommandRunner { get; init; } = new();

    [JsonPropertyName("appearance")]
    public AppearanceSettings Appearance { get; init; } = new();

    [JsonPropertyName("keyboard")]
    public KeyboardSettings Keyboard { get; init; } = new();

    [JsonPropertyName("advanced")]
    public AdvancedSettings Advanced { get; init; } = new();
}

// ── Category records ───────────────────────────────────────────────────────

public sealed record GeneralSettings
{
    [JsonPropertyName("startupBehavior")]
    public StartupBehavior StartupBehavior { get; init; } = StartupBehavior.RestoreSession;

    [JsonPropertyName("defaultViewMode")]
    public DefaultViewMode DefaultViewMode { get; init; } = DefaultViewMode.Details;

    [JsonPropertyName("clickMode")]
    public ClickMode ClickMode { get; init; } = ClickMode.DoubleClick;

    [JsonPropertyName("confirmBeforeDelete")]
    public bool ConfirmBeforeDelete { get; init; } = true;

    [JsonPropertyName("showHiddenFiles")]
    public bool ShowHiddenFiles { get; init; }

    [JsonPropertyName("showSystemFiles")]
    public bool ShowSystemFiles { get; init; }

    [JsonPropertyName("defaultSortMode")]
    public DefaultSortMode DefaultSortMode { get; init; } = DefaultSortMode.FolderFirst;

    [JsonPropertyName("language")]
    public string Language { get; init; } = "en-US";
}

public sealed record PerformanceSettings
{
    [JsonPropertyName("enableAggressiveCaching")]
    public bool EnableAggressiveCaching { get; init; } = true;

    [JsonPropertyName("maxIconCacheSizeMb")]
    [Range(16, 1024)]
    public int MaxIconCacheSizeMb { get; init; } = 128;

    [JsonPropertyName("maxPreviewCacheSizeMb")]
    [Range(16, 2048)]
    public int MaxPreviewCacheSizeMb { get; init; } = 256;

    [JsonPropertyName("maxConcurrentFileOperations")]
    [Range(1, 32)]
    public int MaxConcurrentFileOperations { get; init; } = 4;

    [JsonPropertyName("enableUsnJournalMonitoring")]
    public bool EnableUsnJournalMonitoring { get; init; }

    [JsonPropertyName("enumerationBatchSize")]
    [Range(50, 10000)]
    public int EnumerationBatchSize { get; init; } = 500;

    [JsonPropertyName("throttleBackgroundMetadataMs")]
    [Range(0, 5000)]
    public int ThrottleBackgroundMetadataMs { get; init; } = 100;
}

public sealed record FileOperationSettings
{
    [JsonPropertyName("defaultConflictResolution")]
    public ConflictResolution DefaultConflictResolution { get; init; } = ConflictResolution.Ask;

    [JsonPropertyName("enableCopyQueue")]
    public bool EnableCopyQueue { get; init; } = true;

    [JsonPropertyName("enablePauseResume")]
    public bool EnablePauseResume { get; init; } = true;

    [JsonPropertyName("moveToRecycleBin")]
    public bool MoveToRecycleBin { get; init; } = true;

    [JsonPropertyName("useLongPathSupport")]
    public bool UseLongPathSupport { get; init; } = true;
}

public sealed record CommandRunnerSettings
{
    [JsonPropertyName("defaultShell")]
    public DefaultShell DefaultShell { get; init; } = DefaultShell.PowerShell;

    [JsonPropertyName("enableCommandHistory")]
    public bool EnableCommandHistory { get; init; } = true;

    [JsonPropertyName("maxHistoryEntries")]
    [Range(10, 10000)]
    public int MaxHistoryEntries { get; init; } = 500;

    [JsonPropertyName("enableFuzzyHistorySearch")]
    public bool EnableFuzzyHistorySearch { get; init; } = true;

    [JsonPropertyName("enableAutoSuggestions")]
    public bool EnableAutoSuggestions { get; init; } = true;

    [JsonPropertyName("enableCommandTemplates")]
    public bool EnableCommandTemplates { get; init; } = true;
}

public sealed record AppearanceSettings
{
    [JsonPropertyName("theme")]
    public AppTheme Theme { get; init; } = AppTheme.Dark;

    [JsonPropertyName("accentColor")]
    public string AccentColor { get; init; } = "#0078D4";

    [JsonPropertyName("compactDensityMode")]
    public bool CompactDensityMode { get; init; }

    [JsonPropertyName("showPreviewPane")]
    public bool ShowPreviewPane { get; init; }

    [JsonPropertyName("showFolderTree")]
    public bool ShowFolderTree { get; init; } = true;

    [JsonPropertyName("enableAnimations")]
    public bool EnableAnimations { get; init; } = true;

    [JsonPropertyName("commandLineFontFamily")]
    public string CommandLineFontFamily { get; init; } = "Cascadia Mono";

    [JsonPropertyName("commandLineFontSize")]
    [Range(8, 36)]
    public int CommandLineFontSize { get; init; } = 14;
}

public sealed record KeyboardSettings
{
    [JsonPropertyName("customBindings")]
    public Dictionary<string, string> CustomBindings { get; init; } = new();

    [JsonPropertyName("enableFarStyleKeybindings")]
    public bool EnableFarStyleKeybindings { get; init; }
}

public sealed record AdvancedSettings
{
    [JsonPropertyName("enableDiagnosticsLogging")]
    public bool EnableDiagnosticsLogging { get; init; }

    [JsonPropertyName("logLevel")]
    public LogLevel LogLevel { get; init; } = LogLevel.Warning;

    [JsonPropertyName("enableCrashReporting")]
    public bool EnableCrashReporting { get; init; } = true;

    [JsonPropertyName("enableExperimentalFeatures")]
    public bool EnableExperimentalFeatures { get; init; }
}

// ── Defaults factory ───────────────────────────────────────────────────────

public static class SettingsDefaults
{
    public const int CurrentVersion = 2;

    public static AppSettings CreateDefault() => new();
}
