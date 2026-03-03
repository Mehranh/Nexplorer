using System.Collections.Concurrent;
using System.IO;

namespace Nexplorer.App.Services;

/// <summary>
/// Represents a node in the disk usage tree.
/// </summary>
public sealed class DiskNode
{
    public string Name { get; init; } = "";
    public string FullPath { get; init; } = "";
    public long Size { get; set; }
    public bool IsDirectory { get; init; }
    public List<DiskNode> Children { get; } = [];
}

/// <summary>
/// Async disk space analyzer that streams progress as it scans.
/// </summary>
public static class DiskAnalyzerService
{
    public static async Task<DiskNode> AnalyzeAsync(string rootPath,
        IProgress<(int filesScanned, long totalBytes)>? progress = null,
        CancellationToken ct = default)
    {
        var root = new DiskNode
        {
            Name = Path.GetFileName(rootPath) is { Length: > 0 } name ? name : rootPath,
            FullPath = rootPath,
            IsDirectory = true
        };

        int filesScanned = 0;
        long totalBytes = 0;

        await Task.Run(() => ScanDirectory(root, ref filesScanned, ref totalBytes, progress, ct), ct);

        root.Size = totalBytes;
        return root;
    }

    private static void ScanDirectory(DiskNode node, ref int filesScanned,
        ref long totalBytes, IProgress<(int, long)>? progress, CancellationToken ct)
    {
        DirectoryInfo dirInfo;
        try { dirInfo = new DirectoryInfo(node.FullPath); }
        catch { return; }

        // Scan files
        try
        {
            foreach (var fi in dirInfo.EnumerateFiles())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    long size = fi.Length;
                    node.Children.Add(new DiskNode
                    {
                        Name = fi.Name,
                        FullPath = fi.FullName,
                        Size = size,
                        IsDirectory = false
                    });
                    node.Size += size;
                    totalBytes += size;
                    filesScanned++;

                    if (filesScanned % 500 == 0)
                        progress?.Report((filesScanned, totalBytes));
                }
                catch { /* skip inaccessible files */ }
            }
        }
        catch { /* skip inaccessible directory listing */ }

        // Scan subdirectories
        try
        {
            foreach (var di in dirInfo.EnumerateDirectories())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var childNode = new DiskNode
                    {
                        Name = di.Name,
                        FullPath = di.FullName,
                        IsDirectory = true
                    };
                    ScanDirectory(childNode, ref filesScanned, ref totalBytes, progress, ct);
                    node.Size += childNode.Size;
                    if (childNode.Size > 0)
                        node.Children.Add(childNode);
                }
                catch { /* skip inaccessible subdirectory */ }
            }
        }
        catch { /* skip inaccessible directory listing */ }

        // Sort children by size descending for better treemap layout
        node.Children.Sort((a, b) => b.Size.CompareTo(a.Size));

        progress?.Report((filesScanned, totalBytes));
    }
}
