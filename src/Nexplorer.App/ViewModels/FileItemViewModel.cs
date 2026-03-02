using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Nexplorer.App.Services;
using Nexplorer.Core;

namespace Nexplorer.App.ViewModels;

/// <summary>
/// Mutable view-model wrapper around <see cref="FileItem"/>.
/// Supports inline rename (IsEditing / EditingName).
/// </summary>
public sealed partial class FileItemViewModel : ObservableObject
{
    public FileItemViewModel(FileItem item)
    {
        FullPath           = item.FullPath;
        Name               = item.Name;
        IsDirectory        = item.IsDirectory;
        SizeBytes          = item.SizeBytes;
        LastWriteTimeLocal = item.LastWriteTimeUtc.ToLocalTime();
        Extension          = item.Extension.TrimStart('.').ToUpperInvariant();
        EditingName        = item.Name;
    }

    // ─── Immutable fields ────────────────────────────────────────────────────

    public string   FullPath           { get; }
    public bool     IsDirectory        { get; }
    public long?    SizeBytes          { get; }
    public DateTime LastWriteTimeLocal { get; }
    public string   Extension          { get; }   // uppercase, no dot

    // ─── Mutable: name can change after a rename ──────────────────────────

    [ObservableProperty] private string _name = string.Empty;

    // ─── Inline rename state ──────────────────────────────────────────────

    [ObservableProperty] private bool   _isEditing;
    [ObservableProperty] private string _editingName = string.Empty;

    // ─── Shell icon (lazily resolved) ─────────────────────────────────────

    public BitmapSource ShellIcon =>
        IsDirectory
            ? ShellIconService.GetFolderIcon()
            : ShellIconService.GetFileIcon(
                string.IsNullOrEmpty(Extension) ? string.Empty : "." + Extension);

    // ─── Display helpers ──────────────────────────────────────────────────

    public string SizeDisplay =>
        IsDirectory ? string.Empty :
        SizeBytes is null ? string.Empty :
        SizeBytes.Value switch
        {
            < 1_024               => $"{SizeBytes.Value} B",
            < 1_024 * 1_024       => $"{SizeBytes.Value / 1024.0:0.#} KB",
            < 1_024L * 1_024 * 1_024 => $"{SizeBytes.Value / (1024.0 * 1024):0.#} MB",
            _                     => $"{SizeBytes.Value / (1024.0 * 1024 * 1024):0.##} GB",
        };

    public string TypeDisplay =>
        IsDirectory ? "Folder" :
        (string.IsNullOrEmpty(Extension) ? "File" : Extension + " File");

    // ─── Rename helpers ───────────────────────────────────────────────────

    /// <summary>Begins inline rename, pre-selecting the base name (no extension for files).</summary>
    public void BeginRename()
    {
        EditingName = Name;
        IsEditing   = true;
    }

    /// <summary>Commits a rename – callers must perform the actual filesystem operation.</summary>
    public void CommitRename(string newName)
    {
        Name      = string.IsNullOrWhiteSpace(newName) ? Name : newName.Trim();
        IsEditing = false;
    }

    /// <summary>Cancels inline rename without touching the filesystem.</summary>
    public void CancelRename()
    {
        EditingName = Name;
        IsEditing   = false;
    }
}
