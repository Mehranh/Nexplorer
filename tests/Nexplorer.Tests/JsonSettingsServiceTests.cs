using System.IO;
using System.Text.Json;
using Nexplorer.App.Services;
using Nexplorer.App.Services.Settings;

namespace Nexplorer.Tests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public JsonSettingsServiceTests()
    {
        _tempDir  = Path.Combine(Path.GetTempPath(), "FE_Tests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private JsonSettingsService CreateService() => new(_filePath);

    // ── Load / defaults ────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_WhenNoFile_CreatesDefaults()
    {
        using var svc = CreateService();
        await svc.LoadAsync();

        Assert.Equal(SettingsDefaults.CurrentVersion, svc.Current.Version);
        Assert.True(svc.Current.General.ConfirmBeforeDelete);
        Assert.Equal(DefaultViewMode.Details, svc.Current.General.DefaultViewMode);
    }

    [Fact]
    public async Task LoadAsync_WhenNoFile_PersistsDefaultsToDisk()
    {
        using var svc = CreateService();
        await svc.LoadAsync();

        Assert.True(File.Exists(_filePath));
        var json = await File.ReadAllTextAsync(_filePath);
        Assert.Contains("confirmBeforeDelete", json);
    }

    // ── Update / debounce ──────────────────────────────────────────────────

    [Fact]
    public async Task Update_ChangesCurrentSnapshot()
    {
        using var svc = CreateService();
        await svc.LoadAsync();

        svc.Update(s => s with
        {
            General = s.General with { ShowHiddenFiles = true }
        });

        Assert.True(svc.Current.General.ShowHiddenFiles);
    }

    [Fact]
    public async Task Update_FiresSettingsChangedEvent()
    {
        using var svc = CreateService();
        await svc.LoadAsync();

        AppSettings? received = null;
        svc.SettingsChanged += s => received = s;

        svc.Update(s => s with
        {
            General = s.General with { ShowHiddenFiles = true }
        });

        Assert.NotNull(received);
        Assert.True(received!.General.ShowHiddenFiles);
    }

    [Fact]
    public async Task Update_DebouncedSaveToDisk()
    {
        using var svc = CreateService();
        await svc.LoadAsync();

        svc.Update(s => s with
        {
            Performance = s.Performance with { MaxIconCacheSizeMb = 512 }
        });

        // Wait for debounce
        await Task.Delay(800);

        var json = await File.ReadAllTextAsync(_filePath);
        Assert.Contains("512", json);
    }

    // ── Reset ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResetAsync_RestoresDefaults()
    {
        using var svc = CreateService();
        await svc.LoadAsync();

        svc.Update(s => s with
        {
            General = s.General with { ShowHiddenFiles = true, ConfirmBeforeDelete = false }
        });

        await svc.ResetAsync();

        Assert.False(svc.Current.General.ShowHiddenFiles);
        Assert.True(svc.Current.General.ConfirmBeforeDelete);
    }

    // ── Export / Import ────────────────────────────────────────────────────

    [Fact]
    public async Task ExportAsync_WritesFile()
    {
        using var svc = CreateService();
        await svc.LoadAsync();

        var exportPath = Path.Combine(_tempDir, "export.json");
        await svc.ExportAsync(exportPath);

        Assert.True(File.Exists(exportPath));
        var json = await File.ReadAllTextAsync(exportPath);
        Assert.Contains("general", json);
    }

    [Fact]
    public async Task ImportAsync_AppliesSettings()
    {
        using var svc = CreateService();
        await svc.LoadAsync();

        // Create a file with custom settings
        var importSettings = SettingsDefaults.CreateDefault() with
        {
            General = new GeneralSettings { ShowHiddenFiles = true, ShowSystemFiles = true }
        };
        var importPath = Path.Combine(_tempDir, "import.json");
        await File.WriteAllTextAsync(importPath,
            JsonSerializer.Serialize(importSettings, JsonSettingsService.SerializerOptions));

        await svc.ImportAsync(importPath);

        Assert.True(svc.Current.General.ShowHiddenFiles);
        Assert.True(svc.Current.General.ShowSystemFiles);
    }

    // ── Corrupt file handling ──────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_CorruptFile_FallsBackToDefaults()
    {
        await File.WriteAllTextAsync(_filePath, "THIS IS NOT JSON!!!");

        using var svc = CreateService();
        await svc.LoadAsync();

        // Should fall back to defaults
        Assert.Equal(SettingsDefaults.CurrentVersion, svc.Current.Version);
        Assert.True(svc.Current.General.ConfirmBeforeDelete);
    }

    // ── Migration ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAsync_V1File_MigratesToCurrentVersion()
    {
        var v1 = new
        {
            version = 1,
            general = new { confirmBeforeDelete = false, showHiddenFiles = true },
            performance = new { maxIconCacheSizeMb = 64 },
            commandRunner = new { shellType = "PowerShell" }
        };
        await File.WriteAllTextAsync(_filePath,
            JsonSerializer.Serialize(v1, new JsonSerializerOptions { WriteIndented = true }));

        using var svc = CreateService();
        await svc.LoadAsync();

        Assert.Equal(SettingsDefaults.CurrentVersion, svc.Current.Version);
        // V1 values should be preserved
        Assert.False(svc.Current.General.ConfirmBeforeDelete);
        Assert.True(svc.Current.General.ShowHiddenFiles);
    }

    // ── All categories round-trip ──────────────────────────────────────────

    [Fact]
    public async Task RoundTrip_AllCategories()
    {
        using var svc = CreateService();
        await svc.LoadAsync();

        svc.Update(s => s with
        {
            General        = s.General with { ClickMode = ClickMode.SingleClick },
            Performance    = s.Performance with { EnumerationBatchSize = 1000 },
            FileOperations = s.FileOperations with { DefaultConflictResolution = ConflictResolution.Skip },
            CommandRunner  = s.CommandRunner with { DefaultShell = DefaultShell.Cmd },
            Appearance     = s.Appearance with { Theme = AppTheme.Light, CommandLineFontSize = 18 },
            Keyboard       = s.Keyboard with { EnableFarStyleKeybindings = true },
            Advanced       = s.Advanced with { LogLevel = LogLevel.Debug },
        });

        // Wait for debounce + reload
        await Task.Delay(800);

        using var svc2 = CreateService();
        await svc2.LoadAsync();

        Assert.Equal(ClickMode.SingleClick,          svc2.Current.General.ClickMode);
        Assert.Equal(1000,                           svc2.Current.Performance.EnumerationBatchSize);
        Assert.Equal(ConflictResolution.Skip,        svc2.Current.FileOperations.DefaultConflictResolution);
        Assert.Equal(DefaultShell.Cmd,               svc2.Current.CommandRunner.DefaultShell);
        Assert.Equal(AppTheme.Light,                 svc2.Current.Appearance.Theme);
        Assert.Equal(18,                             svc2.Current.Appearance.CommandLineFontSize);
        Assert.True(svc2.Current.Keyboard.EnableFarStyleKeybindings);
        Assert.Equal(LogLevel.Debug,                 svc2.Current.Advanced.LogLevel);
    }
}
