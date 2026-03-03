using System.Diagnostics;
using System.IO;

namespace Nexplorer.App.Services;

/// <summary>
/// Provides Git log retrieval for a given directory by running git commands asynchronously.
/// </summary>
public static class GitHistoryService
{
    /// <summary>Format: hash | author name | author email | relative date | ISO date | subject</summary>
    private const string LogFormat = "%H|%an|%ae|%ar|%aI|%s";

    /// <summary>
    /// Returns true if the given directory is inside a Git repository.
    /// </summary>
    public static bool IsGitRepository(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return false;

        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "git",
                    Arguments              = "rev-parse --is-inside-work-tree",
                    WorkingDirectory       = path,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);
            return output == "true";
        }
        catch { return false; }
    }

    /// <summary>
    /// Returns a list of all local branch names, with the current branch first.
    /// </summary>
    public static async Task<List<string>> GetBranchesAsync(
        string workingDirectory,
        CancellationToken ct = default)
    {
        var branches = new List<string>();
        string? current = null;

        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "git",
                    Arguments              = "branch --no-color",
                    WorkingDirectory       = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };

            proc.Start();

            while (await proc.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                ct.ThrowIfCancellationRequested();
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (trimmed.StartsWith("* ", StringComparison.Ordinal))
                {
                    current = trimmed[2..].Trim();
                    // skip detached HEAD markers like "* (HEAD detached at ...)"
                    if (!current.StartsWith('('))
                        branches.Insert(0, current);
                }
                else
                {
                    branches.Add(trimmed);
                }
            }

            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* git not available */ }

        return branches;
    }

    /// <summary>
    /// Loads Git log entries for the given directory. If <paramref name="filePath"/> is
    /// specified, retrieves history for that specific file only.
    /// Optionally filters by <paramref name="branch"/>.
    /// </summary>
    public static async Task<List<GitLogEntry>> GetLogAsync(
        string workingDirectory,
        string? filePath = null,
        int maxCount = 200,
        string? branch = null,
        CancellationToken ct = default)
    {
        var args = $"log --format=\"{LogFormat}\" -n {maxCount}";
        if (!string.IsNullOrEmpty(branch))
            args += $" {branch}";
        if (!string.IsNullOrEmpty(filePath))
            args += $" -- \"{filePath}\"";

        var entries = new List<GitLogEntry>();

        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "git",
                    Arguments              = args,
                    WorkingDirectory       = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };

            proc.Start();

            while (await proc.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                ct.ThrowIfCancellationRequested();
                var entry = ParseLine(line);
                if (entry is not null)
                    entries.Add(entry);
            }

            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* git not available or not a repo */ }

        return entries;
    }

    /// <summary>
    /// Gets the diff for a specific commit.
    /// </summary>
    public static async Task<string> GetCommitDiffAsync(
        string workingDirectory,
        string commitHash,
        CancellationToken ct = default)
    {
        var args = $"show --stat --patch {commitHash}";

        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "git",
                    Arguments              = args,
                    WorkingDirectory       = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };

            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            return output;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    /// <summary>
    /// Gets the diff for a specific file within a commit.
    /// </summary>
    public static async Task<string> GetCommitFileDiffAsync(
        string workingDirectory,
        string commitHash,
        string filePath,
        CancellationToken ct = default)
    {
        var args = $"show {commitHash} -- \"{filePath}\"";

        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "git",
                    Arguments              = args,
                    WorkingDirectory       = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };

            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            return output;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    /// <summary>
    /// Gets the content of a file at a specific commit.
    /// </summary>
    public static async Task<string?> GetFileAtCommitAsync(
        string workingDirectory,
        string commitHash,
        string filePath,
        CancellationToken ct = default)
    {
        var args = $"show {commitHash}:\"{filePath.Replace('\\', '/')}\"";

        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "git",
                    Arguments              = args,
                    WorkingDirectory       = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };

            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode == 0 ? output : null;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    /// <summary>
    /// Gets the list of files changed in a specific commit.
    /// </summary>
    public static async Task<List<GitChangedFile>> GetCommitFilesAsync(
        string workingDirectory,
        string commitHash,
        CancellationToken ct = default)
    {
        var files = new List<GitChangedFile>();
        var args = $"diff-tree --no-commit-id --name-status -r {commitHash}";

        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "git",
                    Arguments              = args,
                    WorkingDirectory       = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };

            proc.Start();

            while (await proc.StandardOutput.ReadLineAsync(ct) is { } line)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Format: "M\tpath/to/file" or "R100\told\tnew"
                var parts = line.Split('\t', 3);
                if (parts.Length < 2) continue;

                var statusCode = parts[0].Trim();
                var filePath = parts.Length >= 3 ? parts[2] : parts[1]; // for renames, show new path
                var oldPath = parts.Length >= 3 ? parts[1] : null;

                var status = statusCode[0] switch
                {
                    'A' => FileChangeStatus.Added,
                    'M' => FileChangeStatus.Modified,
                    'D' => FileChangeStatus.Deleted,
                    'R' => FileChangeStatus.Renamed,
                    'C' => FileChangeStatus.Copied,
                    'T' => FileChangeStatus.TypeChanged,
                    _   => FileChangeStatus.Modified,
                };

                files.Add(new GitChangedFile(filePath, status, oldPath));
            }

            await proc.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* git not available */ }

        return files;
    }

    private static GitLogEntry? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var parts = line.Split('|', 6);
        if (parts.Length < 6) return null;

        if (!DateTimeOffset.TryParse(parts[4], out var date))
            date = default;

        return new GitLogEntry(
            Hash:         parts[0],
            AuthorName:   parts[1],
            AuthorEmail:  parts[2],
            RelativeDate: parts[3],
            Date:         date,
            Subject:      parts[5]);
    }
}

/// <summary>Status of a file change in a commit.</summary>
public enum FileChangeStatus
{
    Added,
    Modified,
    Deleted,
    Renamed,
    Copied,
    TypeChanged,
}

/// <summary>Represents a single file changed in a commit.</summary>
public sealed record GitChangedFile(
    string           Path,
    FileChangeStatus Status,
    string?          OldPath = null)
{
    public string FileName => System.IO.Path.GetFileName(Path);
    public string Directory => System.IO.Path.GetDirectoryName(Path)?.Replace('\\', '/') ?? string.Empty;

    public string StatusLabel => Status switch
    {
        FileChangeStatus.Added       => "A",
        FileChangeStatus.Modified    => "M",
        FileChangeStatus.Deleted     => "D",
        FileChangeStatus.Renamed     => "R",
        FileChangeStatus.Copied      => "C",
        FileChangeStatus.TypeChanged => "T",
        _                            => "?",
    };

    public string StatusColor => Status switch
    {
        FileChangeStatus.Added       => "#4EC94E",
        FileChangeStatus.Modified    => "#E2C04B",
        FileChangeStatus.Deleted     => "#F14C4C",
        FileChangeStatus.Renamed     => "#569CD6",
        FileChangeStatus.Copied      => "#569CD6",
        FileChangeStatus.TypeChanged => "#C586C0",
        _                            => "#AAAAAA",
    };
}

/// <summary>Represents a single Git log entry.</summary>
public sealed record GitLogEntry(
    string         Hash,
    string         AuthorName,
    string         AuthorEmail,
    string         RelativeDate,
    DateTimeOffset Date,
    string         Subject)
{
    public string ShortHash => Hash.Length >= 7 ? Hash[..7] : Hash;

    /// <summary>First letter of the author name (used as avatar placeholder).</summary>
    public string AuthorInitial =>
        string.IsNullOrEmpty(AuthorName) ? "?" : AuthorName[..1].ToUpperInvariant();
}
