using FileExplorer.App.Services;
using FileExplorer.App.Services.Settings;

namespace FileExplorer.Tests;

public sealed class SettingsModelsTests
{
    [Fact]
    public void CreateDefault_ReturnsCurrentVersion()
    {
        var settings = SettingsDefaults.CreateDefault();
        Assert.Equal(SettingsDefaults.CurrentVersion, settings.Version);
    }

    [Fact]
    public void CreateDefault_HasExpectedDefaults()
    {
        var s = SettingsDefaults.CreateDefault();

        Assert.Equal(StartupBehavior.RestoreSession, s.General.StartupBehavior);
        Assert.Equal(DefaultViewMode.Details, s.General.DefaultViewMode);
        Assert.Equal(ClickMode.DoubleClick, s.General.ClickMode);
        Assert.True(s.General.ConfirmBeforeDelete);
        Assert.False(s.General.ShowHiddenFiles);
        Assert.False(s.General.ShowSystemFiles);
        Assert.Equal(DefaultSortMode.FolderFirst, s.General.DefaultSortMode);

        Assert.True(s.Performance.EnableAggressiveCaching);
        Assert.Equal(128, s.Performance.MaxIconCacheSizeMb);
        Assert.Equal(4, s.Performance.MaxConcurrentFileOperations);
        Assert.Equal(500, s.Performance.EnumerationBatchSize);

        Assert.Equal(ConflictResolution.Ask, s.FileOperations.DefaultConflictResolution);
        Assert.True(s.FileOperations.EnableCopyQueue);
        Assert.True(s.FileOperations.MoveToRecycleBin);

        Assert.Equal(DefaultShell.PowerShell, s.CommandRunner.DefaultShell);
        Assert.Equal(500, s.CommandRunner.MaxHistoryEntries);

        Assert.Equal(AppTheme.Dark, s.Appearance.Theme);
        Assert.Equal("#0078D4", s.Appearance.AccentColor);
        Assert.Equal(14, s.Appearance.CommandLineFontSize);

        Assert.False(s.Keyboard.EnableFarStyleKeybindings);

        Assert.False(s.Advanced.EnableDiagnosticsLogging);
        Assert.Equal(LogLevel.Warning, s.Advanced.LogLevel);
        Assert.True(s.Advanced.EnableCrashReporting);
    }

    [Fact]
    public void Record_WithExpression_DoesNotMutateOriginal()
    {
        var original = SettingsDefaults.CreateDefault();
        var modified = original with
        {
            General = original.General with { ShowHiddenFiles = true }
        };

        Assert.False(original.General.ShowHiddenFiles);
        Assert.True(modified.General.ShowHiddenFiles);
    }
}
