using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace Nexplorer.App.Services;

/// <summary>Thin wrappers around file-system operations used by PaneViewModel commands.</summary>
public static class FileOperationService
{
    // ─── Delete ─────────────────────────────────────────────────────────────

    /// <summary>Sends each path to the Recycle Bin via VisualBasic.FileSystem helpers.</summary>
    public static void RecycleAll(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (Directory.Exists(path))
                    FileSystem.DeleteDirectory(path,
                        UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                else if (File.Exists(path))
                    FileSystem.DeleteFile(path,
                        UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            }
            catch { /* swallow – VisualBasic already showed an error dialog */ }
        }
    }

    // ─── Rename ─────────────────────────────────────────────────────────────

    /// <summary>Renames a file or directory in its current parent folder.</summary>
    public static void Rename(string fullPath, string newName)
    {
        var dir     = Path.GetDirectoryName(fullPath)!;
        var newPath = Path.Combine(dir, newName);

        if (Directory.Exists(fullPath))
            Directory.Move(fullPath, newPath);
        else if (File.Exists(fullPath))
            File.Move(fullPath, newPath);
    }

    // ─── New folder ──────────────────────────────────────────────────────────

    /// <summary>Creates a new folder under <paramref name="parentPath"/>,
    /// auto-appending (2), (3) … if the name already exists.
    /// Returns the full path of the created folder.</summary>
    public static string CreateFolder(string parentPath, string suggestedName = "New folder")
    {
        var path = Path.Combine(parentPath, suggestedName);
        int n    = 2;
        while (Directory.Exists(path))
            path = Path.Combine(parentPath, $"{suggestedName} ({n++})");
        Directory.CreateDirectory(path);
        return path;
    }

    // ─── New file ─────────────────────────────────────────────────────────────

    /// <summary>Creates a new empty file under <paramref name="parentPath"/>,
    /// auto-appending (2), (3) … if the name already exists.
    /// Returns the full path of the created file.</summary>
    public static string CreateFile(string parentPath, string suggestedName = "New file.txt")
    {
        var nameOnly = Path.GetFileNameWithoutExtension(suggestedName);
        var ext = Path.GetExtension(suggestedName);
        var path = Path.Combine(parentPath, suggestedName);
        int n = 2;
        while (File.Exists(path))
            path = Path.Combine(parentPath, $"{nameOnly} ({n++}){ext}");
        File.Create(path).Dispose();
        return path;
    }

    // ─── Copy ─────────────────────────────────────────────────────────────────

    public static async Task CopyAsync(
        IEnumerable<string> sources,
        string destDir,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var list = sources.ToList();
        await Task.Run(() =>
        {
            foreach (var src in list)
            {
                ct.ThrowIfCancellationRequested();
                if (Directory.Exists(src))
                    CopyDirectoryRecursive(src,
                        Path.Combine(destDir, Path.GetFileName(src)), progress, ct);
                else if (File.Exists(src))
                {
                    var dest = GetUniqueDestPath(destDir, Path.GetFileName(src));
                    progress?.Report($"Copying {Path.GetFileName(src)}…");
                    File.Copy(src, dest, overwrite: false);
                }
            }
        }, ct);
    }

    // ─── Move ─────────────────────────────────────────────────────────────────

    public static async Task MoveAsync(
        IEnumerable<string> sources,
        string destDir,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var list = sources.ToList();
        await Task.Run(() =>
        {
            foreach (var src in list)
            {
                ct.ThrowIfCancellationRequested();
                var dest = GetUniqueDestPath(destDir, Path.GetFileName(src));
                progress?.Report($"Moving {Path.GetFileName(src)}…");
                if (Directory.Exists(src))
                    Directory.Move(src, dest);
                else if (File.Exists(src))
                    File.Move(src, dest, overwrite: false);
            }
        }, ct);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static string GetUniqueDestPath(string destDir, string fileName)
    {
        var path = Path.Combine(destDir, fileName);
        if (!File.Exists(path) && !Directory.Exists(path)) return path;

        var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
        var ext       = Path.GetExtension(fileName);
        int n = 2;
        do
        {
            path = Path.Combine(destDir, $"{nameNoExt} ({n++}){ext}");
        } while (File.Exists(path) || Directory.Exists(path));
        return path;
    }

    private static void CopyDirectoryRecursive(
        string src, string dest,
        IProgress<string>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.GetFiles(src))
        {
            ct.ThrowIfCancellationRequested();
            var fn = Path.GetFileName(f);
            progress?.Report($"Copying {fn}…");
            File.Copy(f, Path.Combine(dest, fn), overwrite: true);
        }
        foreach (var d in Directory.GetDirectories(src))
            CopyDirectoryRecursive(d, Path.Combine(dest, Path.GetFileName(d)), progress, ct);
    }
}
