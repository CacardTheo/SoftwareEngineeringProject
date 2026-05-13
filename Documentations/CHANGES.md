# EasySave — Technical Documentation

This document explains the architecture and implementation of the entire application, file by file, layer by layer. It covers every design decision, threading primitive, and data flow in the current codebase.

---

## Table of contents

1. [Architecture overview](#1-architecture-overview)
2. [Entry point](#2-entry-point)
3. [Data models](#3-data-models)
4. [Persistence layer](#4-persistence-layer)
5. [Language system](#5-language-system)
6. [Backup execution pipeline](#6-backup-execution-pipeline)
7. [Backup strategies](#7-backup-strategies)
8. [Cross-job synchronization](#8-cross-job-synchronization)
9. [ViewModels layer](#9-viewmodels-layer)
10. [Views layer](#10-views-layer)
11. [Threading model](#11-threading-model)
12. [Thread safety — primitives and placement](#12-thread-safety--primitives-and-placement)
13. [Bug fixes applied in this session](#13-bug-fixes-applied-in-this-session)
14. [Code deduplication applied in this session](#14-code-deduplication-applied-in-this-session)

---

## 1. Architecture overview

EasySave follows the **MVVM pattern** (Model – View – ViewModel), which is the standard for Avalonia UI applications.

```
┌────────────────────────────────────────────────────────────┐
│  Views  (XAML + code-behind)                               │
│  MainWindow / AddJobWindow / SettingsWindow                │
│  → Only know about their ViewModel, never about services   │
└───────────────────┬────────────────────────────────────────┘
                    │ DataContext binding
┌───────────────────▼────────────────────────────────────────┐
│  ViewModels                                                │
│  MainViewModel / BackupJobViewModel                        │
│  AddJobViewModel / SettingsViewModel                       │
│  → Expose properties and commands that Views bind to       │
│  → Orchestrate services, never touch the UI directly       │
└───────┬──────────────────┬─────────────────────────────────┘
        │                  │
┌───────▼───────┐  ┌───────▼──────────────────────────────────┐
│  Services     │  │  Backup pipeline                          │
│  ConfigManager│  │  BackupProcessor                          │
│  SettingsManager  │  ↳ FullBackupStrategy                    │
│  StateManager │  │  ↳ DifferentialBackupStrategy             │
│  LanguageManager  │  ↳ BackupStrategyBase (shared logic)     │
│  BusinessSoftwareMonitor  BackupSyncContext (thread sync)    │
│  CryptoSoftService        FileHelper (low-level I/O)         │
└───────────────┘  └──────────────────────────────────────────┘
```

The Views know only their ViewModel. The ViewModels know the services and the backup pipeline. Nothing flows the other direction.

---

## 2. Entry point

**`Program.cs`**

The application checks `args` at startup:

- If command-line arguments are present (e.g. `EasySaveWpf.exe 1-3`), it runs the specified jobs headlessly and exits without launching a GUI.
- If no arguments are given, it starts the Avalonia application normally and opens `MainWindow`.

This dual-mode entry point allows the app to be driven from a script or task scheduler without any UI.

---

## 3. Data models

### `Models/Data/BackupJob.cs`

The persisted unit of work. Contains four nullable properties:

| Property | Type | Description |
|---|---|---|
| `Name` | `string?` | Unique identifier used to match state entries |
| `SourceDir` | `string?` | Source path — can be a file or a directory |
| `TargetDir` | `string?` | Destination directory |
| `Type` | `BackupType` | `Full` or `Differential` |

`Name` doubles as the primary key in both the config file and the state file — every lookup by job is a string comparison on this field.

### `Models/Data/AppSettings.cs`

All user-configurable settings, persisted to `settings.json`:

| Property | Type | Default | Purpose |
|---|---|---|---|
| `Language` | `string` | `"en"` | UI language code |
| `LogFormat` | `LogFormat` | `Json` | Format for the daily log |
| `StateFormat` | `LogFormat` | `Json` | Format for the state file |
| `BusinessSoftwareProcesses` | `List<string>` | empty | Process names that block backups |
| `EncryptedExtensions` | `List<string>` | empty | Extensions encrypted after copy |
| `EncryptionKey` | `string` | `""` | Key passed to CryptoSoft |
| `PrioritizedExtensions` | `List<string>` | empty | Extensions copied before all others |
| `LargeFileSizeThresholdKb` | `int` | `0` | Large-file concurrency limit (`0` = disabled) |

### `Models/Data/StateEntry.cs`

A snapshot of one job at a given moment, written to `state.json` after every significant event. Fields include `Name`, `State` (`BackupStatus`), `TotalFilesToCopy`, `NbFilesLeftToDo`, `TotalFilesSize`, `SizeRemaining`, `Progression` (0–100), `SourceFilePath`, `TargetFilePath`, and `LastRun`.

### `Models/Enums/BackupStatus.cs`

```
Inactive     → job has never run, or was blocked/reset
In_Progress  → copy is currently running
Ended        → last run completed successfully
Error        → last run encountered a fatal exception
```

### `Models/Enums/BackupType.cs`

```
Full          → copy every file in the source tree regardless of target state
Differential  → copy only files that are absent or newer in the target
```

---

## 4. Persistence layer

All three persistence classes use the same AppData folder (`%APPDATA%\EasySave\`). The folder path is resolved once via `FileHelper.GetAppDataFolder()`, which is called by each class constructor and cached statically.

### `Utils/FileHelper.cs`

Two responsibilities:

**1. AppData folder resolution (static, cached)**

```csharp
public static string GetAppDataFolder()
{
    if (_appDataFolder != null) return _appDataFolder;
    string folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EasySave");
    Directory.CreateDirectory(folder);  // idempotent — safe to call even if it exists
    _appDataFolder = folder;
    return folder;
}
```

The result is cached in a static field after the first call. All three managers (`ConfigManager`, `SettingsManager`, `StateManager`) call this in their constructor. `Directory.CreateDirectory` is idempotent and does not throw if the folder already exists.

**2. Chunked file copy**

```csharp
public static void CopyFile(string sourcePath, string targetPath,
    Action<string, string, long>? onBytesWritten = null)
{
    const int bufferSize = 81920; // 80 KB per chunk
    using FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read,
        FileShare.Read, bufferSize, FileOptions.SequentialScan);
    using FileStream target = new(targetPath, FileMode.Create, FileAccess.Write,
        FileShare.None, bufferSize, FileOptions.None);
    byte[] buffer = new byte[bufferSize];
    int bytesRead;
    while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
    {
        target.Write(buffer, 0, bytesRead);
        onBytesWritten?.Invoke(sourcePath, targetPath, bytesRead);
    }
}
```

Key choices:
- `FileShare.Read` on the source allows other processes to read the file during copy.
- `FileShare.None` on the target prevents concurrent writes to the same destination.
- `FileOptions.SequentialScan` tells the OS to pre-fetch data aggressively since we read the file linearly — this improves throughput.
- The 80 KB buffer strikes a balance: large enough to minimize syscall overhead, small enough that `onBytesWritten` is called frequently enough for smooth progress bars.
- `onBytesWritten` is optional (`?.Invoke`). If `null`, the copy runs without notifications — used in unit tests or CLI mode.

### `ViewModels/ConfigManager.cs`

Stores and loads the list of `BackupJob` objects as `backup_jobs.json`. Uses `System.Text.Json` with `WriteIndented = true`. All errors are caught and logged to the console — a read failure returns an empty list so the app still starts.

### `ViewModels/SettingsManager.cs`

Stores and loads `AppSettings` as `settings.json`. A load failure (corrupted file, missing file) returns a fresh `AppSettings` with defaults, so the user never faces an unhandled error on startup.

### `ViewModels/StateManager.cs`

More complex than the other two because it is called from multiple concurrent threads during a "Run All" operation.

**Thread safety:** All public methods that touch the file are protected by `private readonly object _stateLock`. `SaveState` and `LoadStates` are `private` — they are implementation details that must always be called inside the lock.

```csharp
public void UpdateJobState(StateEntry updatedEntry)
{
    lock (_stateLock)
    {
        List<StateEntry> allStates = LoadStates();  // read full file
        // find and replace, or append
        SaveState(allStates);                        // write full file
    }
}

public void ClearState()
{
    lock (_stateLock)
    {
        SaveState(new List<StateEntry>());
    }
}
```

Why a full read-modify-write instead of appending? The state file contains **all** jobs. When job A finishes, the file must still contain the current state of jobs B and C. So the full list must be loaded, the entry for job A replaced, and the full list written back. The lock ensures no two threads interleave their read and write, which would cause one job's update to silently overwrite another's.

Supports both JSON and XML output via the `_format` field set by `SetFormat(LogFormat)`.

---

## 5. Language system

**`ViewModels/LanguageManager.cs`**

A **singleton** that loads translation keys from JSON files in `Resources/Languages/`.

**Singleton pattern:**

```csharp
private static LanguageManager? _instance;
private static readonly object _lock = new();

public static LanguageManager GetInstance()
{
    if (_instance == null)
    {
        lock (_lock)
        {
            if (_instance == null)
                _instance = new LanguageManager();
        }
    }
    return _instance;
}
```

This is the classic **double-checked locking** pattern. The outer `null` check avoids acquiring the lock on every call (the common case). The inner `null` check inside the lock prevents two threads that both passed the outer check from each creating an instance.

**Indexer shorthand:**

```csharp
public string this[string key] => GetText(key);
```

This allows `LangMgr["gui_done"]` syntax in XAML bindings and ViewModel code instead of `LangMgr.GetText("gui_done")`.

**Language change notification:**

`SetLanguage` fires `PropertyChanged` with `"Item"` and `"Item[]"`. Avalonia bindings that use the indexer (`{Binding [gui_done], Source=...}`) listen for `"Item[]"` and automatically re-evaluate — the entire UI relabels itself without any explicit refresh call.

**`ViewModelBase.cs`** subscribes to `LanguageManager.Instance.PropertyChanged` in its constructor:

```csharp
LanguageManager.Instance.PropertyChanged += (s, e) => OnPropertyChanged(string.Empty);
```

`OnPropertyChanged(string.Empty)` notifies Avalonia that every property on the ViewModel may have changed, which triggers a full rebind of all labels in the bound View.

---

## 6. Backup execution pipeline

### `ViewModels/BusinessSoftwareMonitor.cs`

Checks whether any configured process is currently running by calling `Process.GetProcessesByName()`. Process names are normalized before the check: `.exe` extensions are stripped so that both `"word"` and `"word.exe"` match the same process.

### `ViewModels/CryptoSoftService.cs`

Wraps a call to `CryptoProcessor.EncryptFileInPlace` from the `CryptoSoftLib` library. Returns the elapsed time in milliseconds on success, or a negative error code on failure. Catching all exceptions and returning `-1` ensures that an encryption failure never crashes the backup — the copy succeeds, only the encryption step is flagged in the log.

### `ViewModels/BackupProcessor.cs`

The central orchestrator. Takes a `BackupJob` and a `BackupSyncContext` and runs the full backup lifecycle:

**1. Pre-flight check**

```csharp
if (IsBusinessSoftwareRunning(settings, out string detectedBeforeStart))
{
    LogBusinessSoftwareBlock(job, detectedBeforeStart, logService, "BlockedBeforeStart");
    RaiseProgress(job.Name, BackupStatus.Inactive, 0, blocked: true);
    return false;
}
```

If a business process is already running when the job starts, the job is blocked immediately and a log entry is written. The UI is notified with `blocked: true`.

**2. Strategy selection**

```csharp
IBackupStrategy strategy = job.Type switch
{
    BackupType.Full         => new FullBackupStrategy(context),
    BackupType.Differential => new DifferentialBackupStrategy(context),
    _ => throw new ArgumentException($"Unknown backup type: {job.Type}")
};
```

Each `Execute` call creates a fresh strategy instance. This is intentional: each strategy holds a `private readonly Lock _lock` that serializes `onFileCopied` and `logService.Save` calls from concurrent file threads within that job. A new instance per job means the locks are independent between jobs.

**3. File enumeration and size calculation**

```csharp
files = Directory.GetFiles(job.SourceDir, "*.*", SearchOption.AllDirectories);
totalSize = files.Sum(f => new FileInfo(f).Length);
```

`totalSize` is the denominator for all percentage calculations. If the source is a single file rather than a directory, `files` contains only that one path.

**4. Progress callbacks (local functions)**

Three local functions are defined inside `Execute` and passed to `strategy.Backup`:

- `OnBytesWritten(sourceFile, destFile, bytes)` — called after every 80 KB chunk. Uses `Interlocked.Add` to safely accumulate bytes from multiple concurrent file threads. Only fires a UI update if the integer percentage actually changed.
- `OnFileCopied(sourceFile, destFile, fileSize)` — called once per completed file. Increments `filesCopied` (under the strategy's `_lock`) and updates the state file.
- `CanContinue()` — called before each file copy. Returns `false` if a business process was detected since the job started, triggering a graceful stop.

**5. Exception handling**

```csharp
catch (InvalidOperationException ex) when (ex.Message == "BUSINESS_SOFTWARE_DETECTED")
{
    // graceful stop — log it, update state, return false
    return false;
}
catch (Exception ex)
{
    // fatal error — log it, set status to Error, re-throw
    RaiseProgress(job.Name, BackupStatus.Error, 0, errorMessage: ex.Message);
    throw;
}
```

The re-throw on fatal errors is deliberate: the caller (`RunJob`) is responsible for catching it and recording `results[i] = false`. The re-throw also allows the exception to propagate to any outer handler that might need to log it at a higher level.

**6. Progress event**

```csharp
public event Action<string, BackupStatus, int, string, bool, string>? ProgressChanged;
```

Parameters in order: `jobName`, `status`, `progression`, `currentFile`, `blocked`, `errorMessage`. The event is subscribed by `MainViewModel.OnJobProgressChanged`.

---

## 7. Backup strategies

### `Models/Strategies/IBackupStrategy.cs`

The interface contract:

```csharp
void Backup(
    BackupJob job,
    LogService logService,
    AppSettings settings,
    CryptoSoftService cryptoService,
    Action<string, string, long> onFileCopied,
    Func<bool> canCopyNextFile,
    Action<string, string, long>? onBytesWritten = null);
```

`canCopyNextFile` is a `Func<bool>` rather than a boolean flag so the strategy can re-evaluate it before each file — a process might start between two file copies.

### `Models/Strategies/BackupStrategyBase.cs`

Abstract base class that contains all shared logic. Concrete strategies inherit from it and only override `Backup`.

**`CopyGroup` — parallel file copy with barrier and semaphore**

```csharp
protected void CopyGroup(
    List<FileInfo> group,
    bool isPriorityGroup,
    ...,
    Func<FileInfo, string, bool> shouldCopy)
```

For each file in `group`, a `Thread` is created and started. All threads run in parallel. The method then calls `t.Join()` on each, waiting until every thread in the group has finished.

Inside each thread:

```
1. If cancelled → return early
2. If non-priority → WaitForAllPriorityFiles() (cross-job barrier)
3. If business software detected → set cancelled flag, return
4. Evaluate shouldCopy(file, targetPath) → skip if false (differential logic)
5. If large file → AcquireLargeFileSlot() (semaphore, max 1 concurrent large transfer)
6. CreateDirectory(targetDir) — idempotent, no lock needed
7. FileHelper.CopyFile(...)
8. ReleaseLargeFileSlot() — in finally, always released
9. TryEncrypt(targetPath, ...)
10. lock(_lock) { onFileCopied(...); logService.Save(...); }
```

The `finally` block always calls `NotifyPriorityFileDone()` for priority threads, even if the copy threw an exception. This prevents non-priority threads from blocking forever on `WaitForAllPriorityFiles()`.

```csharp
finally
{
    if (isPriorityGroup) _context.NotifyPriorityFileDone();
}
```

**`CopySingleFile` — when the source is a single file**

When `job.SourceDir` points to a file rather than a directory, both strategies short-circuit to `CopySingleFile`. This method lives in the base class:

```csharp
protected static void CopySingleFile(
    ...,
    Func<FileInfo, string, bool>? shouldCopy = null)
```

`shouldCopy` is optional. Full backup passes `null` (always copy). Differential backup passes a predicate that checks file existence and modification time.

**`TryEncrypt`**

```csharp
protected static long TryEncrypt(string targetPath, string extension,
    CryptoSoftService cryptoService, AppSettings settings)
{
    foreach (string ext in settings.EncryptedExtensions)
        if (ext.Equals(extension, StringComparison.OrdinalIgnoreCase))
            return cryptoService.Encrypt(targetPath, settings.EncryptionKey);
    return 0;
}
```

Returns the encryption time in milliseconds, or `0` if the extension is not in the list.

**`_lock` — `System.Threading.Lock`**

```csharp
private readonly Lock _lock = new();
```

`System.Threading.Lock` is a .NET 9 type optimized for mutual exclusion. The JIT generates more efficient code than for `object`-based locks. `_lock` is per-strategy-instance, which means each job has its own lock — parallel jobs never contend with each other on this lock.

### `Models/Strategies/FullBackupStrategy.cs`

Calls `CopyGroup` with `shouldCopy: (f, t) => true` — every file is always copied.

### `Models/Strategies/DifferentialBackupStrategy.cs`

Defines a local static function:

```csharp
static bool ShouldCopy(FileInfo f, string t) =>
    !File.Exists(t) || f.LastWriteTime > File.GetLastWriteTime(t);
```

A file is copied only if it does not exist at the target, or if the source version is newer. This predicate is passed to both `CopyGroup` calls and to `CopySingleFile` when the source is a single file.

---

## 8. Cross-job synchronization

**`Models/BackupSyncContext.cs`**

Created once per `RunJob` call and shared by all job threads in that batch. Enforces two constraints across all concurrent jobs.

### Constraint 1: Priority barrier

```csharp
private int _priorityFilesRemaining = 0;
private readonly ManualResetEventSlim _allPriorityDone = new(initialState: true);
```

Initialized to `true` (open/signaled). When a job registers priority files, the event is Reset (closed) and the counter is incremented. When a priority thread finishes, the counter is decremented. When it reaches zero, the event is Set (open) again.

```csharp
public void RegisterPriorityFiles(int count)
{
    if (count <= 0) return;          // no priority files → event stays open
    _allPriorityDone.Reset();        // close the gate
    Interlocked.Add(ref _priorityFilesRemaining, count);
}

public void NotifyPriorityFileDone()
{
    if (Interlocked.Decrement(ref _priorityFilesRemaining) == 0)
        _allPriorityDone.Set();      // open the gate when last priority file finishes
}

public void WaitForAllPriorityFiles() => _allPriorityDone.Wait();
```

**Why `ManualResetEventSlim` and not other primitives:**

| Primitive | Behavior | Problem |
|---|---|---|
| `AutoResetEvent` | Releases **one** waiter, then resets | With 50 non-priority threads waiting, each would need its own signal. Not a broadcast. |
| `Monitor.PulseAll` | Broadcasts, but requires a `lock` | A missed `PulseAll` (fires before any `Wait`) permanently blocks the thread. |
| `ManualResetEventSlim` | Stays open once `Set()` — all waiters unblock at once | Exactly the "gate" semantics needed. The Slim variant spin-waits briefly before falling back to a kernel wait, avoiding context-switch overhead for short waits. |

### Constraint 2: Large file semaphore

```csharp
private readonly SemaphoreSlim _largeFileSemaphore = new(1, 1);

public void AcquireLargeFileSlot() => _largeFileSemaphore.Wait();
public void ReleaseLargeFileSlot() => _largeFileSemaphore.Release();
```

At most one large file transfer is allowed at a time across all jobs. Small files (below `LargeFileSizeThresholdBytes`) never touch the semaphore.

**Why `SemaphoreSlim` and not `Mutex`:**

A `Mutex` requires the same thread that acquired it to release it. A `SemaphoreSlim` has no thread affinity — any thread can release it. More importantly, `SemaphoreSlim` is a pure managed object with no kernel handle until a thread actually blocks, making uncontended acquisitions cheaper. Changing to `SemaphoreSlim(2, 2)` later would allow two simultaneous large transfers without any other code change.

---

## 9. ViewModels layer

### `ViewModels/ViewModelBase.cs`

Base class for all ViewModels. Implements `INotifyPropertyChanged` and exposes:

```csharp
protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
{
    if (EqualityComparer<T>.Default.Equals(field, value)) return false;
    field = value;
    OnPropertyChanged(propertyName);
    return true;
}
```

`SetField` is a guard: it only fires `PropertyChanged` when the value actually changes. This prevents unnecessary UI redraws.

`[CallerMemberName]` is a compile-time attribute that the C# compiler replaces with the name of the calling property — `SetField(ref _name, value)` inside a property setter automatically passes `"Name"` as the property name without any string literal.

`ViewModelBase` also subscribes to `LanguageManager.Instance.PropertyChanged` to trigger a full rebind when the language changes.

### `ViewModels/Commands.cs`

A custom `Command` class implementing `ICommand`. Two constructors:

- `Command(Action execute, Func<bool>? canExecute)` — wraps parameterless actions.
- `Command(Action<object?> execute, Func<object?, bool>? canExecute)` — for commands that receive a parameter from the binding.

`RaiseCanExecuteChanged()` manually fires `CanExecuteChanged`, which triggers Avalonia to re-evaluate whether buttons should be enabled or disabled.

### `ViewModels/BackupJobViewModel.cs`

Observable wrapper around a `BackupJob`. Exposes:

- `Status` — when set, also notifies `StatusText`, `StatusColor`, `HasBeenRun`, `IsInProgress`, `HasError`.
- `IsRunning` — when set, calls `RaiseCanExecuteChanged()` on both `RunCommand` and `DeleteCommand`.
- `HasError` — computed: `Status == Error && !string.IsNullOrEmpty(ErrorMessage)`.
- `StatusText` — returns a localized string via `LangMgr["gui_done"]` etc.
- `StatusColor` — returns a hex color string matching the status.

`ApplyProgress` is called from the UI thread (via `Dispatcher.UIThread.InvokeAsync`) after each progress event:

```csharp
public void ApplyProgress(string jobName, BackupStatus status, int progression,
    string currentFile, bool blocked, string errorMessage = "")
{
    Status = status;
    Progression = Math.Max(0, progression);
    BlockedByBusinessSoftware = blocked;
    if (!string.IsNullOrEmpty(currentFile)) CurrentFile = currentFile;
    ErrorMessage = status == BackupStatus.Error ? errorMessage : string.Empty;
}
```

`ErrorMessage` is cleared when the status is not `Error`, so old errors don't persist after a successful re-run.

### `ViewModels/MainViewModel.cs`

The main ViewModel. Created by `MainWindow` at startup.

**Initialization:**

1. Loads settings via `SettingsManager`.
2. Creates `StateManager`, `BackupProcessor`, `ConfigManager`.
3. Calls `BackupProcessor.ProgressChanged += OnJobProgressChanged`.
4. Loads jobs from disk, creates a `BackupJobViewModel` card for each.
5. Creates `RunAllCommand`, `AddJobCommand`, `OpenSettingsCommand`.

**`RunCard` — single job on a background thread:**

```csharp
private void RunCard(BackupJobViewModel card)
{
    card.IsRunning = true;
    int index = _jobs.IndexOf(card.Job);
    Thread thread = new(() =>
    {
        try { RunJobByIndex(index); }
        catch (Exception) { }
        finally
        {
            Dispatcher.UIThread.InvokeAsync(() => card.IsRunning = false);
        }
    });
    thread.IsBackground = true;
    thread.Start();
}
```

`IsBackground = true` ensures this thread does not prevent the process from exiting if the user closes the window while a copy is in progress.

**`RunAll` — all jobs on a background thread:**

Same pattern as `RunCard` but calls `RunAllJobs()` and resets `_isRunningAll` in the `finally` block. `RunAllCommand` is disabled while running via `() => !_isRunningAll` as its `canExecute` predicate.

**`RunJob` — the actual multi-thread dispatch:**

```csharp
public bool RunJob(string input)
{
    List<int> indicesToRun = ParseIndices(input, _jobs.Count);
    var context = new BackupSyncContext(_settings.LargeFileSizeThresholdKb);
    var results = new bool[indicesToRun.Count];
    var threads = new List<Thread>();

    for (int i = 0; i < indicesToRun.Count; i++)
    {
        int capturedI = i;
        int capturedIndex = indicesToRun[i];
        threads.Add(new Thread(() =>
        {
            try
            {
                results[capturedI] = _backupProcessor.Execute(_jobs[capturedIndex], context);
            }
            catch (Exception)
            {
                results[capturedI] = false;
            }
        }));
    }

    foreach (var t in threads) t.Start();
    foreach (var t in threads) t.Join();
    return results.All(r => r);
}
```

Key points:
- One `BackupSyncContext` is shared across all job threads — this is what enables the cross-job priority barrier and large-file semaphore.
- The `try/catch` inside each thread is critical: `BackupProcessor.Execute` re-throws unexpected exceptions, and an unhandled exception on any .NET thread terminates the entire process.
- `capturedI` and `capturedIndex` are local copies of the loop variable — without these, all closures would capture the same variable and read its final value by the time the threads start.

**`OnJobProgressChanged` — bridging background threads to the UI:**

```csharp
private void OnJobProgressChanged(string jobName, BackupStatus status, int progression,
    string currentFile, bool blocked, string errorMessage)
{
    Dispatcher.UIThread.InvokeAsync(() =>
    {
        UpdateCardProgress(jobName, status, progression, currentFile, blocked, errorMessage);
    });
}
```

`InvokeAsync` (not `Invoke`) queues the callback on the UI thread and returns immediately. The file copy thread does not wait. This is critical for performance: with many parallel jobs each copying many files, using the synchronous `Invoke` would serialize every progress update through the UI thread, blocking each copy thread for the duration of the UI update.

**`ToUniqueList` — the shared deduplication helper:**

```csharp
private static List<string> ToUniqueList(IEnumerable<string> values, bool lowercase = false)
{
    var result = new List<string>();
    foreach (var value in values)
    {
        if (string.IsNullOrWhiteSpace(value)) continue;
        string clean = lowercase ? value.Trim().ToLowerInvariant() : value.Trim();
        if (!result.Contains(clean, StringComparer.OrdinalIgnoreCase))
            result.Add(clean);
    }
    return result;
}
```

Used for both business software process names (`lowercase: false`) and encrypted extensions (`lowercase: true`). Extensions are stored lowercase so that `.TXT` and `.txt` are treated as the same entry.

**`ParseIndices` — input parsing:**

Converts user input like `"1-3"` or `"1;4;5"` into a zero-based index list. The `1-3` range form is used by `RunAllJobs`, which constructs `"1-{_jobs.Count}"`. The `;` form is for CLI mode.

### `ViewModels/SettingsViewModel.cs`

Holds editable copies of all settings fields as bindable string properties. `Save()` collects them into a new `AppSettings` and fires the `Saved` event. `ParseLines` deduplicates the multiline text inputs (one entry per line) the same way `ToUniqueList` works.

### `ViewModels/AddJobViewModel.cs`

Holds the four fields for creating a new job. `CanCreate()` is the `canExecute` predicate for `CreateCommand` — it returns `false` if any required field is empty, keeping the Create button disabled until the form is valid. `_createCommand.RaiseCanExecuteChanged()` is called from each property setter so the button re-evaluates immediately as the user types.

---

## 10. Views layer

### `Views/MainWindow.xaml.cs`

Creates `MainViewModel` and wires the two dialog callbacks before setting `DataContext`:

```csharp
vm.RequestAddJob = callback =>
{
    var dialog = new AddJobWindow();
    dialog.JobCreated += job => callback(job);
    dialog.Cancelled += () => callback(null);
    dialog.Show(this);
};
```

The ViewModel never imports any View class. Instead it exposes `Action<Action<BackupJob?>>? RequestAddJob` — a callback that accepts a callback. The View sets this property and provides the actual window-opening logic. This is the standard MVVM pattern for dialogs: the ViewModel requests a dialog without knowing how it is shown.

### `Views/AddJobWindow.xaml.cs` and `Views/SettingsWindow.xaml.cs`

Both follow the same pattern:
1. Create their ViewModel in the constructor.
2. Subscribe to the ViewModel's `Saved`/`JobCreated` and `Cancelled` events.
3. When the ViewModel fires the event, relay it to the Window's own event, then call `Close()`.

The Window's events (`JobCreated`, `Saved`, `Cancelled`) are what `MainWindow` subscribes to via the callbacks above.

---

## 11. Threading model

The application uses three categories of threads:

### UI thread (Avalonia dispatcher)

All bindings, property change notifications, and control updates must happen here. `Dispatcher.UIThread.InvokeAsync(action)` queues work here from any thread.

### Outer background thread (one per RunCard / RunAll)

Started by `RunCard` or `RunAll`. Calls `RunJobByIndex` or `RunAllJobs`. Contains the outer `try/catch/finally` that resets UI state when the run completes.

### Inner job threads (one per job in RunJob)

Started inside `RunJob`. Each calls `BackupProcessor.Execute`. These threads are not `IsBackground`, but they are always joined before `RunJob` returns, so they never outlive the outer background thread.

### Inner file threads (one per file in CopyGroup)

Started inside `CopyGroup`. Each copies one file. These are also joined (`t.Join()` on each) before `CopyGroup` returns. Since `CopyGroup` is called inside a job thread, file threads are indirectly joined before the job thread finishes.

```
UI thread
└── [button click] RunAll()
    └── outer background thread (IsBackground=true)
        └── RunAllJobs() → RunJob("1-N")
            ├── job thread 1 → Execute(job[0]) → CopyGroup
            │   ├── file thread 1-1 → CopyFile
            │   ├── file thread 1-2 → CopyFile
            │   └── ...
            ├── job thread 2 → Execute(job[1]) → CopyGroup
            │   ├── file thread 2-1 → CopyFile
            │   └── ...
            └── ...
```

---

## 12. Thread safety — primitives and placement

| Location | Primitive | What it protects |
|---|---|---|
| `StateManager._stateLock` | `object` + `lock` | Read-modify-write on the state file — prevents two job threads writing simultaneously and causing `IOException` or data loss |
| `BackupStrategyBase._lock` | `System.Threading.Lock` | `onFileCopied` and `logService.Save` calls — prevents concurrent log writes from file threads within one job |
| `BackupSyncContext._priorityFilesRemaining` | `Interlocked.Add` / `Interlocked.Decrement` | Atomic counter update — no lock needed; CPU atomic instruction is sufficient |
| `BackupSyncContext._allPriorityDone` | `ManualResetEventSlim` | Cross-job broadcast barrier — releases all non-priority threads when the last priority file finishes |
| `BackupSyncContext._largeFileSemaphore` | `SemaphoreSlim(1,1)` | Limits concurrent large-file transfers to one at a time across all jobs |
| `BackupProcessor.OnBytesWritten.bytesCopied` | `Interlocked.Add` | Running byte total accumulated from concurrent file threads |
| `BackupStrategyBase.CopyGroup.cancelled` | `Volatile.Read` / `Volatile.Write` | Cancellation flag read by many threads, written by one — Volatile ensures CPU cache coherence without a lock |
| `LanguageManager._instance` | `object` + double-checked lock | Singleton creation — prevents two threads each creating a separate instance at startup |
| `FileHelper._appDataFolder` | Static field, lazy init | Folder path cached after first call — no lock because the write is idempotent (worst case: two threads compute the same path) |
