using Nexplorer.App.ViewModels;

namespace Nexplorer.App.Services;

// ─── Data model for CLI tool definitions ──────────────────────────────────────

public sealed record CliFlag(
    string  Name,
    string? ShortName   = null,
    string? Description = null,
    bool    ExpectsValue = false);

public sealed record CliCommand(
    string                      Name,
    string?                     Description = null,
    IReadOnlyList<CliCommand>?  Subcommands = null,
    IReadOnlyList<CliFlag>?     Flags       = null);

public sealed record CliToolDefinition(
    string                      Name,
    string?                     Description = null,
    IReadOnlyList<CliCommand>?  Subcommands = null,
    IReadOnlyList<CliFlag>?     GlobalFlags = null);

// ─── CLI suggestion engine ────────────────────────────────────────────────────

public static class CliSuggestionService
{
    private static readonly Dictionary<string, CliToolDefinition> _tools =
        new(StringComparer.OrdinalIgnoreCase);

    static CliSuggestionService()
    {
        foreach (var def in CliToolDefinitions.All)
            _tools[def.Name] = def;
    }

    /// <summary>
    /// Register a custom tool definition at runtime (e.g. from plugins).
    /// </summary>
    public static void Register(CliToolDefinition tool)
        => _tools[tool.Name] = tool;

    /// <summary>
    /// Returns CLI-aware suggestions for the given input, or empty if the input
    /// doesn't match a known CLI tool.
    /// </summary>
    public static IReadOnlyList<SuggestionItem> GetSuggestions(
        string input, int maxItems = 12)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Array.Empty<SuggestionItem>();

        var parts = TokenizeInput(input);
        if (parts.Count == 0)
            return Array.Empty<SuggestionItem>();

        var toolName = parts[0];

        // If user is still typing the tool name (single token, no trailing space)
        if (parts.Count == 1 && !input.EndsWith(' '))
        {
            return MatchToolNames(toolName, maxItems);
        }

        if (!_tools.TryGetValue(toolName, out var tool))
            return Array.Empty<SuggestionItem>();

        // Walk the command tree to find the deepest matching subcommand
        var (node, depth) = ResolveCommandContext(tool, parts);

        // The current token being typed (empty if input ends with space)
        var currentToken = input.EndsWith(' ') ? "" : parts[^1];
        var isTyping = !input.EndsWith(' ');

        // Tokens after the tool name, minus any we consumed walking the tree
        var argIndex = depth + 1; // +1 for tool name
        var remainingParts = parts.Skip(argIndex).ToList();

        // Collect already-used flags to avoid re-suggesting them
        // Exclude the current token being typed (it's incomplete)
        var usedFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipLast = isTyping ? parts.Count - 1 : parts.Count;
        for (int i = 0; i < skipLast; i++)
        {
            if (parts[i].StartsWith('-'))
                usedFlags.Add(parts[i]);
        }

        // Compute the prefix (everything before the current token)
        string prefix;
        if (isTyping)
        {
            // Strip the partial token being typed, keep everything before it
            var lastSpaceIdx = input.LastIndexOf(' ', input.Length - 1);
            prefix = lastSpaceIdx >= 0 ? input[..(lastSpaceIdx + 1)] : "";
        }
        else
        {
            // Input ends with space — everything is the prefix
            prefix = input;
        }

        var results = new List<SuggestionItem>(maxItems);
        var seen    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Determine available subcommands: from matched node, or from tool root
        var availableSubcommands = node is null ? tool.Subcommands : node.Subcommands;

        // If current token starts with '-', suggest flags
        if (currentToken.StartsWith('-'))
        {
            AddFlagSuggestions(node, tool, prefix, currentToken, usedFlags, results, seen, maxItems);
        }
        else
        {
            // Suggest subcommands first, then flags
            AddSubcommandSuggestions(availableSubcommands, prefix, currentToken, results, seen, maxItems);

            if (results.Count < maxItems)
                AddFlagSuggestions(node, tool, prefix, currentToken, usedFlags, results, seen, maxItems);
        }

        return results;
    }

    // ─── Internal helpers ─────────────────────────────────────────────────────

    private static List<SuggestionItem> MatchToolNames(
        string prefix, int maxItems)
    {
        var results = new List<SuggestionItem>();
        foreach (var kvp in _tools)
        {
            if (results.Count >= maxItems) break;
            if (kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new SuggestionItem(
                    kvp.Key, SuggestionKind.CliTool,
                    kvp.Value.Description));
            }
        }
        return results;
    }

    /// <summary>
    /// Walks the command tree and returns the deepest matching CliCommand (or null
    /// if we're still at tool root) and how many tokens were consumed.
    /// </summary>
    private static (CliCommand? Node, int Depth) ResolveCommandContext(
        CliToolDefinition tool, List<string> parts)
    {
        var subcommands = tool.Subcommands;
        CliCommand? current = null;
        int depth = 0;

        for (int i = 1; i < parts.Count; i++)
        {
            var token = parts[i];
            if (token.StartsWith('-')) continue; // skip flags

            var match = subcommands?.FirstOrDefault(
                c => c.Name.Equals(token, StringComparison.OrdinalIgnoreCase));

            if (match is null) continue; // skip arguments / flag values

            current = match;
            depth = i;
            subcommands = match.Subcommands;
        }

        return (current, depth);
    }

    private static void AddSubcommandSuggestions(
        IReadOnlyList<CliCommand>? subcommands, string inputPrefix, string currentToken,
        List<SuggestionItem> results, HashSet<string> seen, int maxItems)
    {
        if (subcommands is null) return;

        foreach (var cmd in subcommands)
        {
            if (results.Count >= maxItems) break;
            if (!string.IsNullOrEmpty(currentToken) &&
                !cmd.Name.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!seen.Add(cmd.Name)) continue;

            results.Add(new SuggestionItem(
                inputPrefix + cmd.Name, SuggestionKind.CliTool,
                cmd.Description));
        }
    }

    private static void AddFlagSuggestions(
        CliCommand? node, CliToolDefinition tool, string inputPrefix, string currentToken,
        HashSet<string> usedFlags,
        List<SuggestionItem> results, HashSet<string> seen, int maxItems)
    {
        // Merge node-specific flags + global flags
        var allFlags = new List<CliFlag>();
        if (node?.Flags is not null) allFlags.AddRange(node.Flags);
        if (tool.GlobalFlags is not null) allFlags.AddRange(tool.GlobalFlags);

        foreach (var flag in allFlags)
        {
            if (results.Count >= maxItems) break;
            if (usedFlags.Contains(flag.Name)) continue;
            if (flag.ShortName is not null && usedFlags.Contains(flag.ShortName)) continue;

            if (!string.IsNullOrEmpty(currentToken))
            {
                if (!flag.Name.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase) &&
                    (flag.ShortName is null ||
                     !flag.ShortName.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase)))
                    continue;
            }

            var detail = flag.ShortName is not null
                ? $"{flag.Description ?? flag.Name} ({flag.ShortName})"
                : flag.Description ?? flag.Name;

            if (!seen.Add(flag.Name)) continue;
            results.Add(new SuggestionItem(
                inputPrefix + flag.Name, SuggestionKind.CliTool,
                detail));
        }
    }

    /// <summary>
    /// Simple shell-style tokenizer: splits on whitespace, respects double/single quotes.
    /// </summary>
    private static List<string> TokenizeInput(string input)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        bool inSingle = false, inDouble = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '"' && !inSingle) { inDouble = !inDouble; continue; }
            if (c == '\'' && !inDouble) { inSingle = !inSingle; continue; }

            if (char.IsWhiteSpace(c) && !inSingle && !inDouble)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }
            current.Append(c);
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }
}
