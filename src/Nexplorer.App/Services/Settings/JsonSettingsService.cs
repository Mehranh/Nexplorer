using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Nexplorer.App.Services.Settings;

/// <summary>
/// JSON-backed implementation of <see cref="ISettingsService"/>.
/// Persists to <c>%AppData%\Explorer\settings.json</c>.
/// Supports async load, debounced auto-save, versioning, and import/export.
/// </summary>
public sealed class JsonSettingsService : ISettingsService, IDisposable
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Explorer");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        PropertyNameCaseInsensitive = true,
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _filePath;
    private Timer? _debounceTimer;
    private const int DebounceMs = 500;

    private AppSettings _current = SettingsDefaults.CreateDefault();

    public AppSettings Current => _current;
    public event Action<AppSettings>? SettingsChanged;

    public JsonSettingsService() : this(SettingsPath) { }

    /// <summary>Testable constructor that accepts a custom file path.</summary>
    internal JsonSettingsService(string filePath)
    {
        _filePath = filePath;
    }

    // ── Load ───────────────────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                _current = SettingsDefaults.CreateDefault();
                await PersistCoreAsync().ConfigureAwait(false);
                return;
            }

            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);

            // Run migration if needed
            var migrated = SettingsMigrator.MigrateIfNeeded(json);
            if (migrated is not null)
            {
                json = migrated;
                await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
            }

            _current = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions)
                       ?? SettingsDefaults.CreateDefault();
        }
        catch
        {
            // Corrupt file – fall back to defaults, keep a backup
            _current = SettingsDefaults.CreateDefault();
            TryBackupCorrupt();
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Update (debounced) ─────────────────────────────────────────────────

    public void Update(Func<AppSettings, AppSettings> mutator)
    {
        _current = mutator(_current);
        SettingsChanged?.Invoke(_current);
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ => _ = SaveNowAsync(), null, DebounceMs, Timeout.Infinite);
    }

    private async Task SaveNowAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            await PersistCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Reset ──────────────────────────────────────────────────────────────

    public async Task ResetAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _current = SettingsDefaults.CreateDefault();
            await PersistCoreAsync().ConfigureAwait(false);
            SettingsChanged?.Invoke(_current);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Export / Import ────────────────────────────────────────────────────

    public async Task ExportAsync(string filePath)
    {
        var json = JsonSerializer.Serialize(_current, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
    }

    public async Task ImportAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);

        // Validate JSON structure before accepting
        var imported = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions)
                       ?? throw new InvalidOperationException("Invalid settings file.");

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _current = imported with { Version = SettingsDefaults.CurrentVersion };
            await PersistCoreAsync().ConfigureAwait(false);
            SettingsChanged?.Invoke(_current);
        }
        finally
        {
            _lock.Release();
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task PersistCoreAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(_current, SerializerOptions);
        await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
    }

    private void TryBackupCorrupt()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var backup = _filePath + $".corrupt.{DateTime.UtcNow:yyyyMMddHHmmss}";
                File.Move(_filePath, backup);
            }
        }
        catch { /* best effort */ }
    }

    public void Dispose()
    {
        _debounceTimer?.Dispose();
        _lock.Dispose();
    }
}
