using System.IO;

namespace Nexplorer.App.Services;

public enum CompareStatus { Same, LeftOnly, RightOnly, Different }

public sealed record CompareResult(
    string Name,
    bool   IsDirectory,
    CompareStatus Status,
    long?  LeftSize,
    long?  RightSize,
    DateTime? LeftModified,
    DateTime? RightModified);

/// <summary>Compares two folders side by side.</summary>
public static class FolderCompareService
{
    public static IReadOnlyList<CompareResult> Compare(string leftDir, string rightDir)
    {
        var results = new List<CompareResult>();

        var leftEntries  = GetEntries(leftDir);
        var rightEntries = GetEntries(rightDir);

        var allNames = leftEntries.Keys
            .Union(rightEntries.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        foreach (var name in allNames)
        {
            var hasLeft  = leftEntries.TryGetValue(name, out var left);
            var hasRight = rightEntries.TryGetValue(name, out var right);

            if (hasLeft && !hasRight)
            {
                results.Add(new CompareResult(name, left!.IsDir, CompareStatus.LeftOnly,
                    left.Size, null, left.Modified, null));
            }
            else if (!hasLeft && hasRight)
            {
                results.Add(new CompareResult(name, right!.IsDir, CompareStatus.RightOnly,
                    null, right.Size, null, right.Modified));
            }
            else if (hasLeft && hasRight)
            {
                var status = (left!.IsDir && right!.IsDir)    ? CompareStatus.Same
                           : (left.Size  != right!.Size)      ? CompareStatus.Different
                           : (left.Modified != right.Modified) ? CompareStatus.Different
                           : CompareStatus.Same;

                results.Add(new CompareResult(name, left.IsDir || right.IsDir, status,
                    left.Size, right.Size, left.Modified, right.Modified));
            }
        }

        return results;
    }

    private static Dictionary<string, EntryInfo> GetEntries(string dir)
    {
        var map = new Dictionary<string, EntryInfo>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(dir)) return map;
        try
        {
            foreach (var entry in new DirectoryInfo(dir).GetFileSystemInfos())
            {
                var info = new EntryInfo(
                    entry is DirectoryInfo,
                    entry is FileInfo fi ? fi.Length : null,
                    entry.LastWriteTimeUtc);
                map[entry.Name] = info;
            }
        }
        catch { /* ignore access denied */ }
        return map;
    }

    private sealed record EntryInfo(bool IsDir, long? Size, DateTime Modified);
}
