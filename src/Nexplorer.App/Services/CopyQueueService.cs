using System.Collections.Concurrent;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexplorer.App.Services;

public enum ConflictResolution { Skip, Replace, Rename, Ask }

public enum CopyJobStatus { Queued, Running, Paused, Completed, Failed, Cancelled }

/// <summary>A single copy/move operation in the queue.</summary>
public sealed partial class CopyJob : ObservableObject
{
    public string             Id          { get; } = Guid.NewGuid().ToString("N");
    public IReadOnlyList<string> Sources  { get; init; } = Array.Empty<string>();
    public string             Destination { get; init; } = string.Empty;
    public bool               IsMove      { get; init; }

    [ObservableProperty] private CopyJobStatus _status      = CopyJobStatus.Queued;
    [ObservableProperty] private string        _currentFile = string.Empty;
    [ObservableProperty] private long          _totalBytes;
    [ObservableProperty] private long          _copiedBytes;
    [ObservableProperty] private double        _progressPercent;
    [ObservableProperty] private string        _statusText  = "Queued";

    public CancellationTokenSource Cts { get; } = new();

    private readonly ManualResetEventSlim _pauseGate = new(true);

    public void Pause()  { _pauseGate.Reset(); Status = CopyJobStatus.Paused;  StatusText = "Paused"; }
    public void Resume() { _pauseGate.Set();   Status = CopyJobStatus.Running; }

    /// <summary>Blocks if paused. Call from the worker thread.</summary>
    public void WaitIfPaused() => _pauseGate.Wait(Cts.Token);
}

/// <summary>Global copy/move queue with concurrency of 1.</summary>
public sealed class CopyQueueService
{
    private static readonly Lazy<CopyQueueService> _instance = new(() => new CopyQueueService());
    public static CopyQueueService Instance => _instance.Value;

    public System.Collections.ObjectModel.ObservableCollection<CopyJob> Jobs { get; } = new();

    public ConflictResolution DefaultConflictResolution { get; set; } = ConflictResolution.Rename;

    /// <summary>Raised when a conflict is detected and DefaultConflictResolution is Ask.
    /// The handler must set Result on the args.</summary>
    public event EventHandler<ConflictEventArgs>? ConflictDetected;

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>Enqueues a copy or move job and starts processing.</summary>
    public CopyJob Enqueue(IEnumerable<string> sources, string dest, bool isMove)
    {
        var job = new CopyJob
        {
            Sources     = sources.ToList().AsReadOnly(),
            Destination = dest,
            IsMove      = isMove,
        };
        Jobs.Add(job);
        _ = ProcessJobAsync(job);
        return job;
    }

    private async Task ProcessJobAsync(CopyJob job)
    {
        await _semaphore.WaitAsync(job.Cts.Token);
        try
        {
            job.Status     = CopyJobStatus.Running;
            job.StatusText = "Calculating…";

            // Phase 1: calculate total size
            long total = 0;
            foreach (var src in job.Sources)
            {
                if (Directory.Exists(src))
                    total += DirSize(src);
                else if (File.Exists(src))
                    total += new FileInfo(src).Length;
            }
            job.TotalBytes = total;

            // Phase 2: process each source
            foreach (var src in job.Sources)
            {
                job.Cts.Token.ThrowIfCancellationRequested();
                job.WaitIfPaused();

                if (Directory.Exists(src))
                    await ProcessDirectoryAsync(src,
                        Path.Combine(job.Destination, Path.GetFileName(src)), job);
                else if (File.Exists(src))
                    await ProcessFileAsync(src,
                        ResolveConflict(Path.Combine(job.Destination, Path.GetFileName(src)), job),
                        job);
            }

            job.Status      = CopyJobStatus.Completed;
            job.StatusText  = job.IsMove ? "Move complete" : "Copy complete";
            job.ProgressPercent = 100;
        }
        catch (OperationCanceledException)
        {
            job.Status     = CopyJobStatus.Cancelled;
            job.StatusText = "Cancelled";
        }
        catch (Exception ex)
        {
            job.Status     = CopyJobStatus.Failed;
            job.StatusText = ex.Message;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ProcessDirectoryAsync(string srcDir, string destDir, CopyJob job)
    {
        Directory.CreateDirectory(destDir);
        foreach (var f in Directory.GetFiles(srcDir))
        {
            job.Cts.Token.ThrowIfCancellationRequested();
            job.WaitIfPaused();
            await ProcessFileAsync(f,
                ResolveConflict(Path.Combine(destDir, Path.GetFileName(f)), job), job);
        }
        foreach (var d in Directory.GetDirectories(srcDir))
        {
            await ProcessDirectoryAsync(d, Path.Combine(destDir, Path.GetFileName(d)), job);
        }

        // If move, remove empty source dir
        if (job.IsMove)
        {
            try { Directory.Delete(srcDir, recursive: false); } catch { }
        }
    }

    private async Task ProcessFileAsync(string src, string dest, CopyJob job)
    {
        if (dest == "__SKIP__") return;

        job.CurrentFile = Path.GetFileName(src);
        job.StatusText  = (job.IsMove ? "Moving " : "Copying ") + job.CurrentFile;

        const int bufferSize = 81920;
        var buffer = new byte[bufferSize];

        await using var sourceStream = new FileStream(src, FileMode.Open, FileAccess.Read,
            FileShare.Read, bufferSize, useAsync: true);
        await using var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write,
            FileShare.None, bufferSize, useAsync: true);

        int bytesRead;
        while ((bytesRead = await sourceStream.ReadAsync(buffer, job.Cts.Token)) > 0)
        {
            job.WaitIfPaused();
            await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), job.Cts.Token);

            job.CopiedBytes    += bytesRead;
            job.ProgressPercent = job.TotalBytes > 0
                ? Math.Round(100.0 * job.CopiedBytes / job.TotalBytes, 1) : 0;
        }

        // If move, delete original after full copy
        if (job.IsMove)
        {
            try { File.Delete(src); } catch { }
        }
    }

    private string ResolveConflict(string destPath, CopyJob job)
    {
        if (!File.Exists(destPath) && !Directory.Exists(destPath))
            return destPath;

        var resolution = DefaultConflictResolution;
        if (resolution == ConflictResolution.Ask)
        {
            var args = new ConflictEventArgs(destPath);
            ConflictDetected?.Invoke(this, args);
            resolution = args.Result;
        }

        return resolution switch
        {
            ConflictResolution.Skip    => "__SKIP__",
            ConflictResolution.Replace => destPath,
            _                          => GetUniquePath(destPath),
        };
    }

    private static string GetUniquePath(string path)
    {
        var dir     = Path.GetDirectoryName(path)!;
        var name    = Path.GetFileNameWithoutExtension(path);
        var ext     = Path.GetExtension(path);
        int n       = 2;
        string result;
        do { result = Path.Combine(dir, $"{name} ({n++}){ext}"); }
        while (File.Exists(result) || Directory.Exists(result));
        return result;
    }

    private static long DirSize(string path)
    {
        long size = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", new EnumerationOptions
                     { RecurseSubdirectories = true, IgnoreInaccessible = true }))
            {
                try { size += new FileInfo(f).Length; } catch { }
            }
        }
        catch { }
        return size;
    }
}

public sealed class ConflictEventArgs : EventArgs
{
    public string Path   { get; }
    public ConflictResolution Result { get; set; } = ConflictResolution.Rename;
    public ConflictEventArgs(string path) => Path = path;
}
