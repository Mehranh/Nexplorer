namespace Nexplorer.App.Services.Settings;

/// <summary>
/// Abstraction over settings persistence and access.
/// Consumers read/write the mutable <see cref="Current"/> snapshot;
/// changes are auto-saved with debounce.
/// </summary>
public interface ISettingsService
{
    /// <summary>The currently loaded settings snapshot.</summary>
    AppSettings Current { get; }

    /// <summary>Raised after settings are applied (saved to disk).</summary>
    event Action<AppSettings>? SettingsChanged;

    /// <summary>Load settings from disk asynchronously.</summary>
    Task LoadAsync();

    /// <summary>
    /// Apply a change to the settings. The <paramref name="mutator"/> receives the
    /// current snapshot and returns the modified version. Persist is debounced.
    /// </summary>
    void Update(Func<AppSettings, AppSettings> mutator);

    /// <summary>Reset all settings to factory defaults.</summary>
    Task ResetAsync();

    /// <summary>Export current settings to the given file path.</summary>
    Task ExportAsync(string filePath);

    /// <summary>Import settings from the given file path.</summary>
    Task ImportAsync(string filePath);
}
