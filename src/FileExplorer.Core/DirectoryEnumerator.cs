using System.IO;

namespace FileExplorer.Core;

public static class DirectoryEnumerator
{
    public static IEnumerable<FileItem> Enumerate(string path, CancellationToken ct = default)
    {
        FileSystemInfo[] entries;
        try
        {
            entries = new DirectoryInfo(path).GetFileSystemInfos();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
            yield break;
        }

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            if (entry is DirectoryInfo dir)
            {
                yield return new FileItem
                {
                    FullPath         = dir.FullName,
                    Name             = dir.Name,
                    IsDirectory      = true,
                    SizeBytes        = null,
                    LastWriteTimeUtc = dir.LastWriteTimeUtc,
                };
            }
            else if (entry is FileInfo file)
            {
                yield return new FileItem
                {
                    FullPath         = file.FullName,
                    Name             = file.Name,
                    IsDirectory      = false,
                    SizeBytes        = file.Length,
                    LastWriteTimeUtc = file.LastWriteTimeUtc,
                    Extension        = file.Extension,
                };
            }
        }
    }
}
