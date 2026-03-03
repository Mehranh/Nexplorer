using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Nexplorer.App.Services;

/// <summary>
/// Quickly detects the current Git branch for a given directory.
/// Uses git rev-parse and git status for minimal overhead.
/// </summary>
public static class GitBranchService
{
    /// <summary>
    /// Returns the active Git branch name, or null if not in a Git repo.
    /// </summary>
    public static string? GetCurrentBranch(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        // Fast path: read HEAD file directly
        var headPath = FindGitHead(directory);
        if (headPath is not null)
        {
            try
            {
                var headContent = File.ReadAllText(headPath).Trim();
                if (headContent.StartsWith("ref: refs/heads/", StringComparison.Ordinal))
                    return headContent["ref: refs/heads/".Length..];
                if (headContent.Length >= 7)
                    return headContent[..7]; // detached HEAD
            }
            catch { }
        }

        // Fallback: run git command
        return GetBranchFromGit(directory);
    }

    /// <summary>
    /// Gets Git status summary: branch, dirty count, ahead/behind.
    /// </summary>
    public static GitBranchInfo? GetBranchInfo(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        var branch = GetCurrentBranch(directory);
        if (branch is null) return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "status --porcelain -b",
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return new GitBranchInfo(branch, 0, 0, 0);

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(2000);

            int dirty = 0, staged = 0;
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines.Skip(1))
            {
                if (line.Length < 2) continue;
                if (line[0] != ' ' && line[0] != '?') staged++;
                if (line[1] != ' ') dirty++;
            }

            // Parse ahead/behind from first line
            int ahead = 0, behind = 0;
            var firstLine = lines.Length > 0 ? lines[0] : string.Empty;
            var aheadMatch = System.Text.RegularExpressions.Regex.Match(firstLine, @"\[ahead (\d+)");
            var behindMatch = System.Text.RegularExpressions.Regex.Match(firstLine, @"behind (\d+)");
            if (aheadMatch.Success) ahead = int.Parse(aheadMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            if (behindMatch.Success) behind = int.Parse(behindMatch.Groups[1].Value, CultureInfo.InvariantCulture);

            return new GitBranchInfo(branch, dirty, ahead, behind, staged);
        }
        catch
        {
            return new GitBranchInfo(branch, 0, 0, 0);
        }
    }

    private static string? FindGitHead(string directory)
    {
        var dir = directory;
        while (!string.IsNullOrEmpty(dir))
        {
            var gitDir = Path.Combine(dir, ".git");
            if (Directory.Exists(gitDir))
            {
                var headFile = Path.Combine(gitDir, "HEAD");
                if (File.Exists(headFile)) return headFile;
            }
            else if (File.Exists(gitDir))
            {
                // Worktree: .git file points to actual git dir
                try
                {
                    var content = File.ReadAllText(gitDir).Trim();
                    if (content.StartsWith("gitdir: ", StringComparison.Ordinal))
                    {
                        var actualGitDir = content["gitdir: ".Length..];
                        if (!Path.IsPathRooted(actualGitDir))
                            actualGitDir = Path.Combine(dir, actualGitDir);
                        var headFile = Path.Combine(actualGitDir, "HEAD");
                        if (File.Exists(headFile)) return headFile;
                    }
                }
                catch { }
            }
            var parent = Directory.GetParent(dir)?.FullName;
            if (parent == dir) break;
            dir = parent;
        }
        return null;
    }

    private static string? GetBranchFromGit(string directory)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --abbrev-ref HEAD",
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc is null) return null;

            var branch = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(2000);
            return proc.ExitCode == 0 && !string.IsNullOrEmpty(branch) ? branch : null;
        }
        catch { return null; }
    }
}

public sealed record GitBranchInfo(
    string Branch,
    int    DirtyCount,
    int    Ahead,
    int    Behind,
    int    StagedCount = 0)
{
    public bool IsDirty => DirtyCount > 0 || StagedCount > 0;

    public string FormatPrompt()
    {
        var parts = new List<string> { Branch };
        if (StagedCount > 0) parts.Add($"+{StagedCount}");
        if (DirtyCount > 0)  parts.Add($"~{DirtyCount}");
        if (Ahead > 0)       parts.Add($"↑{Ahead}");
        if (Behind > 0)      parts.Add($"↓{Behind}");
        return string.Join(" ", parts);
    }
}
