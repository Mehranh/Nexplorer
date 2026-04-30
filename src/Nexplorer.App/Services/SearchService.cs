using System.IO;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace Nexplorer.App.Services;

/// <summary>Search filter criteria.</summary>
public sealed record SearchCriteria
{
    public string  Query          { get; init; } = string.Empty;
    public string  RootPath       { get; init; } = string.Empty;
    public bool    Recursive      { get; init; } = true;
    public bool    UseRegex       { get; init; }

    // Size filters (bytes)
    public long?   MinSize        { get; init; }
    public long?   MaxSize        { get; init; }

    // Date filters
    public DateTime? ModifiedAfter  { get; init; }
    public DateTime? ModifiedBefore { get; init; }
}

/// <summary>
/// High-throughput parallel filesystem search.
///
/// Algorithm:
///   1. A work channel holds directories that still need traversal (seeded with the root).
///   2. N worker tasks (capped at <see cref="Environment.ProcessorCount"/>, max 8) read
///      directories from the channel and enumerate them non-recursively using
///      <see cref="FileSystemEnumerable{T}"/> — the lowest-allocation public API in
///      .NET, backed by Win32 FindFirstFileEx. The transform delegate runs against a
///      <c>ref</c>-only <see cref="FileSystemEntry"/>, so size/date/name filters are
///      applied without materialising <see cref="FileInfo"/> objects.
///   3. Subdirectories discovered during enumeration are pushed back onto the work
///      channel; matching files are written to a bounded results channel which provides
///      natural backpressure if the UI consumer is slow.
///   4. A pending-work counter completes the work channel exactly when in-flight work
///      drops to zero, letting all workers exit cleanly.
///
/// Properties:
///   - Scales near-linearly with CPU count on SSDs and large trees.
///   - Cancellation is honoured at every <c>await</c> and at the start of each entry.
///   - No <see cref="FileSystemInfo"/> allocations during traversal; <see cref="FileInfo"/>
///     instances are constructed only for surviving matches.
///   - Junctions / reparse points are skipped to prevent infinite recursion.
/// </summary>
public static class SearchService
{
    private readonly record struct Hit(string FullPath, bool IsDir);

    /// <summary>Returns matching paths asynchronously.</summary>
    public static async IAsyncEnumerable<FileInfo> SearchAsync(
        SearchCriteria criteria,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (criteria is null) yield break;

        var root = criteria.RootPath;
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) yield break;

        // ---- Compile regex once (if any) ---------------------------------------------------
        Regex? regex = null;
        if (criteria.UseRegex)
        {
            try
            {
                regex = new Regex(
                    criteria.Query ?? string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(2));
            }
            catch
            {
                yield break;
            }
        }

        // ---- Hoist filter state to locals (avoids Nullable<T> branches in hot loop) --------
        var query     = criteria.Query ?? string.Empty;
        var hasQuery  = query.Length > 0;
        var isGlob    = !criteria.UseRegex && hasQuery &&
                        (query.Contains('*') || query.Contains('?'));
        var hasMin    = criteria.MinSize.HasValue;
        var hasMax    = criteria.MaxSize.HasValue;
        var minVal    = criteria.MinSize.GetValueOrDefault();
        var maxVal    = criteria.MaxSize.GetValueOrDefault();
        var hasAfter  = criteria.ModifiedAfter.HasValue;
        var hasBefore = criteria.ModifiedBefore.HasValue;
        var afterUtc  = criteria.ModifiedAfter.GetValueOrDefault().ToUniversalTime();
        var beforeUtc = criteria.ModifiedBefore.GetValueOrDefault().ToUniversalTime();
        var recursive = criteria.Recursive;

        var enumOpts = new EnumerationOptions
        {
            RecurseSubdirectories    = false,
            IgnoreInaccessible       = true,
            AttributesToSkip         = FileAttributes.System | FileAttributes.ReparsePoint,
            ReturnSpecialDirectories = false,
            BufferSize               = 4096,
        };

        // Predicate runs on a ref FileSystemEntry — no allocation, no FileInfo materialisation.
        // Directories are always included so they can be enqueued for traversal; their name
        // match is checked separately at consumption time.
        var localRegex = regex;
        FileSystemEnumerable<Hit>.FindPredicate filePredicate = (ref FileSystemEntry e) =>
        {
            if (e.IsDirectory) return true;

            // Cheapest filters first (avoid touching DateTime when possible).
            if (hasMin && e.Length < minVal) return false;
            if (hasMax && e.Length > maxVal) return false;

            if (hasAfter || hasBefore)
            {
                var lw = e.LastWriteTimeUtc.UtcDateTime;
                if (hasAfter  && lw < afterUtc)  return false;
                if (hasBefore && lw > beforeUtc) return false;
            }

            if (localRegex is not null) return localRegex.IsMatch(e.FileName);
            if (!hasQuery)              return true;
            if (isGlob)                 return FileSystemName.MatchesSimpleExpression(query, e.FileName, ignoreCase: true);
            return e.FileName.Contains(query, StringComparison.OrdinalIgnoreCase);
        };

        FileSystemEnumerable<Hit>.FindTransform transform = static (ref FileSystemEntry e)
            => new Hit(e.ToFullPath(), e.IsDirectory);

        bool MatchDirName(ReadOnlySpan<char> name)
        {
            if (localRegex is not null) return localRegex.IsMatch(name);
            if (!hasQuery)              return true;
            if (isGlob)                 return FileSystemName.MatchesSimpleExpression(query, name, ignoreCase: true);
            return name.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        // ---- Channels ----------------------------------------------------------------------
        var work = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        });

        // Bounded so a fast traversal cannot OOM if the UI consumer falls behind.
        var results = Channel.CreateBounded<FileInfo>(new BoundedChannelOptions(4096)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode     = BoundedChannelFullMode.Wait,
        });

        // pending = (items in `work` channel) + (items currently being processed by a worker).
        // When this hits 0 after a worker finishes a directory, no more work can ever arrive,
        // so we complete the channel and all readers exit.
        var pending = 1;
        work.Writer.TryWrite(root);

        var workerCount = Math.Clamp(Environment.ProcessorCount, 2, 8);
        var workers = new Task[workerCount];

        for (var i = 0; i < workerCount; i++)
        {
            workers[i] = Task.Run(async () =>
            {
                try
                {
                    await foreach (var dir in work.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                    {
                        try
                        {
                            var enumerable = new FileSystemEnumerable<Hit>(dir, transform, enumOpts)
                            {
                                ShouldIncludePredicate = filePredicate,
                            };

                            foreach (var hit in enumerable)
                            {
                                if (ct.IsCancellationRequested) return;

                                if (hit.IsDir)
                                {
                                    if (recursive)
                                    {
                                        Interlocked.Increment(ref pending);
                                        if (!work.Writer.TryWrite(hit.FullPath))
                                            Interlocked.Decrement(ref pending);
                                    }

                                    // Directories are not pre-filtered by name — apply now.
                                    if (!MatchDirName(Path.GetFileName(hit.FullPath.AsSpan())))
                                        continue;
                                }

                                await results.Writer
                                             .WriteAsync(new FileInfo(hit.FullPath), ct)
                                             .ConfigureAwait(false);
                            }
                        }
                        catch (UnauthorizedAccessException) { /* skip locked dir */ }
                        catch (DirectoryNotFoundException)  { /* removed mid-walk */ }
                        catch (IOException)                 { /* network glitch / sharing */ }
                        finally
                        {
                            if (Interlocked.Decrement(ref pending) == 0)
                                work.Writer.TryComplete();
                        }
                    }
                }
                catch (OperationCanceledException) { /* expected on cancel */ }
            }, CancellationToken.None);
        }

        // When all workers finish (naturally, on cancel, or on fault) close the result channel.
        var pump = Task.Run(async () =>
        {
            try { await Task.WhenAll(workers).ConfigureAwait(false); }
            catch { /* swallow per-worker faults */ }
            finally
            {
                work.Writer.TryComplete();
                results.Writer.TryComplete();
            }
        }, CancellationToken.None);

        try
        {
            await foreach (var fi in results.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                yield return fi;
        }
        finally
        {
            // Ensure background work observes cancellation and completes before we leave.
            try { await pump.ConfigureAwait(false); }
            catch { /* swallow */ }
        }
    }
}
