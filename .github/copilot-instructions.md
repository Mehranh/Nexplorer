# GitHub Copilot Instructions — High-Performance Windows File Explorer

> This file defines the architecture, performance constraints, and engineering standards for this solution.
> Copilot MUST follow these rules in all generated code.

---

# Project Overview

**Project Name:** High-Performance File Explorer for Windows 11
**Target Framework:** .NET 8+
**Platform:** Windows 11 (x64)
**UI Stack:** WinUI 3 (preferred) or WPF (only if virtualization constraints require)
**Architecture:** Clean Architecture + MVVM
**Primary Goal:** Build a native-feeling, ultra-fast file explorer combining the power of:

* Directory Opus-style advanced file management
* Far Manager-style command execution & keyboard workflows

This is a performance-critical desktop application. UI thread blocking is unacceptable.

---

# 1. Architecture Rules

## 1.1 Layered Structure

```
src/
 ├── Explorer.App                (UI layer - WinUI/WPF)
 ├── Explorer.Application        (Use cases, orchestration)
 ├── Explorer.Domain             (Core models & abstractions)
 ├── Explorer.Infrastructure     (Filesystem, Shell, SQLite, OS interop)
 ├── Explorer.Terminal           (Command runner engine)
 ├── Explorer.Plugins            (Extension system)
 └── Explorer.Tests              (Unit + Integration + Benchmarks)
```

### Rules

* UI must not access filesystem directly.
* Domain must not reference UI.
* Infrastructure must implement abstractions defined in Domain.
* No circular dependencies.
* No static state unless absolutely required.

---

# 2. Performance & Memory Constraints (Critical)

This application must handle:

* 500k+ files in a directory
* Network shares with latency
* Rapid navigation changes
* Continuous file system updates

### Mandatory Engineering Standards

* No blocking calls on UI thread.
* Use async/await properly.
* Use IAsyncEnumerable for streaming file enumeration.
* Support CancellationToken everywhere.
* Avoid large object heap allocations.
* Use ArrayPool where beneficial.
* Prefer ValueTask for high-frequency async operations.
* Avoid unnecessary LINQ allocations in hot paths.
* Do NOT materialize full collections for large folders.

---

# 3. Core Explorer Capabilities

## 3.1 Navigation

* Multi-tab support
* Dual-pane toggle
* Independent history per tab
* Back / Forward / Up navigation
* Breadcrumb path navigation
* Direct address bar input

### History System

* Immutable history entries
* Snapshot-based navigation
* Avoid re-enumeration if cached

---

## 3.2 High-Performance File Enumeration

### Requirements

* Use low-level Win32 APIs when needed:

  * FindFirstFileEx
  * FILE_FLAG_BACKUP_SEMANTICS
* Async streaming results
* Incremental UI updates

### Enumeration Phases

1. Phase 1: Minimal metadata (name, size, timestamps, attributes)
2. Phase 2: Lazy-load heavy metadata
3. Phase 3: Deferred icon extraction (background queue)

### Virtualization

* Must use UI virtualization (recycling mode)
* Never load all items into memory for large directories
* Sorting must operate on lightweight models

### Cancellation

* Immediate cancellation on navigation change
* Cascade cancellation to:

  * Enumeration
  * Sorting
  * Metadata loading
  * Icon extraction

---

# 4. File Operation Engine

## 4.1 Copy / Move / Delete

Must support:

* Operation queue
* Pause / Resume
* Cancellation
* Long path handling
* Recycle Bin integration
* UAC elevation flow
* Conflict resolution (overwrite/skip/rename)

### Safety

* Atomic moves when possible
* Progress reporting
* Operation throttling

---

# 5. Search Engine

* In-folder search (wildcards)
* Optional regex support
* Recursive search with streaming results
* Incremental search results
* Cancelable search operations

---

# 6. Command Runner Engine

## 6.1 Embedded Terminal

* Supports:

  * PowerShell
  * cmd
* Runs in active pane directory
* Uses Windows ConPTY
* Async streaming output
* ANSI color support
* Per-pane execution context

---

## 6.2 Persistent Command History (Primary Feature)

Must store:

* Command text
* Working directory
* Shell type
* Timestamp
* Exit code
* Execution duration

Storage:

* SQLite (preferred)
* Indexed for fast search

### Access Features

* Fuzzy search
* Keyboard cycling (up/down)
* Pin/favorite commands
* Filter by:

  * Directory
  * Exit code
  * Date range

History must survive crash and restart.

---

## 6.3 Command Templates

User-defined templates with variables:

Examples:

```
git commit -m "${Input:Message}"
robocopy "${CurrentPath}" "${Input:Destination}" /E
```

Supported variables:

* ${CurrentPath}
* ${Selected}
* ${SelectedNames}
* ${Input:VariableName}

---

# 7. Advanced Features (Directory Opus Level)

## 7.1 Customizable Columns

* Configurable column visibility
* Natural sort
* Folder-first sorting
* Custom comparers

## 7.2 Preview Pane

* Non-blocking preview
* Plugin-based preview handlers
* Background loading
* Memory cap on preview cache

## 7.3 Batch Rename

* Regex support
* Counters
* Search/replace
* Case transformations
* Rename preview before commit

---

# 8. Windows Integration

## 8.1 Shell Integration

* Native context menu invocation
* File association resolution
* .lnk shortcut resolution
* File property dialog support

## 8.2 File System Monitoring

* Prefer USN Journal when possible
* Fallback to FileSystemWatcher
* Debounce rapid events
* Coalesce burst changes

---

# 9. UX Philosophy

* Keyboard-first design
* Rebindable shortcuts
* Instant command focus
* Command palette support
* Smooth scrolling at 60 FPS
* Clear status indicators:

  * Item count
  * Selection size
  * Current operation

---

# 10. Reliability & Diagnostics

## 10.1 Session Recovery

Persist:

* Open tabs
* Pane layout
* Last directories
* Window geometry

## 10.2 Logging

* Structured logging (Serilog)
* Rolling file logs
* Optional ETW provider

Metrics to capture:

* Enumeration duration
* Sorting time
* Icon load latency
* File operation throughput

---

# 11. Extensibility

Plugin system must allow:

* Custom commands
* Column providers
* Preview handlers
* File operation extensions

Implementation:

* Interface contracts
* Assembly scanning
* Isolated load context

---

# 12. Concurrency Model

* No fire-and-forget tasks
* Use Channels for streaming data
* Use SemaphoreSlim for throttling
* Centralized background task scheduler
* Strict cancellation discipline

---

# 13. Testing Strategy

Must include:

* File system abstraction
* Unit tests for domain logic
* Integration tests using temp directories
* Stress tests for 100k+ files
* BenchmarkDotNet performance benchmarks

---

# 14. Performance Budget

| Operation                    | Target            |
| ---------------------------- | ----------------- |
| Initial folder visible items | < 200ms perceived |
| Smooth scrolling             | 60 FPS            |
| Command startup              | < 150ms           |
| Search incremental batch     | < 100ms           |

---

# MVP Definition

Must include:

* Single-window explorer
* Dual-pane toggle
* Virtualized details view
* Async streaming enumeration
* Copy/move/rename/delete
* Embedded command runner
* Persistent searchable history (SQLite-backed)
* Stable tab navigation

---

# Acceptance Criteria

* Enumerating 50k+ files does not freeze UI.
* UI thread remains responsive at all times.
* Cancelling navigation stops all background work.
* Command history persists across restarts.
* Memory usage remains stable during large folder browsing.
