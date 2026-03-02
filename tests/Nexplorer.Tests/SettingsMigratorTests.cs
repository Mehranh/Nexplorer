using Nexplorer.App.Services.Settings;

namespace Nexplorer.Tests;

public sealed class SettingsMigratorTests
{
    [Fact]
    public void MigrateIfNeeded_CurrentVersion_ReturnsNull()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            SettingsDefaults.CreateDefault(), JsonSettingsService.SerializerOptions);

        var result = SettingsMigrator.MigrateIfNeeded(json);
        Assert.Null(result);
    }

    [Fact]
    public void MigrateIfNeeded_V1_AddsThrottleField()
    {
        var v1Json = """
        {
            "version": 1,
            "performance": {
                "maxIconCacheSizeMb": 128
            }
        }
        """;

        var result = SettingsMigrator.MigrateIfNeeded(v1Json);
        Assert.NotNull(result);
        Assert.Contains("throttleBackgroundMetadataMs", result);
        Assert.Contains($"\"version\": {SettingsDefaults.CurrentVersion}", result);
    }

    [Fact]
    public void MigrateIfNeeded_V1_RenamesShellType()
    {
        var v1Json = """
        {
            "version": 1,
            "commandRunner": {
                "shellType": "PowerShell"
            }
        }
        """;

        var result = SettingsMigrator.MigrateIfNeeded(v1Json);
        Assert.NotNull(result);
        Assert.Contains("defaultShell", result);
        Assert.DoesNotContain("shellType", result);
    }

    [Fact]
    public void MigrateIfNeeded_MissingVersion_TreatsAsV1()
    {
        var json = """{ "general": { "showHiddenFiles": true } }""";

        var result = SettingsMigrator.MigrateIfNeeded(json);
        Assert.NotNull(result);
        Assert.Contains($"\"version\": {SettingsDefaults.CurrentVersion}", result);
    }
}
