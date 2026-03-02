using System.Diagnostics;
using System.IO;

namespace Nexplorer.App.Services;

/// <summary>
/// Provides Git working-tree operations: status, stage, unstage, commit, push, pull, etc.
/// </summary>
public static class GitService
{
    /// <summary>
    /// Returns the list of pending changes (staged + unstaged + untracked).
    /// </summary>
    public static async Task<List<GitStatusEntry>> GetStatusAsync(
        string workingDirectory, CancellationToken ct = default)
    {
        var entries = new List<GitStatusEntry>();
        var output = await RunGitAsync(workingDirectory, "status --porcelain=v1 -uall", ct);
        if (output is null) return entries;

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 3) continue;

            var indexStatus = line[0];
            var workTreeStatus = line[1];
            var path = line[3..].Trim().Trim('"');

            // Handle renames: "R  old -> new"
            string? oldPath = null;
            var arrowIdx = path.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrowIdx >= 0)
            {
                oldPath = path[..arrowIdx];
                path = path[(arrowIdx + 4)..];
            }

            var staged = indexStatus != ' ' && indexStatus != '?';
            var area = staged ? GitFileArea.Staged : GitFileArea.Changes;

            var status = (staged ? indexStatus : workTreeStatus) switch
            {
                'A' => GitChangeKind.Added,
                'M' => GitChangeKind.Modified,
                'D' => GitChangeKind.Deleted,
                'R' => GitChangeKind.Renamed,
                'C' => GitChangeKind.Copied,
                '?' => GitChangeKind.Untracked,
                'U' => GitChangeKind.Conflict,
                _   => GitChangeKind.Modified,
            };

            // If both index and worktree have changes, emit two entries
            if (indexStatus != ' ' && indexStatus != '?' && workTreeStatus != ' ')
            {
                entries.Add(new GitStatusEntry(path, status, GitFileArea.Staged, oldPath));
                var wtStatus = workTreeStatus switch
                {
                    'M' => GitChangeKind.Modified,
                    'D' => GitChangeKind.Deleted,
                    _   => GitChangeKind.Modified,
                };
                entries.Add(new GitStatusEntry(path, wtStatus, GitFileArea.Changes));
            }
            else
            {
                entries.Add(new GitStatusEntry(path, status, area, oldPath));
            }
        }

        return entries;
    }

    /// <summary>Gets the diff for a specific file (unstaged changes).</summary>
    public static Task<string?> GetFileDiffAsync(
        string workingDirectory, string filePath, bool staged, CancellationToken ct = default)
    {
        var args = staged
            ? $"diff --cached -- \"{filePath}\""
            : $"diff -- \"{filePath}\"";
        return RunGitAsync(workingDirectory, args, ct);
    }

    /// <summary>Gets the full content of a file at HEAD.</summary>
    public static Task<string?> GetFileAtHeadAsync(
        string workingDirectory, string filePath, CancellationToken ct = default)
        => RunGitAsync(workingDirectory, $"show HEAD:\"{filePath.Replace('\\', '/')}\"", ct);

    /// <summary>Gets the current working tree content (for staged files, shows index version).</summary>
    public static Task<string?> GetFileAtIndexAsync(
        string workingDirectory, string filePath, CancellationToken ct = default)
        => RunGitAsync(workingDirectory, $"show :\"{filePath.Replace('\\', '/')}\"", ct);

    /// <summary>Stage a file.</summary>
    public static Task<string?> StageFileAsync(
        string workingDirectory, string filePath, CancellationToken ct = default)
        => RunGitAsync(workingDirectory, $"add -- \"{filePath}\"", ct);

    /// <summary>Stage all files.</summary>
    public static Task<string?> StageAllAsync(
        string workingDirectory, CancellationToken ct = default)
        => RunGitAsync(workingDirectory, "add -A", ct);

    /// <summary>Unstage a file.</summary>
    public static Task<string?> UnstageFileAsync(
        string workingDirectory, string filePath, CancellationToken ct = default)
        => RunGitAsync(workingDirectory, $"restore --staged -- \"{filePath}\"", ct);

    /// <summary>Unstage all files.</summary>
    public static Task<string?> UnstageAllAsync(
        string workingDirectory, CancellationToken ct = default)
        => RunGitAsync(workingDirectory, "reset HEAD", ct);

    /// <summary>Discard changes to a file (restore from HEAD).</summary>
    public static Task<string?> DiscardFileAsync(
        string workingDirectory, string filePath, CancellationToken ct = default)
        => RunGitAsync(workingDirectory, $"checkout -- \"{filePath}\"", ct);

    /// <summary>Commit staged changes.</summary>
    public static Task<string?> CommitAsync(
        string workingDirectory, string message, CancellationToken ct = default)
        => RunGitAsync(workingDirectory, $"commit -m \"{message.Replace("\"", "\\\"")}\"", ct);

    /// <summary>Push to the remote.</summary>
    public static Task<string?> PushAsync(
        string workingDirectory, CancellationToken ct = default)
        => RunGitAsync(workingDirectory, "push", ct);

    /// <summary>Pull from the remote.</summary>
    public static Task<string?> PullAsync(
        string workingDirectory, CancellationToken ct = default)
        => RunGitAsync(workingDirectory, "pull", ct);

    /// <summary>Fetch from the remote.</summary>
    public static Task<string?> FetchAsync(
        string workingDirectory, CancellationToken ct = default)
        => RunGitAsync(workingDirectory, "fetch", ct);

    /// <summary>Stash changes.</summary>
    public static Task<string?> StashAsync(
        string workingDirectory, CancellationToken ct = default)
        => RunGitAsync(workingDirectory, "stash", ct);

    /// <summary>Pop stash.</summary>
    public static Task<string?> StashPopAsync(
        string workingDirectory, CancellationToken ct = default)
        => RunGitAsync(workingDirectory, "stash pop", ct);

    /// <summary>Get current branch name.</summary>
    public static async Task<string?> GetCurrentBranchAsync(
        string workingDirectory, CancellationToken ct = default)
    {
        var result = await RunGitAsync(workingDirectory, "rev-parse --abbrev-ref HEAD", ct);
        return result?.Trim();
    }

    /// <summary>Get remote URL.</summary>
    public static async Task<string?> GetRemoteUrlAsync(
        string workingDirectory, CancellationToken ct = default)
    {
        var result = await RunGitAsync(workingDirectory, "remote get-url origin", ct);
        return result?.Trim();
    }

    private static async Task<string?> RunGitAsync(
        string workingDirectory, string arguments, CancellationToken ct = default)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "git",
                    Arguments              = arguments,
                    WorkingDirectory       = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };

            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync(ct);
            var error = await proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);

            return proc.ExitCode == 0 ? output : error;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }
}

public enum GitChangeKind
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Copied,
    Untracked,
    Conflict,
}

public enum GitFileArea
{
    Staged,
    Changes,
}

public sealed record GitStatusEntry(
    string        Path,
    GitChangeKind Status,
    GitFileArea   Area,
    string?       OldPath = null)
{
    public string FileName => System.IO.Path.GetFileName(Path);
    public string Directory => System.IO.Path.GetDirectoryName(Path)?.Replace('\\', '/') ?? string.Empty;

    public string StatusLabel => Status switch
    {
        GitChangeKind.Added     => "A",
        GitChangeKind.Modified  => "M",
        GitChangeKind.Deleted   => "D",
        GitChangeKind.Renamed   => "R",
        GitChangeKind.Copied    => "C",
        GitChangeKind.Untracked => "U",
        GitChangeKind.Conflict  => "!",
        _                       => "?",
    };

    public string StatusColor => Status switch
    {
        GitChangeKind.Added     => "#4EC94E",
        GitChangeKind.Modified  => "#E2C04B",
        GitChangeKind.Deleted   => "#F14C4C",
        GitChangeKind.Renamed   => "#569CD6",
        GitChangeKind.Copied    => "#569CD6",
        GitChangeKind.Untracked => "#73C991",
        GitChangeKind.Conflict  => "#E89B4C",
        _                       => "#AAAAAA",
    };
}
