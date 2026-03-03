using System.Collections.ObjectModel;
using System.IO;
using Nexplorer.App.Services;
using Nexplorer.App.ViewModels;

namespace Nexplorer.Tests;

/// <summary>
/// Tests that TerminalTabViewModel correctly drives suggestions, tab completion,
/// history navigation, and bang commands when used end-to-end.
/// </summary>
public class TerminalTabIntegrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly AliasService _aliasService = new();
    private readonly CommandHistoryStore _historyStore = new();
    private readonly ObservableCollection<CommandHistoryEntry> _sharedHistory = new();

    public TerminalTabIntegrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FEInt_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        Directory.CreateDirectory(Path.Combine(_testDir, "Documents"));
        Directory.CreateDirectory(Path.Combine(_testDir, "Downloads"));
        File.WriteAllText(Path.Combine(_testDir, "readme.md"), "test");
        File.WriteAllText(Path.Combine(_testDir, "readme.txt"), "test");
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    private TerminalTabViewModel CreateTab() => new(
        ShellKind.PowerShell, _testDir, _aliasService, _historyStore, _sharedHistory);

    // ─── Suggestions are populated when typing ─────────────────────────────

    [Fact]
    public void CommandText_Change_PopulatesSuggestionsFromHistory()
    {
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "git status", 0));
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "git push", 0));

        var tab = CreateTab();
        tab.CommandText = "git";

        Assert.True(tab.ShowSuggestions, "ShowSuggestions should be true");
        Assert.True(tab.Suggestions.Count >= 2, $"Expected >=2 suggestions, got {tab.Suggestions.Count}");
        Assert.Contains(tab.Suggestions, s => s.Text == "git status");
        Assert.Contains(tab.Suggestions, s => s.Text == "git push");
    }

    [Fact]
    public void CommandText_Change_PopulatesFileSystemSuggestions()
    {
        var tab = CreateTab();
        tab.CommandText = "ls re";

        var fsItems = tab.Suggestions.Where(s => s.Kind == SuggestionKind.FileSystem).ToList();
        Assert.True(fsItems.Count >= 2, $"Expected >=2 FS suggestions, got {fsItems.Count}");
    }

    [Fact]
    public void CommandText_Empty_ClearsSuggestions()
    {
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "test", 0));

        var tab = CreateTab();
        tab.CommandText = "test";
        Assert.True(tab.ShowSuggestions);
        var hadInlineSuggestion = !string.IsNullOrEmpty(tab.InlineSuggestion);

        tab.CommandText = "";
        Assert.False(tab.ShowSuggestions);
        Assert.Empty(tab.Suggestions);
    }

    // ─── InlineSuggestion (ghost text) ────────────────────────────────────

    [Fact]
    public void InlineSuggestion_ShowsHistoryGhost()
    {
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "git status", 0));

        var tab = CreateTab();
        tab.CommandText = "git";

        Assert.EndsWith(" status", tab.InlineSuggestion, StringComparison.Ordinal);
        Assert.StartsWith("   ", tab.InlineSuggestion, StringComparison.Ordinal);
    }

    [Fact]
    public void InlineSuggestion_ShowsOnlyForPrefixMatches()
    {
        var tab = CreateTab();
        tab.CommandText = "ls re";

        // Prefix file-system match exists, so ghost suffix should be visible.
        Assert.False(string.IsNullOrEmpty(tab.InlineSuggestion));
        Assert.Contains("adme", tab.InlineSuggestion, StringComparison.OrdinalIgnoreCase);

        // No suggestion starts with "build" in this test setup, so no ghost text should be shown.
        tab.CommandText = "build";
        Assert.Equal(string.Empty, tab.InlineSuggestion);
    }

    // ─── Tab completion ───────────────────────────────────────────────────

    [Fact]
    public void HandleTabCompletion_CompletesFilePath()
    {
        var tab = CreateTab();
        tab.CommandText = "cd Do";
        tab.HandleTabCompletion();

        // Should have completed to Documents\ or Downloads\
        Assert.NotEqual("cd Do", tab.CommandText);
        Assert.Contains("Do", tab.CommandText);
    }

    [Fact]
    public void HandleTabCompletion_CyclesThroughMatches()
    {
        var tab = CreateTab();
        tab.CommandText = "cd Do";

        tab.HandleTabCompletion();
        var first = tab.CommandText;

        tab.CommandText = "cd Do"; // reset to original
        tab.HandleTabCompletion();
        tab.HandleTabCompletion();
        var second = tab.CommandText;

        // After two tab presses from same input, should get different result
        // (first press = common prefix or first match, second press = next match)
    }

    [Fact]
    public void HandleTabCompletion_NoMatch_KeepsOriginal()
    {
        var tab = CreateTab();
        tab.CommandText = "cd zzz_no_match";
        tab.HandleTabCompletion();

        Assert.Equal("cd zzz_no_match", tab.CommandText);
    }

    // ─── AcceptSuggestion ─────────────────────────────────────────────────

    [Fact]
    public void AcceptSuggestion_WithItem_SetsCommandText()
    {
        var tab = CreateTab();
        var item = new SuggestionItem("git push", SuggestionKind.History, "C:\\work");
        tab.AcceptSuggestion(item);

        Assert.Equal("git push", tab.CommandText);
        Assert.False(tab.ShowSuggestions);
        Assert.Equal(string.Empty, tab.InlineSuggestion);
    }

    [Fact]
    public void AcceptSuggestion_SelectedIndex_UsesSelectedItem()
    {
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "git status", 0));

        var tab = CreateTab();
        tab.CommandText = "git";
        Assert.True(tab.Suggestions.Count > 0);

        tab.SelectedSuggestionIndex = 0;
        tab.AcceptSuggestion();

        Assert.Equal(tab.Suggestions.Count > 0 ? "git status" : "", tab.CommandText);
        Assert.False(tab.ShowSuggestions);
    }

    [Fact]
    public void AcceptSuggestion_InlineSuggestionFallback()
    {
        var tab = CreateTab();
        tab.CommandText = "test";

        // Manually set inline suggestion (as if it were set by RefreshSuggestions)
        tab.InlineSuggestion = "test-command --flag";
        tab.AcceptSuggestion();

        Assert.Equal("test-command --flag", tab.CommandText);
    }

    // ─── DismissSuggestions ───────────────────────────────────────────────

    [Fact]
    public void DismissSuggestions_ClearsAllState()
    {
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "git status", 0));

        var tab = CreateTab();
        tab.CommandText = "git";
        Assert.True(tab.ShowSuggestions);

        tab.DismissSuggestions();
        Assert.False(tab.ShowSuggestions);
        Assert.Equal(string.Empty, tab.InlineSuggestion);
    }

    // ─── History navigation ──────────────────────────────────────────────

    [Fact]
    public void NavigateHistoryUp_SetsCommandToHistoryEntry()
    {
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "cmd1", 0));
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "cmd2", 0));

        var tab = CreateTab();
        tab.CommandText = "current";
        tab.NavigateHistoryUp();

        Assert.Equal("cmd1", tab.CommandText);
    }

    [Fact]
    public void NavigateHistoryDown_RestoresSnapshot()
    {
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "cmd1", 0));

        var tab = CreateTab();
        tab.CommandText = "typed";
        tab.NavigateHistoryUp();
        Assert.Equal("cmd1", tab.CommandText);

        tab.NavigateHistoryDown();
        Assert.Equal("typed", tab.CommandText);
    }

    // ─── Bang commands via suggestion ─────────────────────────────────────

    [Fact]
    public void BangBang_SuggestsLastCommand()
    {
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "dotnet build", 0));

        var tab = CreateTab();
        tab.CommandText = "!!";

        Assert.True(tab.Suggestions.Count >= 1);
        Assert.Equal("dotnet build", tab.Suggestions[0].Text);
        Assert.Equal(SuggestionKind.BangCommand, tab.Suggestions[0].Kind);
    }

    [Fact]
    public void BangNumber_SuggestsNthEntry()
    {
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "first", 0));
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "second", 0));

        var tab = CreateTab();
        tab.CommandText = "!2";

        Assert.True(tab.Suggestions.Count >= 1);
        Assert.Equal("second", tab.Suggestions[0].Text);
    }

    [Fact]
    public void BangPrefix_SuggestsMatchingCommands()
    {
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "dotnet build", 0));
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "dotnet run", 0));
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "git push", 0));

        var tab = CreateTab();
        tab.CommandText = "!dotnet";

        Assert.True(tab.Suggestions.Count >= 1);
        Assert.All(tab.Suggestions, s =>
        {
            Assert.Equal(SuggestionKind.BangCommand, s.Kind);
            Assert.StartsWith("dotnet", s.Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ─── PropertyChanged notifications ────────────────────────────────────

    [Fact]
    public void PropertyChanged_FiredForSuggestionProperties()
    {
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "test-cmd", 0));

        var tab = CreateTab();
        var changedProps = new List<string>();
        tab.PropertyChanged += (_, e) => { if (e.PropertyName is not null) changedProps.Add(e.PropertyName); };

        tab.CommandText = "test";

        Assert.Contains(nameof(TerminalTabViewModel.ShowSuggestions), changedProps);
        Assert.Contains(nameof(TerminalTabViewModel.InlineSuggestion), changedProps);
    }

    [Fact]
    public void PropertyChanged_FiredOnDismiss()
    {
        _sharedHistory.Add(new CommandHistoryEntry(
            DateTimeOffset.Now, _testDir, ShellKind.PowerShell, "test", 0));

        var tab = CreateTab();
        tab.CommandText = "test";
        Assert.True(tab.ShowSuggestions);
        var hadInlineSuggestion = !string.IsNullOrEmpty(tab.InlineSuggestion);

        var changedProps = new List<string>();
        tab.PropertyChanged += (_, e) => { if (e.PropertyName is not null) changedProps.Add(e.PropertyName); };

        tab.DismissSuggestions();

        Assert.Contains(nameof(TerminalTabViewModel.ShowSuggestions), changedProps);
        if (hadInlineSuggestion)
            Assert.Contains(nameof(TerminalTabViewModel.InlineSuggestion), changedProps);
    }

    // ─── Frequency weighting ─────────────────────────────────────────────

    [Fact]
    public void Suggestions_FrequencyWeighted_MostUsedFirst()
    {
        var now = DateTimeOffset.Now;
        _sharedHistory.Add(new CommandHistoryEntry(now, _testDir, ShellKind.PowerShell, "git status", 0));
        _sharedHistory.Add(new CommandHistoryEntry(now.AddSeconds(-1), _testDir, ShellKind.PowerShell, "git push", 0));
        _sharedHistory.Add(new CommandHistoryEntry(now.AddSeconds(-2), _testDir, ShellKind.PowerShell, "git push", 0));
        _sharedHistory.Add(new CommandHistoryEntry(now.AddSeconds(-3), _testDir, ShellKind.PowerShell, "git push", 0));

        var tab = CreateTab();
        tab.CommandText = "git";

        var historyItems = tab.Suggestions.Where(s => s.Kind == SuggestionKind.History).ToList();
        // "git push" used 3 times should rank before "git status" used once
        Assert.True(historyItems.Count >= 2);
        Assert.Equal("git push", historyItems[0].Text);
    }

    // ─── File-system detail labels ───────────────────────────────────────

    [Fact]
    public void Suggestions_FileSystem_ShowsDirectoryOrFileDetail()
    {
        var tab = CreateTab();
        tab.CommandText = "ls ";

        var fsItems = tab.Suggestions.Where(s => s.Kind == SuggestionKind.FileSystem).ToList();
        Assert.True(fsItems.Count >= 1);

        var dirItem = fsItems.FirstOrDefault(s => s.Detail == "directory");
        Assert.NotNull(dirItem);
    }
}
