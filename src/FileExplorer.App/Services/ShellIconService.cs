using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FileExplorer.App.Services;

/// <summary>
/// Fetches native Windows shell icons using the system image list (SHGetImageList).
/// Requests SHIL_LARGE (32×32) to get the full-colour Windows 11-style icons.
/// Results are permanently cached by extension / path key.
/// All public methods are safe to call from any thread.
/// </summary>
public static class ShellIconService
{
    // ── P/Invoke ─────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFOW
    {
        public IntPtr hIcon;
        public int    iIcon;
        public uint   dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]  public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern IntPtr SHGetFileInfoW(
        string pszPath, uint dwFileAttributes,
        ref SHFILEINFOW psfi, uint cbFileInfo, uint uFlags);

    // SHGetImageList returns the system image list for a given size tier.
    [DllImport("shell32.dll", SetLastError = false)]
    private static extern int SHGetImageList(int iImageList, ref Guid riid, out IntPtr ppv);

    // IImageList COM methods we need (vtable slots 8 & 11).
    [DllImport("comctl32.dll", SetLastError = false)]
    private static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);

    [DllImport("user32.dll", SetLastError = false)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    // SHGFI flags
    private const uint SHGFI_SYSICONINDEX     = 0x4000;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x0010;
    private const uint SHGFI_ICON              = 0x0100;
    private const uint SHGFI_SMALLICON         = 0x0001;

    // File attribute flags
    private const uint FILE_ATTRIBUTE_NORMAL    = 0x0080;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x0010;

    // System image list size tiers
    // SHIL_LARGE = 32×32 — gives the full-colour Windows 11 icons
    private const int SHIL_LARGE      = 0x0;   // 32×32
    private const int SHIL_SMALL      = 0x1;   // 16×16
    private const int SHIL_EXTRALARGE = 0x2;   // 48×48

    // IImageList IID
    private static readonly Guid IID_IImageList =
        new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    // ILR_DEFAULT flag for ImageList_GetIcon
    private const uint ILD_TRANSPARENT = 0x0001;

    // ── Cache ─────────────────────────────────────────────────────────────────

    private static readonly ConcurrentDictionary<string, BitmapSource> s_cache =
        new(StringComparer.OrdinalIgnoreCase);

    // Lazily-obtained handle to the 32×32 system image list.
    private static IntPtr s_imageList32 = IntPtr.Zero;
    private static readonly object s_imageListLock = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the 32×32 Windows 11-style shell icon for a file of the given extension
    /// without touching the disk (uses SHGFI_USEFILEATTRIBUTES).
    /// <paramref name="dotExtension"/> should include the dot, e.g. ".cs".
    /// Pass an empty string for files with no extension.
    /// </summary>
    public static BitmapSource GetFileIcon(string dotExtension)
    {
        var key = dotExtension.ToUpperInvariant();
        if (s_cache.TryGetValue(key, out var cached)) return cached;

        var fakePath = string.IsNullOrEmpty(dotExtension) ? "file" : ("file" + dotExtension);
        var bmp = FetchViaAttribs(fakePath, isDir: false);
        s_cache.TryAdd(key, bmp);
        return bmp;
    }

    /// <summary>Generic 32×32 folder icon (cached permanently).</summary>
    public static BitmapSource GetFolderIcon()
        => s_cache.GetOrAdd("__FOLDER__", _ => FetchViaAttribs("folder", isDir: true));

    /// <summary>
    /// 32×32 Windows 11-style shell icon for a concrete path (drive root, special folder …).
    /// Reads the actual shell icon, so drives get drive-type icons.
    /// </summary>
    public static BitmapSource GetPathIcon(string fullPath)
    {
        if (s_cache.TryGetValue(fullPath, out var cached)) return cached;
        var bmp = FetchReal(fullPath);
        s_cache.TryAdd(fullPath, bmp);
        return bmp;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>Gets the 32×32 icon index then fetches from the system image list.</summary>
    private static BitmapSource FetchViaAttribs(string path, bool isDir)
    {
        var shfi  = new SHFILEINFOW();
        uint attrs = isDir ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
        SHGetFileInfoW(path, attrs, ref shfi, (uint)Marshal.SizeOf<SHFILEINFOW>(),
                       SHGFI_SYSICONINDEX | SHGFI_USEFILEATTRIBUTES);
        return FromIconIndex(shfi.iIcon);
    }

    /// <summary>Gets the 32×32 icon for a real path via the system image list.</summary>
    private static BitmapSource FetchReal(string path)
    {
        var shfi  = new SHFILEINFOW();
        SHGetFileInfoW(path, 0, ref shfi, (uint)Marshal.SizeOf<SHFILEINFOW>(),
                       SHGFI_SYSICONINDEX);
        return FromIconIndex(shfi.iIcon);
    }

    private static IntPtr GetImageList32()
    {
        if (s_imageList32 != IntPtr.Zero) return s_imageList32;
        lock (s_imageListLock)
        {
            if (s_imageList32 != IntPtr.Zero) return s_imageList32;
            var iid = IID_IImageList;
            SHGetImageList(SHIL_LARGE, ref iid, out var himl);
            s_imageList32 = himl;
            return himl;
        }
    }

    private static BitmapSource FromIconIndex(int index)
    {
        var himl = GetImageList32();
        if (himl == IntPtr.Zero) return Fallback();

        var hIcon = ImageList_GetIcon(himl, index, ILD_TRANSPARENT);
        if (hIcon == IntPtr.Zero) return Fallback();

        try
        {
            var bmp = Imaging.CreateBitmapSourceFromHIcon(
                hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bmp.Freeze();
            return bmp;
        }
        finally { DestroyIcon(hIcon); }
    }

    private static BitmapSource? s_fallback;
    private static BitmapSource Fallback()
    {
        if (s_fallback is not null) return s_fallback;
        var bmp = new WriteableBitmap(32, 32, 96, 96, PixelFormats.Bgra32, null);
        bmp.Freeze();
        s_fallback = bmp;
        return s_fallback;
    }
}
