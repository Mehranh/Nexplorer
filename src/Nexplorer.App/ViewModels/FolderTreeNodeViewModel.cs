using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Nexplorer.App.Services;

namespace Nexplorer.App.ViewModels;

// ── Single tree node ──────────────────────────────────────────────────────────

public sealed partial class FolderTreeNodeViewModel : ObservableObject
{
    // Dummy sentinel – keeps the expand arrow visible before children are loaded
    private static readonly FolderTreeNodeViewModel s_dummy =
        new("…", null, null, addDummy: false);

    public string        Name        { get; }
    public string?       FullPath    { get; }   // null → section header, non-navigable
    public BitmapSource? Icon        { get; }
    public bool          IsHeader    { get; }   // display as a section label

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;

    public ObservableCollection<FolderTreeNodeViewModel> Children { get; } = [];

    private bool _loaded;

    public FolderTreeNodeViewModel(
        string name, string? path, BitmapSource? icon,
        bool addDummy = true, bool isHeader = false)
    {
        Name     = name;
        FullPath = path;
        Icon     = icon;
        IsHeader = isHeader;
        if (addDummy && path is not null)
            Children.Add(s_dummy);
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && !_loaded) _ = LoadChildrenAsync();
    }

    private async Task LoadChildrenAsync()
    {
        _loaded = true;
        var path = FullPath;
        if (path is null) { Children.Clear(); return; }

        string[] dirs;
        try   { dirs = await Task.Run(() => Directory.GetDirectories(path)); }
        catch { Children.Clear(); return; }

        var folderIcon = ShellIconService.GetFolderIcon();
        var nodes = dirs
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .Select(d => new FolderTreeNodeViewModel(
                Path.GetFileName(d), d, folderIcon))
            .ToArray();

        Children.Clear();
        foreach (var n in nodes) Children.Add(n);
    }
}

// ── Root tree (quick-access + drives) ─────────────────────────────────────────

public sealed class FolderTreeViewModel
{
    private readonly FavoritesService _favService = new();
    private int _favoritesInsertIndex;
    private int _recentHeaderIndex;

    public ObservableCollection<FolderTreeNodeViewModel> Roots { get; } = [];

    public FolderTreeViewModel()
    {
        // ── Favorites section ─────────────────────────────────────────────────
        AddHeader("⭐  Favorites");
        _favoritesInsertIndex = Roots.Count;
        foreach (var fav in _favService.Load())
        {
            if (Directory.Exists(fav))
            {
                Roots.Add(new FolderTreeNodeViewModel(
                    Path.GetFileName(fav.TrimEnd(Path.DirectorySeparatorChar)),
                    fav, ShellIconService.GetPathIcon(fav)));
            }
        }

        // ── Recent section ────────────────────────────────────────────────────
        AddHeader("🕒  Recent");
        _recentHeaderIndex = Roots.Count - 1;

        // ── Quick Access section header ───────────────────────────────────────
        AddHeader("⚡  Quick Access");

        TryAddSpecial("Home",      Environment.SpecialFolder.UserProfile);
        TryAddSpecial("Desktop",   Environment.SpecialFolder.Desktop);
        TryAddSpecial("Documents", Environment.SpecialFolder.MyDocuments);
        TryAddDownloads();
        TryAddSpecial("Pictures",  Environment.SpecialFolder.MyPictures);
        TryAddSpecial("Music",     Environment.SpecialFolder.MyMusic);
        TryAddSpecial("Videos",    Environment.SpecialFolder.MyVideos);

        // ── This PC section header ────────────────────────────────────────────
        AddHeader("💻  This PC");

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady) continue;
                var root  = drive.RootDirectory.FullName;
                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? root.TrimEnd('\\')
                    : $"{drive.VolumeLabel} ({root.TrimEnd('\\')})";
                Roots.Add(new FolderTreeNodeViewModel(
                    label, root, ShellIconService.GetPathIcon(root)));
            }
            catch { /* drive not ready / access denied */ }
        }
    }

    /// <summary>Adds a folder to the Favorites section.</summary>
    public void AddFavorite(string path)
    {
        if (!Directory.Exists(path)) return;

        // Check if already in favorites
        var existing = GetFavoritePaths();
        if (existing.Any(f => f.Equals(path, StringComparison.OrdinalIgnoreCase))) return;

        // Find insertion point (after the Favorites header, before next header)
        int insertAt = _favoritesInsertIndex;
        while (insertAt < Roots.Count && !Roots[insertAt].IsHeader)
            insertAt++;

        Roots.Insert(insertAt, new FolderTreeNodeViewModel(
            Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)),
            path, ShellIconService.GetPathIcon(path)));

        SaveFavorites();
    }

    /// <summary>Removes a folder from the Favorites section.</summary>
    public void RemoveFavorite(string path)
    {
        var nodeToRemove = Roots
            .Where(r => !r.IsHeader && r.FullPath != null)
            .FirstOrDefault(r => r.FullPath!.Equals(path, StringComparison.OrdinalIgnoreCase));

        if (nodeToRemove is not null)
        {
            var idx = Roots.IndexOf(nodeToRemove);
            if (idx >= _favoritesInsertIndex)
            {
                Roots.Remove(nodeToRemove);
                SaveFavorites();
            }
        }
    }

    public bool IsFavorite(string path) =>
        GetFavoritePaths().Any(f => f.Equals(path, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<string> GetFavoritePaths()
    {
        // Favorites are between _favoritesInsertIndex and the next header
        for (int i = _favoritesInsertIndex; i < Roots.Count; i++)
        {
            if (Roots[i].IsHeader) break;
            if (Roots[i].FullPath is not null)
                yield return Roots[i].FullPath!;
        }
    }

    private void SaveFavorites() => _favService.Save(GetFavoritePaths());

    // ── Recent Locations ──────────────────────────────────────────────────────

    /// <summary>Replaces the Recent section with the given paths (max 5, most recent first).</summary>
    public void SetRecentLocations(IEnumerable<string> paths)
    {
        // Find the range of recent nodes: from _recentHeaderIndex+1 until the next header
        int start = _recentHeaderIndex + 1;
        int end = start;
        while (end < Roots.Count && !Roots[end].IsHeader)
            end++;

        // Remove existing recent items
        for (int i = end - 1; i >= start; i--)
            Roots.RemoveAt(i);

        // Insert new recent items
        int insertAt = start;
        foreach (var p in paths.Take(5))
        {
            if (!Directory.Exists(p)) continue;
            Roots.Insert(insertAt++, new FolderTreeNodeViewModel(
                Path.GetFileName(p.TrimEnd(Path.DirectorySeparatorChar)),
                p, ShellIconService.GetPathIcon(p), addDummy: false));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void AddHeader(string title)
        => Roots.Add(new FolderTreeNodeViewModel(
            title, null, null, addDummy: false, isHeader: true));

    private void TryAddSpecial(string label, Environment.SpecialFolder sf)
    {
        var path = Environment.GetFolderPath(sf);
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            Roots.Add(new FolderTreeNodeViewModel(
                label, path, ShellIconService.GetPathIcon(path)));
    }

    private void TryAddDownloads()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        if (Directory.Exists(path))
            Roots.Add(new FolderTreeNodeViewModel(
                "Downloads", path, ShellIconService.GetPathIcon(path)));
    }
}
