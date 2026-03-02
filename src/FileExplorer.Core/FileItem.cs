namespace FileExplorer.Core;

public sealed class FileItem
{
    public string FullPath   { get; init; } = string.Empty;
    public string Name       { get; init; } = string.Empty;
    public bool   IsDirectory { get; init; }
    public long?  SizeBytes  { get; init; }
    public DateTime LastWriteTimeUtc { get; init; }
    public string Extension  { get; init; } = string.Empty;
}
