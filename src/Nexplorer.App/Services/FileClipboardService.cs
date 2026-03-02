namespace Nexplorer.App.Services;

public enum ClipboardAction { Copy, Cut }

/// <summary>Application-level file clipboard (independent of the Windows Clipboard).</summary>
public static class FileClipboardService
{
    private static (IReadOnlyList<string> Paths, ClipboardAction Action)? _clip;

    public static (IReadOnlyList<string> Paths, ClipboardAction Action)? Current => _clip;

    public static bool HasFiles => (_clip?.Paths.Count ?? 0) > 0;

    public static void Set(IEnumerable<string> paths, ClipboardAction action)
        => _clip = (paths.ToList().AsReadOnly(), action);

    public static void Clear() => _clip = null;
}
