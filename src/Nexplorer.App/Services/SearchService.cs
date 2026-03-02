using System.IO;

namespace Nexplorer.App.Services;

/// <summary>Search filter criteria.</summary>
public sealed record SearchCriteria
{
    public string  Query          { get; init; } = string.Empty;
    public string  RootPath       { get; init; } = string.Empty;
    public bool    Recursive      { get; init; } = true;
    public bool    UseRegex       { get; init; }

    // Size filters (bytes)
    public long?   MinSize        { get; init; }
    public long?   MaxSize        { get; init; }

    // Date filters
    public DateTime? ModifiedAfter  { get; init; }
    public DateTime? ModifiedBefore { get; init; }
}

/// <summary>Searches the filesystem on a background thread, yielding matching paths.</summary>
public static class SearchService
{
    /// <summary>Returns matching file paths asynchronously.</summary>
    public static async IAsyncEnumerable<FileInfo> SearchAsync(
        SearchCriteria criteria,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var root = criteria.RootPath;
        if (!Directory.Exists(root)) yield break;

        System.Text.RegularExpressions.Regex? regex = null;
        if (criteria.UseRegex)
        {
            try { regex = new(criteria.Query, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled); }
            catch { yield break; }
        }

        var option = criteria.Recursive
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;

        IEnumerable<FileSystemInfo> entries;
        try
        {
            var di = new DirectoryInfo(root);
            entries = di.EnumerateFileSystemInfos("*", new EnumerationOptions
            {
                RecurseSubdirectories = criteria.Recursive,
                IgnoreInaccessible    = true,
                AttributesToSkip      = FileAttributes.System,
            });
        }
        catch { yield break; }

        await Task.CompletedTask; // ensure async path

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            // Name match
            bool nameMatch;
            if (regex is not null)
                nameMatch = regex.IsMatch(entry.Name);
            else
                nameMatch = entry.Name.Contains(criteria.Query, StringComparison.OrdinalIgnoreCase);

            if (!nameMatch) continue;

            // Size filter (files only)
            if (entry is FileInfo file)
            {
                if (criteria.MinSize.HasValue && file.Length < criteria.MinSize.Value) continue;
                if (criteria.MaxSize.HasValue && file.Length > criteria.MaxSize.Value) continue;
            }

            // Date filter
            if (criteria.ModifiedAfter.HasValue  && entry.LastWriteTime < criteria.ModifiedAfter.Value) continue;
            if (criteria.ModifiedBefore.HasValue && entry.LastWriteTime > criteria.ModifiedBefore.Value) continue;

            if (entry is FileInfo fi)
                yield return fi;
            else if (entry is DirectoryInfo di2)
            {
                yield return new FileInfo(di2.FullName); // return as FileInfo for compat
            }
        }
    }
}
