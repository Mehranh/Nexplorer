---
name: perf-review
description: 'Review code on hot paths (file enumeration, sorting, virtualization, icon loading, search, terminal streaming) against the Nexplorer performance budget defined in copilot-instructions.md. Use when the user asks to "review performance", "check for UI thread blocking", "audit allocations", "find perf regressions", "look at hot paths", or before merging changes that touch DirectoryEnumerator, FileItemViewModel, SearchService, terminal output streaming, or any IAsyncEnumerable producer/consumer. Flags blocking I/O on UI thread, missing CancellationToken plumbing, LOH allocations, eager LINQ materialization, and missing virtualization.'
argument-hint: '<file-path or feature area to review>'
---

# Performance Review

Nexplorer must stay responsive while handling 500k+ file directories, network shares, rapid navigation, and continuous filesystem updates. Every change to a hot path needs an explicit perf check.

## When to Use
- Reviewing a PR or change that touches file enumeration, virtualization, sorting, search, terminal streaming, icon extraction, preview pane, or any `IAsyncEnumerable<T>` pipeline.
- Investigating UI freezes, jank, or memory growth.
- Before merging changes to `DirectoryEnumerator`, `SearchService`, `CopyQueueService`, `ShellIconService`, `CommandHistoryStore`, or anything in `Nexplorer.Core`.

## When NOT to Use
- Cold-path code (settings dialogs, one-shot init, error paths).
- Style-only changes.

## Performance Budget (from copilot-instructions.md)

| Operation | Target |
|-----------|--------|
| Initial folder visible items | < 200 ms perceived |
| Smooth scrolling | 60 FPS |
| Command startup | < 150 ms |
| Search incremental batch | < 100 ms |

## Review Checklist

Walk the code under review and check each item. Flag any violation with file + line.

### Concurrency & UI Thread
- [ ] **No blocking I/O on UI thread** — no `.Result`, `.Wait()`, `Task.Run(...).Wait()`, synchronous `File.*` calls from view-model command handlers or XAML code-behind.
- [ ] **`async` all the way down** — no `async void` except event handlers; use `await` not `.GetAwaiter().GetResult()`.
- [ ] **`CancellationToken` flows end-to-end** — every public async method accepts and forwards it; navigation/search/enumeration cancellation cascades to icons, sorting, metadata.
- [ ] **No fire-and-forget tasks** — every `Task` is awaited or tracked by a centralized scheduler.
- [ ] **`ConfigureAwait(false)`** on library code (`Nexplorer.Core`, services that don't touch UI).

### Allocations & Hot-Path Memory
- [ ] **No materialization of full collections** for large folders — use `IAsyncEnumerable<T>`, never `.ToList()` / `.ToArray()` on enumeration results.
- [ ] **No LINQ chains in hot loops** — `Where().Select().OrderBy()` allocates iterators per call. Manual loops or precomputed arrays in inner loops.
- [ ] **Avoid LOH (≥ 85 KB) allocations** — no large `byte[]` or `string` building without `ArrayPool<T>` / `StringBuilder` reuse.
- [ ] **`ValueTask` for high-frequency async** — per-item awaits in enumeration/streaming paths should use `ValueTask` over `Task`.
- [ ] **`ArrayPool<T>.Shared`** for transient buffers (e.g., file IO buffers, ConPTY reads).
- [ ] **No string concatenation in loops** — `StringBuilder` or `string.Create`.

### Streaming & Virtualization
- [ ] **`IAsyncEnumerable<T>`** for any producer of >100 items.
- [ ] **UI uses recycling virtualization** — `VirtualizingStackPanel.IsVirtualizing="True"` and `VirtualizationMode="Recycling"` on `ItemsControl` / `ListView` for file lists.
- [ ] **Sorting operates on lightweight models** — `FileItem` (record), not the heavy `FileItemViewModel`.
- [ ] **Incremental UI updates** — batch additions via `RangeObservableCollection` rather than per-item `Add`.

### Filesystem Specifics
- [ ] **`FindFirstFileEx` / `FILE_FLAG_BACKUP_SEMANTICS`** for large directories rather than `Directory.GetFiles`.
- [ ] **Lazy metadata** — heavy properties (icons, thumbnails, full attributes) are deferred to background queues, not loaded during the initial enumeration.
- [ ] **Long path support** — paths use `\\?\` prefix where applicable.
- [ ] **USN Journal preferred over `FileSystemWatcher`** for monitoring; rapid events are debounced and coalesced.

### Cancellation Discipline
- [ ] Navigation change cancels: enumeration, sort, metadata, icon, preview.
- [ ] Tokens are linked, not swallowed (`CancellationTokenSource.CreateLinkedTokenSource`).
- [ ] No `catch (OperationCanceledException) { /* swallow */ }` on the producer side without intent.

### Throttling
- [ ] `SemaphoreSlim` or bounded `Channel<T>` throttling on parallel I/O (icon batch loads, parallel copies).
- [ ] No unbounded `Parallel.ForEach` over filesystem operations.

## Procedure

1. **Read the changed files** end-to-end before judging — partial reads miss context.
2. **Walk the checklist** in order; record violations as `file.cs:line — issue — suggested fix`.
3. **Cross-check against the performance budget** — if you can reason about the cost, estimate it (e.g., "allocates one `List<string>` per directory entry, ~500k allocs for a large folder").
4. **Suggest concrete fixes**, not vague "use async" comments. Example: "Replace `.ToList()` on line 84 with `await foreach` and stream into `RangeObservableCollection.AddRange`."
5. **If unsure, recommend a benchmark** — BenchmarkDotNet harness lives in `tests/Nexplorer.Tests/` per project rules.

## Output Format

Structure findings as:

```
### Findings

**Critical** (blocks UI or unbounded growth):
- DirectoryEnumerator.cs:42 — `await foreach` materialized into `List<FileItem>` before yielding; defeats streaming. Yield directly.

**High** (allocations / cancellation):
- ...

**Medium** (style / minor allocations):
- ...

### Verified Good
- Cancellation token propagated correctly through SearchService.cs.
```

## Common Pitfalls in Reviews

- **Approving without reading the consumer** — a streaming producer is useless if the consumer calls `.ToListAsync()`.
- **Ignoring icon/thumbnail paths** — icon extraction is the single biggest source of UI jank in file explorers.
- **Missing the `ConfigureAwait(false)` opportunity in `Nexplorer.Core`** — Core has no UI dependency and should never sync back to a context.
- **Not measuring** — if a change *might* be slow, ask for a benchmark before merging.
