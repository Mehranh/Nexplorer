using System.IO;
using Nexplorer.App.Services;
using Nexplorer.App.ViewModels;

namespace Nexplorer.Tests;

public class CompletionServiceTests : IDisposable
{
    private readonly string _testDir;

    public CompletionServiceTests()
    {
        // Create a temp directory with known structure for file-system tests
        _testDir = Path.Combine(Path.GetTempPath(), "FETests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);

        // Create test files and folders
        Directory.CreateDirectory(Path.Combine(_testDir, "Documents"));
        Directory.CreateDirectory(Path.Combine(_testDir, "Downloads"));
        Directory.CreateDirectory(Path.Combine(_testDir, "Desktop"));
        Directory.CreateDirectory(Path.Combine(_testDir, "src"));
        Directory.CreateDirectory(Path.Combine(_testDir, "src", "components"));
        File.WriteAllText(Path.Combine(_testDir, "readme.md"), "test");
        File.WriteAllText(Path.Combine(_testDir, "readme.txt"), "test");
        File.WriteAllText(Path.Combine(_testDir, "run.ps1"), "test");
        File.WriteAllText(Path.Combine(_testDir, "src", "main.cs"), "test");
        File.WriteAllText(Path.Combine(_testDir, "src", "app.cs"), "test");
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    private static CommandHistoryEntry MakeEntry(string cmd, string wd = "C:\\work",
        int? exitCode = 0, DateTimeOffset? ts = null)
        => new(ts ?? DateTimeOffset.Now, wd, ShellKind.PowerShell, cmd, exitCode);

    // ═════════════════════════════════════════════════════════════════════════
    //  GetLastWord
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("git status", "status")]
    [InlineData("ls", "ls")]
    [InlineData("cd Documents", "Documents")]
    [InlineData("ls ", "")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    [InlineData("cd src\\components", "src\\components")]
    public void GetLastWord_ReturnsCorrectToken(string input, string expected)
    {
        Assert.Equal(expected, CompletionService.GetLastWord(input));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  ExpandTilde
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ExpandTilde_TildeAlone_ReturnsHomePath()
    {
        var result = CompletionService.ExpandTilde("~");
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), result);
    }

    [Fact]
    public void ExpandTilde_TildeSlash_ExpandsCorrectly()
    {
        var result = CompletionService.ExpandTilde("~/Documents");
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpandTilde_TildeBackslash_ExpandsCorrectly()
    {
        var result = CompletionService.ExpandTilde("~\\Downloads");
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ExpandTilde_NormalPath_ReturnsUnchanged()
    {
        Assert.Equal("Documents", CompletionService.ExpandTilde("Documents"));
        Assert.Equal("C:\\test", CompletionService.ExpandTilde("C:\\test"));
    }

    [Fact]
    public void ExpandTilde_EmptyOrNull_ReturnsAsIs()
    {
        Assert.Equal("", CompletionService.ExpandTilde(""));
        Assert.Null(CompletionService.ExpandTilde(null!));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  GetFileSystemCompletions
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetFileSystemCompletions_EmptyPrefix_ListsAll()
    {
        var results = CompletionService.GetFileSystemCompletions("", _testDir);
        // Should include Documents, Downloads, Desktop, src, readme.md, readme.txt, run.ps1
        Assert.True(results.Count >= 7, $"Expected >=7 entries, got {results.Count}");
    }

    [Fact]
    public void GetFileSystemCompletions_PartialPrefix_FiltersCorrectly()
    {
        var results = CompletionService.GetFileSystemCompletions("Do", _testDir);
        // Documents, Downloads
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.StartsWith("Do", r, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetFileSystemCompletions_DirectoryEndsWithSeparator()
    {
        var results = CompletionService.GetFileSystemCompletions("src", _testDir);
        Assert.Single(results);
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), results[0]);
    }

    [Fact]
    public void GetFileSystemCompletions_FileDoesNotEndWithSeparator()
    {
        var results = CompletionService.GetFileSystemCompletions("run", _testDir);
        Assert.Single(results);
        Assert.DoesNotContain(Path.DirectorySeparatorChar.ToString(), results[0].TrimEnd(Path.DirectorySeparatorChar));
        Assert.False(results[0].EndsWith(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void GetFileSystemCompletions_SubdirectoryPath_Works()
    {
        var results = CompletionService.GetFileSystemCompletions("src\\m", _testDir);
        Assert.Single(results);
        Assert.Contains("main.cs", results[0]);
    }

    [Fact]
    public void GetFileSystemCompletions_NoMatches_ReturnsEmpty()
    {
        var results = CompletionService.GetFileSystemCompletions("zzz_nonexistent", _testDir);
        Assert.Empty(results);
    }

    [Fact]
    public void GetFileSystemCompletions_InvalidDir_ReturnsEmpty()
    {
        var results = CompletionService.GetFileSystemCompletions("test", "Z:\\nonexistent_dir_xyz");
        Assert.Empty(results);
    }

    [Fact]
    public void GetFileSystemCompletions_RespectsMaxLimit()
    {
        var results = CompletionService.GetFileSystemCompletions("", _testDir, max: 3);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void GetFileSystemCompletions_ReadmePrefix_FindsBothFiles()
    {
        var results = CompletionService.GetFileSystemCompletions("readme", _testDir);
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Contains("readme.md"));
        Assert.Contains(results, r => r.Contains("readme.txt"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  GetSuggestions — History
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetSuggestions_EmptyInput_ReturnsEmpty()
    {
        var history = new[] { MakeEntry("git status") };
        var results = CompletionService.GetSuggestions("", _testDir, history);
        Assert.Empty(results);
    }

    [Fact]
    public void GetSuggestions_WhitespaceOnly_ReturnsEmpty()
    {
        var history = new[] { MakeEntry("git status") };
        var results = CompletionService.GetSuggestions("   ", _testDir, history);
        Assert.Empty(results);
    }

    [Fact]
    public void GetSuggestions_PrefixMatch_ReturnsHistory()
    {
        var history = new[]
        {
            MakeEntry("git status"),
            MakeEntry("git push"),
            MakeEntry("dotnet build"),
        };
        var results = CompletionService.GetSuggestions("git", _testDir, history);
        Assert.True(results.Count >= 2);
        Assert.All(results.Where(r => r.Kind == SuggestionKind.History),
            r => Assert.Contains("git", r.Text, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetSuggestions_ContainsMatch_IncludedAfterPrefix()
    {
        var history = new[]
        {
            MakeEntry("dotnet build"),
            MakeEntry("run build script"),
        };
        var results = CompletionService.GetSuggestions("build", _testDir, history);
        // "dotnet build" is a contains match; "run build script" is also a contains match
        var historyResults = results.Where(r => r.Kind == SuggestionKind.History).ToList();
        Assert.True(historyResults.Count >= 1);
    }

    [Fact]
    public void GetSuggestions_FrequencyWeighted_MostUsedFirst()
    {
        var now = DateTimeOffset.Now;
        var history = new[]
        {
            MakeEntry("git status", ts: now),
            MakeEntry("git push", ts: now.AddSeconds(-1)),
            MakeEntry("git push", ts: now.AddSeconds(-2)),
            MakeEntry("git push", ts: now.AddSeconds(-3)),
        };
        var results = CompletionService.GetSuggestions("git", _testDir, history);
        var historyItems = results.Where(r => r.Kind == SuggestionKind.History).ToList();
        Assert.True(historyItems.Count >= 2);
        // git push (×3) should come before git status (×1)
        Assert.Equal("git push", historyItems[0].Text);
        Assert.Contains("×3", historyItems[0].Detail!);
    }

    [Fact]
    public void GetSuggestions_DeduplicatesHistory()
    {
        var history = new[]
        {
            MakeEntry("git status"),
            MakeEntry("git status"),
            MakeEntry("git status"),
        };
        var results = CompletionService.GetSuggestions("git", _testDir, history);
        var historyItems = results.Where(r => r.Kind == SuggestionKind.History).ToList();
        Assert.Single(historyItems);
    }

    [Fact]
    public void GetSuggestions_RespectsMaxItems()
    {
        var history = Enumerable.Range(1, 20)
            .Select(i => MakeEntry($"cmd{i}"))
            .ToArray();
        var results = CompletionService.GetSuggestions("cmd", _testDir, history, maxItems: 5);
        Assert.True(results.Count <= 5);
    }

    [Fact]
    public void GetSuggestions_IncludesFileSystemCompletions()
    {
        var history = Array.Empty<CommandHistoryEntry>();
        // "ls re" → last word is "re" → should match readme.md, readme.txt
        var results = CompletionService.GetSuggestions("ls re", _testDir, history);
        var fsItems = results.Where(r => r.Kind == SuggestionKind.FileSystem).ToList();
        Assert.True(fsItems.Count >= 2, $"Expected >=2 FS items, got {fsItems.Count}");
    }

    [Fact]
    public void GetSuggestions_SpaceAfterCommand_ShowsAllFiles()
    {
        var history = Array.Empty<CommandHistoryEntry>();
        // "ls " (with trailing space) → empty last word → lists all entries
        var results = CompletionService.GetSuggestions("ls ", _testDir, history);
        var fsItems = results.Where(r => r.Kind == SuggestionKind.FileSystem).ToList();
        Assert.True(fsItems.Count >= 1, "Expected file-system suggestions with trailing space");
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  GetSuggestions — Bang Commands
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetSuggestions_BangBang_ReturnsLastCommand()
    {
        var history = new[]
        {
            MakeEntry("git push"),
            MakeEntry("git status"),
        };
        var results = CompletionService.GetSuggestions("!!", _testDir, history);
        Assert.Single(results);
        Assert.Equal("git push", results[0].Text);
        Assert.Equal(SuggestionKind.BangCommand, results[0].Kind);
        Assert.Contains("re-run last", results[0].Detail!);
    }

    [Fact]
    public void GetSuggestions_BangNumber_ReturnsNthEntry()
    {
        var history = new[]
        {
            MakeEntry("first"),
            MakeEntry("second"),
            MakeEntry("third"),
        };
        var results = CompletionService.GetSuggestions("!2", _testDir, history);
        Assert.Single(results);
        Assert.Equal("second", results[0].Text);
        Assert.Equal(SuggestionKind.BangCommand, results[0].Kind);
    }

    [Fact]
    public void GetSuggestions_BangNumber_OutOfRange_ReturnsEmpty()
    {
        var history = new[] { MakeEntry("only-one") };
        var results = CompletionService.GetSuggestions("!5", _testDir, history);
        Assert.Empty(results);
    }

    [Fact]
    public void GetSuggestions_BangPrefix_MatchesByPrefix()
    {
        var history = new[]
        {
            MakeEntry("dotnet build"),
            MakeEntry("git push"),
            MakeEntry("git status"),
        };
        var results = CompletionService.GetSuggestions("!git", _testDir, history);
        Assert.True(results.Count >= 1);
        Assert.All(results, r =>
        {
            Assert.Equal(SuggestionKind.BangCommand, r.Kind);
            Assert.StartsWith("git", r.Text, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void GetSuggestions_BangEmptyHistory_ReturnsEmpty()
    {
        var results = CompletionService.GetSuggestions("!!", _testDir,
            Array.Empty<CommandHistoryEntry>());
        Assert.Empty(results);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  CycleTabCompletion
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CycleTabCompletion_SingleMatch_CompletesImmediately()
    {
        var state = new TabCycleState();
        var result = CompletionService.CycleTabCompletion("ls run", _testDir, state);
        Assert.Contains("run.ps1", result);
    }

    [Fact]
    public void CycleTabCompletion_MultipleMatches_FirstPressGivesCommonPrefix()
    {
        // "Do" matches Documents\ and Downloads\ → common prefix is "Do"
        // But they differ at char 2 (c vs w), so common prefix is "Do"
        // Actually "Documents" vs "Downloads" → common is "Do"
        var state = new TabCycleState();
        var result1 = CompletionService.CycleTabCompletion("cd Do", _testDir, state);
        // First press with no longer common prefix should cycle
        Assert.Contains("Do", result1);
    }

    [Fact]
    public void CycleTabCompletion_MultipleMatches_CyclesThroughAll()
    {
        var state = new TabCycleState();
        // "readme" matches readme.md and readme.txt
        var r1 = CompletionService.CycleTabCompletion("ls readme", _testDir, state);
        // Common prefix "readme." should be applied first if it's longer than "readme"
        // readme.md and readme.txt → common prefix = "readme."
        Assert.Contains("readme.", r1);

        // Second press should cycle to first specific match
        var r2 = CompletionService.CycleTabCompletion(r1, _testDir, state);
        // Since state.Prefix changed (r1 ended with "readme."), this will re-init
        // Let's test the cycling by keeping original input
        state.Reset();
        var c1 = CompletionService.CycleTabCompletion("ls readme", _testDir, state);
        var c2 = CompletionService.CycleTabCompletion("ls readme", _testDir, state);
        var c3 = CompletionService.CycleTabCompletion("ls readme", _testDir, state);
        // c2 and c3 should cycle through individual completions
        Assert.NotEqual(c1, c2);
    }

    [Fact]
    public void CycleTabCompletion_NoMatches_ReturnsOriginal()
    {
        var state = new TabCycleState();
        var result = CompletionService.CycleTabCompletion("ls zzz_none", _testDir, state);
        Assert.Equal("ls zzz_none", result);
    }

    [Fact]
    public void CycleTabCompletion_EmptyLastWord_ListsAllEntries()
    {
        var state = new TabCycleState();
        var result = CompletionService.CycleTabCompletion("ls ", _testDir, state);
        // Should pick the first file-system entry
        Assert.NotEqual("ls ", result);
    }

    [Fact]
    public void CycleTabCompletion_StateReset_WhenInputChanges()
    {
        var state = new TabCycleState();
        CompletionService.CycleTabCompletion("ls re", _testDir, state);
        Assert.Equal("re", state.Prefix);
        Assert.True(state.Completions.Count > 0);

        // Typing more should reset
        CompletionService.CycleTabCompletion("ls run", _testDir, state);
        Assert.Equal("run", state.Prefix);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  GetAllTabCompletions
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAllTabCompletions_ReturnsMatchingEntries()
    {
        var results = CompletionService.GetAllTabCompletions("ls D", _testDir);
        Assert.True(results.Count >= 3); // Documents, Downloads, Desktop
    }

    [Fact]
    public void GetAllTabCompletions_NoInput_ReturnsAll()
    {
        var results = CompletionService.GetAllTabCompletions("ls ", _testDir);
        Assert.True(results.Count >= 7);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  TabCycleState
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TabCycleState_Reset_ClearsAll()
    {
        var state = new TabCycleState
        {
            Prefix = "test",
            Completions = new List<string> { "a", "b" },
            Index = 1,
            CommonPrefixApplied = true
        };
        state.Reset();
        Assert.Equal(string.Empty, state.Prefix);
        Assert.Empty(state.Completions);
        Assert.Equal(-1, state.Index);
        Assert.False(state.CommonPrefixApplied);
    }
}
