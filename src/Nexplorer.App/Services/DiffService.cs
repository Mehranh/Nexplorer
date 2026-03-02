namespace Nexplorer.App.Services;

public enum DiffLineKind
{
    Unchanged,
    Added,
    Removed,
}

public sealed record DiffLine(string Text, DiffLineKind Kind, int? LineNumber);

/// <summary>
/// Computes a simple line-level diff between two texts using the LCS algorithm.
/// Returns two lists of DiffLine corresponding to the old and new sides.
/// </summary>
public static class DiffService
{
    /// <summary>
    /// Produces side-by-side diff lines from old and new content.
    /// </summary>
    public static (List<DiffLine> OldLines, List<DiffLine> NewLines) ComputeSideBySide(
        string oldText, string newText)
    {
        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);

        var lcs = ComputeLcs(oldLines, newLines);

        var resultOld = new List<DiffLine>();
        var resultNew = new List<DiffLine>();

        int oi = 0, ni = 0;
        foreach (var (oldIdx, newIdx) in lcs)
        {
            // Emit removed lines before this match
            while (oi < oldIdx)
            {
                resultOld.Add(new DiffLine(oldLines[oi], DiffLineKind.Removed, oi + 1));
                resultNew.Add(new DiffLine(string.Empty, DiffLineKind.Removed, null));
                oi++;
            }

            // Emit added lines before this match
            while (ni < newIdx)
            {
                resultOld.Add(new DiffLine(string.Empty, DiffLineKind.Added, null));
                resultNew.Add(new DiffLine(newLines[ni], DiffLineKind.Added, ni + 1));
                ni++;
            }

            // Emit matching line
            resultOld.Add(new DiffLine(oldLines[oi], DiffLineKind.Unchanged, oi + 1));
            resultNew.Add(new DiffLine(newLines[ni], DiffLineKind.Unchanged, ni + 1));
            oi++;
            ni++;
        }

        // Remaining removed lines
        while (oi < oldLines.Length)
        {
            resultOld.Add(new DiffLine(oldLines[oi], DiffLineKind.Removed, oi + 1));
            resultNew.Add(new DiffLine(string.Empty, DiffLineKind.Removed, null));
            oi++;
        }

        // Remaining added lines
        while (ni < newLines.Length)
        {
            resultOld.Add(new DiffLine(string.Empty, DiffLineKind.Added, null));
            resultNew.Add(new DiffLine(newLines[ni], DiffLineKind.Added, ni + 1));
            ni++;
        }

        return (resultOld, resultNew);
    }

    private static string[] SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        return text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
    }

    /// <summary>
    /// Computes LCS indices using a standard DP approach with O(n*m) time.
    /// For very large files, limits comparison to avoid UI freeze.
    /// </summary>
    private static List<(int OldIdx, int NewIdx)> ComputeLcs(string[] oldLines, string[] newLines)
    {
        int m = oldLines.Length, n = newLines.Length;

        // For extremely large files, fall back to simple line-by-line comparison
        if ((long)m * n > 2_000_000)
            return ComputeLcsFallback(oldLines, newLines);

        var dp = new int[m + 1, n + 1];

        for (int i = m - 1; i >= 0; i--)
        for (int j = n - 1; j >= 0; j--)
        {
            if (oldLines[i] == newLines[j])
                dp[i, j] = dp[i + 1, j + 1] + 1;
            else
                dp[i, j] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
        }

        var result = new List<(int, int)>();
        int oi = 0, ni = 0;
        while (oi < m && ni < n)
        {
            if (oldLines[oi] == newLines[ni])
            {
                result.Add((oi, ni));
                oi++;
                ni++;
            }
            else if (dp[oi + 1, ni] >= dp[oi, ni + 1])
                oi++;
            else
                ni++;
        }

        return result;
    }

    private static List<(int OldIdx, int NewIdx)> ComputeLcsFallback(string[] oldLines, string[] newLines)
    {
        var result = new List<(int, int)>();
        int oi = 0, ni = 0;
        while (oi < oldLines.Length && ni < newLines.Length)
        {
            if (oldLines[oi] == newLines[ni])
            {
                result.Add((oi, ni));
                oi++;
                ni++;
            }
            else
            {
                // Try to find a match nearby
                int lookAhead = Math.Min(20, Math.Max(oldLines.Length - oi, newLines.Length - ni));
                bool found = false;
                for (int d = 1; d <= lookAhead && !found; d++)
                {
                    if (oi + d < oldLines.Length && oldLines[oi + d] == newLines[ni])
                    {
                        oi += d;
                        found = true;
                    }
                    else if (ni + d < newLines.Length && oldLines[oi] == newLines[ni + d])
                    {
                        ni += d;
                        found = true;
                    }
                }
                if (!found) { oi++; ni++; }
            }
        }
        return result;
    }
}
