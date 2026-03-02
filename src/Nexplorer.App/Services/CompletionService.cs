using System.IO;
using Nexplorer.App.ViewModels;

namespace Nexplorer.App.Services;

/// <summary>
/// Provides auto-complete suggestions for the terminal command bar.
/// Sources: command history (prefix + contains + frequency), file-system entries,
/// bang-command shortcuts (!! / !n / !prefix).
/// </summary>
public static class CompletionService
{
    private static readonly string HomePath =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // ─── Unified suggestion list ──────────────────────────────────────────────

    public static IReadOnlyList<SuggestionItem> GetSuggestions(
        string                          input,
        string                          workingDir,
        IEnumerable<CommandHistoryEntry> history,
        int                             maxItems = 12)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Array.Empty<SuggestionItem>();

        var results  = new List<SuggestionItem>(maxItems);
        var seen     = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 0. Bang-command shortcuts (!! / !n / !prefix)
        if (input.StartsWith('!'))
        {
            AddBangSuggestions(input, history, results, seen, maxItems);
            if (results.Count > 0) return results;
        }

        // 1. History — frequency-weighted prefix matches first (highest relevance)
        var historyList = history as IList<CommandHistoryEntry> ?? history.ToList();
        var freqMap = BuildFrequencyMap(historyList);

        var prefixMatches = historyList
            .Where(h => h.Command.StartsWith(input, StringComparison.OrdinalIgnoreCase))
            .GroupBy(h => h.Command, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => freqMap.GetValueOrDefault(g.Key, 0))
            .ThenByDescending(g => g.First().Timestamp);

        foreach (var g in prefixMatches)
        {
            if (results.Count >= maxItems) break;
            if (!seen.Add(g.Key)) continue;
            var recent = g.First();
            var freq = freqMap.GetValueOrDefault(g.Key, 1);
            var detail = freq > 1 ? $"{recent.WorkingDirectory}  (×{freq})" : recent.WorkingDirectory;
            results.Add(new SuggestionItem(g.Key, SuggestionKind.History, detail));
        }

        // 2. History — contains matches (lower relevance), also frequency-weighted
        var containsMatches = historyList
            .Where(h => h.Command.Contains(input, StringComparison.OrdinalIgnoreCase))
            .GroupBy(h => h.Command, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => freqMap.GetValueOrDefault(g.Key, 0))
            .ThenByDescending(g => g.First().Timestamp);

        foreach (var g in containsMatches)
        {
            if (results.Count >= maxItems) break;
            if (!seen.Add(g.Key)) continue;
            var recent = g.First();
            var freq = freqMap.GetValueOrDefault(g.Key, 1);
            var detail = freq > 1 ? $"{recent.WorkingDirectory}  (×{freq})" : recent.WorkingDirectory;
            results.Add(new SuggestionItem(g.Key, SuggestionKind.History, detail));
        }

        // 3. File-system completions on the last token (with ~ expansion)
        var lastWord = GetLastWord(input);
        var hasCommand = input.TrimEnd().Length > 0;
        if (!string.IsNullOrEmpty(lastWord) || (hasCommand && input.Length > input.TrimEnd().Length))
        {
            var expandedWord = ExpandTilde(lastWord);
            foreach (var fsPath in GetFileSystemCompletions(expandedWord, workingDir))
            {
                if (results.Count >= maxItems) break;
                var full = ReplaceLastWord(input, lastWord, fsPath);
                if (!seen.Add(full)) continue;
                var isDir = fsPath.EndsWith(Path.DirectorySeparatorChar)
                         || fsPath.EndsWith(Path.AltDirectorySeparatorChar);
                var detail = isDir ? "directory" : "file";
                results.Add(new SuggestionItem(full, SuggestionKind.FileSystem, detail));
            }
        }

        return results;
    }

    // ─── Bang-command suggestions ─────────────────────────────────────────────

    private static void AddBangSuggestions(
        string input,
        IEnumerable<CommandHistoryEntry> history,
        List<SuggestionItem> results,
        HashSet<string> seen,
        int maxItems)
    {
        var historyList = history as IList<CommandHistoryEntry> ?? history.ToList();

        if (input == "!!")
        {
            // !! = re-run last command
            if (historyList.Count > 0)
            {
                var last = historyList[0];
                results.Add(new SuggestionItem(
                    last.Command, SuggestionKind.BangCommand,
                    $"!! → re-run last command"));
            }
            return;
        }

        // !n = run nth history entry (1-based)
        if (input.Length > 1 && int.TryParse(input[1..], out int idx))
        {
            if (idx >= 1 && idx <= historyList.Count)
            {
                var entry = historyList[idx - 1];
                results.Add(new SuggestionItem(
                    entry.Command, SuggestionKind.BangCommand,
                    $"!{idx} → {entry.Command}"));
            }
            return;
        }

        // !prefix = last command starting with prefix
        if (input.Length > 1)
        {
            var prefix = input[1..];
            foreach (var h in historyList)
            {
                if (results.Count >= maxItems) break;
                if (!h.Command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (!seen.Add(h.Command)) continue;
                results.Add(new SuggestionItem(
                    h.Command, SuggestionKind.BangCommand,
                    $"!{prefix} → re-run"));
            }
        }
    }

    // ─── Tilde expansion ──────────────────────────────────────────────────────

    public static string ExpandTilde(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path == "~") return HomePath;
        if (path.StartsWith("~/") || path.StartsWith("~\\"))
            return Path.Combine(HomePath, path[2..]);
        return path;
    }

    // ─── Frequency map for history ────────────────────────────────────────────

    private static Dictionary<string, int> BuildFrequencyMap(
        IEnumerable<CommandHistoryEntry> history)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in history)
        {
            map.TryGetValue(h.Command, out int count);
            map[h.Command] = count + 1;
        }
        return map;
    }

    // ─── File-system completions ──────────────────────────────────────────────

    /// <summary>
    /// Returns absolute paths (dirs end with \) that start with <paramref name="prefix"/>.
    /// </summary>
    public static IReadOnlyList<string> GetFileSystemCompletions(
        string prefix, string workingDir, int max = 20)
    {
        try
        {
            string searchDir, filePrefix;
            bool isAbsolute = false;

            if (string.IsNullOrEmpty(prefix))
            {
                // Empty prefix: list all entries in working directory
                searchDir  = workingDir;
                filePrefix = string.Empty;
            }
            else if (Path.IsPathRooted(prefix))
            {
                searchDir  = Path.GetDirectoryName(prefix) ?? workingDir;
                filePrefix = Path.GetFileName(prefix);
                isAbsolute = true;
            }
            else if (prefix.Contains('\\') || prefix.Contains('/'))
            {
                var fullPath = Path.Combine(workingDir, prefix);
                searchDir  = Path.GetDirectoryName(fullPath) ?? workingDir;
                filePrefix = Path.GetFileName(prefix);
            }
            else
            {
                searchDir  = workingDir;
                filePrefix = prefix;
            }

            if (!Directory.Exists(searchDir))
                return Array.Empty<string>();

            return Directory
                .EnumerateFileSystemEntries(searchDir,
                    string.IsNullOrEmpty(filePrefix) ? "*" : filePrefix + "*")
                .Take(max)
                .Select(p =>
                {
                    // Return absolute paths only when user typed an absolute prefix;
                    // otherwise return paths relative to the working directory.
                    var name = isAbsolute ? p : Path.GetRelativePath(workingDir, p);
                    return Directory.Exists(p) ? name + Path.DirectorySeparatorChar : name;
                })
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    // ─── Bash-style tab-completion ────────────────────────────────────────────

    /// <summary>
    /// Given an input string and cycle state, returns the next tab-cycled value.
    /// First press: completes to the longest common prefix among all matches.
    /// Subsequent presses: cycles through individual matches.
    /// Supports ~ expansion.
    /// </summary>
    public static string CycleTabCompletion(
        string          input,
        string          workingDir,
        TabCycleState   state)
    {
        var lastWord = GetLastWord(input);
        var expandedWord = ExpandTilde(lastWord);

        // Reset if the input changed (user typed)
        if (state.Prefix != lastWord || state.Completions.Count == 0)
        {
            state.Prefix      = lastWord;
            state.Completions = GetFileSystemCompletions(expandedWord, workingDir).ToList();
            state.Index       = -1;
            state.CommonPrefixApplied = false;
        }

        if (state.Completions.Count == 0) return input;

        // First Tab press: apply longest common prefix
        if (!state.CommonPrefixApplied && state.Completions.Count > 1)
        {
            var lcp = LongestCommonPrefix(state.Completions);
            if (lcp.Length > expandedWord.Length ||
                (lcp.Length > lastWord.Length && expandedWord != lastWord))
            {
                state.CommonPrefixApplied = true;
                return ReplaceLastWord(input, lastWord, lcp);
            }
        }

        state.CommonPrefixApplied = true;
        state.Index = (state.Index + 1) % state.Completions.Count;
        return ReplaceLastWord(input, lastWord, state.Completions[state.Index]);
    }

    /// <summary>
    /// Returns all file-system completions for the current input token (for display on double-Tab).
    /// </summary>
    public static IReadOnlyList<string> GetAllTabCompletions(
        string input, string workingDir)
    {
        var lastWord = GetLastWord(input);
        var expanded = ExpandTilde(lastWord);
        return GetFileSystemCompletions(expanded, workingDir, max: 50);
    }

    // ─── Longest common prefix ────────────────────────────────────────────────

    private static string LongestCommonPrefix(IReadOnlyList<string> strings)
    {
        if (strings.Count == 0) return string.Empty;
        if (strings.Count == 1) return strings[0];

        var first = strings[0];
        int len = first.Length;

        for (int i = 1; i < strings.Count; i++)
        {
            len = Math.Min(len, strings[i].Length);
            for (int j = 0; j < len; j++)
            {
                if (char.ToLowerInvariant(first[j]) != char.ToLowerInvariant(strings[i][j]))
                {
                    len = j;
                    break;
                }
            }
        }

        return first[..len];
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    public static string GetLastWord(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        // If input ends with whitespace, user is starting a new argument
        if (char.IsWhiteSpace(input[^1]))
            return string.Empty;
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : string.Empty;
    }

    private static string ReplaceLastWord(string input, string lastWord, string replacement)
    {
        if (string.IsNullOrEmpty(lastWord)) return input + replacement;
        var idx = input.LastIndexOf(lastWord, StringComparison.Ordinal);
        return idx < 0 ? input : input[..idx] + replacement;
    }
}

// ─── Tab-cycle state (mutable, owned by ViewModel) ──────────────────────────

public sealed class TabCycleState
{
    public string       Prefix             { get; set; } = string.Empty;
    public List<string> Completions        { get; set; } = new();
    public int          Index              { get; set; } = -1;
    public bool         CommonPrefixApplied { get; set; }

    public void Reset()
    {
        Prefix             = string.Empty;
        Completions        = new();
        Index              = -1;
        CommonPrefixApplied = false;
    }
}
