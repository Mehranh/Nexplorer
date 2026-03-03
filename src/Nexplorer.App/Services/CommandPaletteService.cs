using System.Text.RegularExpressions;

namespace Nexplorer.App.Services;

/// <summary>
/// Defines a single command that can appear in the Command Palette.
/// </summary>
public sealed record PaletteCommand(
    string Id,
    string Name,
    string Category,
    string? Shortcut,
    string IconKind,
    Action Execute
);

/// <summary>
/// Fuzzy-matching engine for the Command Palette.  Uses a contiguous-subsequence
/// algorithm that rewards consecutive matches, word-boundary hits, and prefix matches.
/// </summary>
public static class FuzzyMatcher
{
    public static (bool IsMatch, int Score, List<int> MatchedIndices) Match(string pattern, string text)
    {
        if (string.IsNullOrEmpty(pattern))
            return (true, 0, []);

        var indices = new List<int>();
        int score = 0;
        int patternIdx = 0;
        int lastMatchIdx = -1;
        int consecutiveBonus = 0;

        string lowerPattern = pattern.ToLowerInvariant();
        string lowerText = text.ToLowerInvariant();

        for (int i = 0; i < lowerText.Length && patternIdx < lowerPattern.Length; i++)
        {
            if (lowerText[i] != lowerPattern[patternIdx])
            {
                consecutiveBonus = 0;
                continue;
            }

            indices.Add(i);

            // Base match score
            score += 1;

            // Consecutive character bonus (exponential)
            if (lastMatchIdx == i - 1)
            {
                consecutiveBonus++;
                score += consecutiveBonus * 5;
            }
            else
            {
                consecutiveBonus = 0;
            }

            // Word boundary bonus (after space, or start of text, or camelCase)
            if (i == 0 || text[i - 1] == ' ' || text[i - 1] == '/' ||
                (char.IsUpper(text[i]) && i > 0 && char.IsLower(text[i - 1])))
            {
                score += 10;
            }

            // Exact case match bonus
            if (pattern[patternIdx] == text[i])
                score += 2;

            // Prefix bonus
            if (i == patternIdx)
                score += 15;

            lastMatchIdx = i;
            patternIdx++;
        }

        bool isMatch = patternIdx == lowerPattern.Length;
        return (isMatch, isMatch ? score : 0, isMatch ? indices : []);
    }
}
