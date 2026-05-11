# EasySave — Code changes explanation

## The original problem

When a backup was started, the progress bar jumped directly from 0% to 100%
with no update in between. It also overflowed visually outside the window.

---

## Why the progress bar wasn't moving

The original code copied files like this:

```
For each file:
    Copy the entire file
    Then notify the interface that the file is done
```

The problem: for a 4 GB ISO file, the interface was only notified **once**,
when all 4 GB were fully copied. During the entire copy, nothing happened visually.

The fix: notify the interface **during** the copy, not only at the end.

---

## How the copy works now

### `Utils/FileHelper.cs` — The actual file copy

This is where data is physically read and written to disk.
The copy works in small pieces called **chunks** (blocks of data).
Before, the method copied each chunk silently.

Now, after each chunk is written, it calls a function that was given to it
to say "I just wrote X bytes". That function is called `onBytesWritten`.

```csharp
while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
{
    target.Write(buffer, 0, bytesRead);
    onBytesWritten?.Invoke(sourcePath, targetPath, bytesRead); // new
}
```

The `?.` means "only call this function if it exists".
It is optional: if no function is provided, the copy runs normally without any notification.

Since `FileHelper` already knows the source and destination paths, it passes them
directly in every notification — no extra work needed elsewhere.

The chunk size was also increased: 4,096 bytes (4 KB) → 81,920 bytes (80 KB).
A bigger chunk = fewer read/write operations = faster copy, especially for large files.

---

### `Models/Strategies/IBackupStrategy.cs` — The strategy contract

This is the interface that all backup strategies must follow.
An `onBytesWritten` parameter was added so each strategy can pass
progress notifications up to the rest of the program.

---

### `Models/Strategies/FullBackupStrategy.cs` and `DifferentialBackupStrategy.cs`

These classes decide which files to copy and in what order.
For each file, they call `FileHelper.CopyFile` and pass the `onBytesWritten`
function directly.

```csharp
FileHelper.CopyFile(filePath, targetPath, onBytesWritten);
```

Simple and straightforward — no extra logic needed here.

---

### `ViewModels/BackupProcessor.cs` — The progress calculator

This is where all the progression logic is calculated.
Two functions (called callbacks) handle the incoming notifications:

**`OnBytesWritten` — called after every 80 KB chunk**

It receives the number of bytes just written, adds them to the running total,
then calculates the percentage: `(bytes copied / total bytes) × 100`.

If the percentage hasn't changed since last time, it does nothing.
If the percentage changed (e.g. 37% → 38%), it updates `state.json`
and sends the new value to the UI.

```csharp
void OnBytesWritten(string sourceFile, string destFile, long bytes)
{
    bytesCopied += bytes;
    int progression = totalSize > 0 ? (int)(bytesCopied * 100 / totalSize) : 0;

    if (progression == lastReportedProgression) return; // nothing new
    lastReportedProgression = progression;

    _stateManager.UpdateJobState(...); // update state.json
    RaiseProgress(...);               // notify the UI
}
```

**`OnFileCopied` — called once per fully completed file**

It increments the file counter, which allows calculating how many files are left.
It also updates the UI to show the name of the last completed file.

It does not add bytes to `bytesCopied` because they were already counted
chunk by chunk inside `OnBytesWritten`.

---

### `ViewModels/MainViewModel.cs` — Updating the interface

The UI runs on a dedicated thread (the UI thread).
The backup runs on a separate background thread.
These two threads cannot safely modify the same elements at the same time.

To update the interface from the backup thread, `Invoke` is used.
`Invoke` tells the UI thread "update the progress bar" and **waits** for it
to be done before continuing the copy.

If `InvokeAsync` (the non-waiting version) were used, all updates would pile up
and only be processed when the backup is finished.
That is what caused the 0% → 100% jump.

---

### `Views/MainWindow.xaml` — The visual interface

Three visual fixes:

**Cards were overflowing the window** when a file path was too long.
Horizontal scrolling was disabled (`HorizontalScrollBarVisibility="Disabled"`),
which forces cards to stay within the window width.

**Long file paths** are now cut off with `…` (`TextTrimming="CharacterEllipsis"`).

**The minimum window width** was increased from 700 to 800 pixels to give
more room to the progress bar and file paths.

---

## Full flow summary

```
1. FileHelper copies an 80 KB chunk
2. FileHelper calls onBytesWritten(source, destination, bytes)
3. BackupProcessor.OnBytesWritten adds the bytes to the running total
4. If the % changed → state.json updated + notification sent to the UI
5. MainViewModel receives the notification → Invoke → progress bar updated
6. Repeat until the file is fully copied
7. FileHelper finishes the file → OnFileCopied → file counter updated
```

---

## Error handling — displaying errors in the UI and writing them to logs

### The original problem

When a fatal error occurred during a backup (for example: the source directory
doesn't exist, the paths in the job are empty, or the OS refuses access),
the backup strategies would print a message to the console and then silently
`return` without throwing an exception.

Because they returned normally, `BackupProcessor` had no way to know something
went wrong. It assumed the backup had succeeded and would:
- Set the job status to `Ended`
- Set the progression to `100%`
- Fire `ProgressChanged` with `BackupStatus.Ended`

The UI showed the job as successfully completed, even though nothing was copied.
No log entry was written either, so there was no trace of the failure.

---

### Fix 1 — Strategies throw exceptions instead of returning silently

In `FullBackupStrategy.cs` and `DifferentialBackupStrategy.cs`, every silent
`return` path was replaced with a typed exception:

```csharp
// Before:
if (string.IsNullOrEmpty(job.SourceDir) || string.IsNullOrEmpty(job.TargetDir))
{
    Console.WriteLine(_languageManager.GetText("log_error_missing_paths"));
    return; // BackupProcessor never knows this happened
}

// After:
if (string.IsNullOrEmpty(job.SourceDir) || string.IsNullOrEmpty(job.TargetDir))
    throw new ArgumentException(_languageManager.GetText("log_error_missing_paths"));
```

The full mapping of errors to exception types:

| Situation | Exception thrown |
|---|---|
| Source or target path is null/empty | `ArgumentException` |
| Source path does not exist on disk | `DirectoryNotFoundException` |
| OS won't let us list the directory contents | `IOException` |
| OS refuses access to a file or directory | `UnauthorizedAccessException` |

The `UnauthorizedAccessException` catch in `FullBackupStrategy` previously also
called `logService.Save()` directly. That call was removed because
`BackupProcessor` now centralizes all fatal error logging (see Fix 2).

---

### Fix 2 — BackupProcessor centralizes all fatal error logging

`BackupProcessor`'s generic `catch` block was changed from a bare `catch` to
`catch (Exception ex)`, which gives access to the exception object.
A `logService.Save()` call was added inside it, so every fatal exception —
regardless of where it was thrown in the strategy — gets a log entry:

```csharp
catch (Exception ex)
{
    _stateManager.UpdateJobState(new StateEntry
    {
        Name = job.Name ?? "Unnamed Job",
        State = BackupStatus.Error,
        Progression = 0,
        // ...
    });

    logService.Save(new LogEntry
    {
        BackupName = job.Name ?? string.Empty,
        SourceFilePath = job.SourceDir ?? string.Empty,
        TargetFilePath = string.Empty,
        FileSize = 0,
        FileTransferTimeMs = -1,  // -1 signals "failed, not a real duration"
        EncryptionTimeMs = 0,
        Event = $"Error:{ex.GetType().Name}:{ex.Message}"
    });

    RaiseProgress(job.Name, BackupStatus.Error, 0, errorMessage: ex.Message);
    throw; // re-throw so the calling thread can handle it too
}
```

This means any fatal exception automatically produces:
1. A state update set to `BackupStatus.Error` with `Progression = 0`
2. A log entry formatted as `Error:ExceptionType:message`
3. A UI notification carrying the error message text

---

### Fix 3 — The ProgressChanged event carries the error message

`ProgressChanged` previously had 5 parameters.
A 6th `string errorMessage` parameter was added at the end:

```csharp
// Before:
public event Action<string, BackupStatus, int, string, bool>? ProgressChanged;

// After:
public event Action<string, BackupStatus, int, string, bool, string>? ProgressChanged;
```

The `RaiseProgress` helper was extended the same way:

```csharp
private void RaiseProgress(string? jobName, BackupStatus status, int progression,
    string currentFile = "", bool blocked = false, string errorMessage = "")
{
    ProgressChanged?.Invoke(jobName ?? string.Empty, status, progression, currentFile, blocked, errorMessage);
}
```

The error message travels through this chain without any transformation:

```
BackupProcessor.catch(Exception ex)
  → RaiseProgress(errorMessage: ex.Message)
    → ProgressChanged.Invoke(..., ex.Message)
      → MainViewModel.OnJobProgressChanged(..., errorMessage)
        → MainViewModel.UpdateCardProgress(..., errorMessage)
          → BackupJobViewModel.ApplyProgress(..., errorMessage)
            → BackupJobViewModel.ErrorMessage (property)
              → HasError (computed property)
                → MainWindow.xaml TextBlock (IsVisible="{Binding HasError}")
```

---

### Fix 4 — BackupJobViewModel: ErrorMessage property and HasError computed property

Two members were added to `BackupJobViewModel`:

```csharp
private string _errorMessage = string.Empty;

public string ErrorMessage
{
    get => _errorMessage;
    set
    {
        if (SetField(ref _errorMessage, value))
            OnPropertyChanged(nameof(HasError)); // HasError depends on this value
    }
}

public bool HasError => Status == BackupStatus.Error && !string.IsNullOrEmpty(_errorMessage);
```

`HasError` is a computed property: it returns `true` only when the status is
`Error` AND there is a non-empty error message. This prevents a stale red message
from showing if the job somehow enters Error status without a message.

The `Status` setter was also updated to notify `HasError`, because `HasError`
depends on the status value:

```csharp
set
{
    SetField(ref _status, value);
    OnPropertyChanged(nameof(StatusText));
    OnPropertyChanged(nameof(StatusColor));
    OnPropertyChanged(nameof(HasBeenRun));
    OnPropertyChanged(nameof(IsInProgress));
    OnPropertyChanged(nameof(HasError)); // added
}
```

`ApplyProgress` was extended to accept and set the error message:

```csharp
public void ApplyProgress(string jobName, BackupStatus status, int progression,
    string currentFile, bool blocked, string errorMessage = "")
{
    Status = status;
    Progression = Math.Max(0, progression);
    BlockedByBusinessSoftware = blocked;
    if (!string.IsNullOrEmpty(currentFile))
        CurrentFile = currentFile;
    ErrorMessage = status == BackupStatus.Error ? errorMessage : string.Empty;
    // If the job is not in error state, clear any previous error message
}
```

---

### Fix 5 — MainWindow.xaml: error TextBlock in the job card

A new `TextBlock` was added inside each job card in the DataTemplate,
placed after the "blocked by business software" warning:

```xml
<!-- error message -->
<TextBlock IsVisible="{Binding HasError}"
           Text="{Binding ErrorMessage}"
           Foreground="#F44336" FontSize="12" Margin="0,6,0,0"
           TextWrapping="Wrap"/>
```

- `IsVisible="{Binding HasError}"` — the block is hidden when there is no error
- `Text="{Binding ErrorMessage}"` — shows the raw exception message
- `Foreground="#F44336"` — red, consistent with the error status color
- `TextWrapping="Wrap"` — long error messages wrap inside the card instead of overflowing

---

### Full error flow summary

```
1. Strategy detects a fatal condition (missing path, source not found, access denied...)
2. Strategy throws a typed exception (ArgumentException, DirectoryNotFoundException, etc.)
3. BackupProcessor.catch(Exception ex) intercepts it
4. State file updated → BackupStatus.Error, Progression = 0
5. Log file entry written → Event = "Error:ExceptionType:message"
6. RaiseProgress fires with BackupStatus.Error and the error message string
7. MainViewModel receives it on the background thread
8. Dispatcher.UIThread.Invoke switches to the UI thread
9. BackupJobViewModel.ApplyProgress sets ErrorMessage and Status = Error
10. HasError becomes true → red TextBlock appears in the job card
11. Exception is re-thrown → RunCard/RunAll catch it silently to keep the UI alive
```

---

## Parallel backup, priority management across jobs, and large file limit

### The original problem

Files were copied one by one, sequentially. Multiple backup jobs also ran one after another in a single background thread.
Priority extensions existed in settings but only sorted the file list — no parallelism was possible.

---

### New class: `Models/BackupSyncContext.cs`

A `BackupSyncContext` is created once per run (in `MainViewModel.RunJob`) and shared by all jobs in that batch.
It enforces two constraints across every concurrent job:

**1. Priority barrier**

A shared atomic counter `_priorityFilesRemaining` tracks how many priority files are still pending across all running jobs.
A `ManualResetEventSlim _allPriorityDone` is the signal:

- `Reset` (blocked) while any priority file is still in progress
- `Set` (open) when the counter reaches zero

```
RegisterPriorityFiles(count)   → increments the counter, Resets the event
NotifyPriorityFileDone()       → decrements the counter; if 0 → Sets the event
WaitForAllPriorityFiles()      → blocks the calling thread until the event is Set
```

**2. Large file semaphore**

A `SemaphoreSlim(1, 1)` allows at most one transfer of a large file at a time.
The threshold is `LargeFileSizeThresholdKb` from `AppSettings` (0 = disabled).
Small files are never blocked by this semaphore.

---

### `Models/Strategies/FullBackupStrategy.cs` and `DifferentialBackupStrategy.cs`

Both strategies now accept a `BackupSyncContext` via their constructor.
The sequential `foreach` was replaced by a two-phase parallel model:

**Phase 1 — priority files (all in parallel)**

Before starting the threads:
```csharp
_context.RegisterPriorityFiles(prioritized.Count);
```

Each priority thread wraps its work in `try/finally`:
```csharp
finally
{
    if (isPriorityGroup) _context.NotifyPriorityFileDone();
}
```
`NotifyPriorityFileDone` is called unconditionally — even if the thread was cancelled or failed —
so non-priority threads waiting on `WaitForAllPriorityFiles()` are never permanently blocked.

**Phase 2 — regular files (all in parallel)**

Each non-priority thread checks twice:
```csharp
_context.WaitForAllPriorityFiles(); // blocks until ALL jobs' priority files are done
if (Volatile.Read(ref cancelled) != 0) return; // re-check in case cancellation occurred while waiting
```

**Large file slot acquisition (both phases)**

```csharp
bool isLargeFile = _context.LargeFileSizeThresholdBytes > 0
                   && captured.Length > _context.LargeFileSizeThresholdBytes;
if (isLargeFile) _context.AcquireLargeFileSlot();
try   { FileHelper.CopyFile(...); }
finally { if (isLargeFile) _context.ReleaseLargeFileSlot(); }
```

**Thread-safety for callbacks and log writes**

`onFileCopied` and `logService.Save` are called inside `lock (_lock)` to prevent concurrent calls from multiple threads in the same job.
`Directory.CreateDirectory` is idempotent in .NET and does not need a lock.

---

### `ViewModels/BackupProcessor.cs`

`Execute` now takes a `BackupSyncContext` parameter and passes it to the strategy constructor:

```csharp
public bool Execute(BackupJob job, BackupSyncContext context)
{
    ...
    strategy = new FullBackupStrategy(context);
    ...
}
```

`OnBytesWritten` was fixed for thread-safety: `bytesCopied += bytes` (not atomic) was replaced by:
```csharp
long current = Interlocked.Add(ref bytesCopied, bytes);
```

---

### `ViewModels/MainViewModel.cs`

`RunJob` now creates one `BackupSyncContext` for the whole batch, then launches each job in its own `Thread`:

```csharp
var context = new BackupSyncContext(_settings.LargeFileSizeThresholdKb);

for (int i = 0; i < indicesToRun.Count; i++)
{
    int capturedI = i;
    int capturedIndex = indicesToRun[i];
    var thread = new Thread(() =>
    {
        results[capturedI] = _backupProcessor.Execute(_jobs[capturedIndex], context);
    });
    threads.Add(thread);
}

foreach (var t in threads) t.Start();
foreach (var t in threads) t.Join(); // wait for all jobs to finish before returning
```

`RunJobByIndex` (single job) creates its own context so the large file semaphore still applies.

---

### Full parallel flow summary

```
1. MainViewModel.RunJob creates one BackupSyncContext for the batch
2. One Thread is started per job — all jobs run concurrently
3. Each strategy calls RegisterPriorityFiles(count) before starting file threads
4. Priority file threads run in parallel (phase 1)
5. Each priority thread calls NotifyPriorityFileDone() in its finally block
6. When the last priority file across ALL jobs is done → ManualResetEventSlim is Set
7. Non-priority threads unblock from WaitForAllPriorityFiles() (phase 2)
8. Non-priority file threads run in parallel across all jobs
9. Large files (> threshold) acquire the SemaphoreSlim before copying — max 1 at a time
10. Small files copy freely without any semaphore
11. Join() on all job threads — RunJob returns when every job has completed
```

---

### New settings

| Setting | Type | Default | Effect |
|---|---|---|---|
| `PrioritizedExtensions` | `List<string>` | empty | Extensions that are always copied before all others |
| `LargeFileSizeThresholdKb` | `int` | `0` | Files larger than this (in KB) share a single transfer slot. `0` disables the limit. |

---

## Technical design choices — why these primitives and not others

### `Thread` + `Join()` instead of `Task` / `Parallel.ForEach`

`Task` and `Parallel.ForEach` are built on the .NET **ThreadPool**.
The ThreadPool manages a shared pool of reusable threads and may throttle creation when many are requested at once — it is designed for short, CPU-bound work.

Backup copies can run for several seconds or several minutes per file (I/O-bound, not CPU-bound). Using ThreadPool threads for long-running I/O would hold pool threads hostage for the duration of the copy, potentially starving other parts of the application that rely on the pool.

`Thread` creates a **dedicated OS thread** per file. Each thread lives exactly as long as its copy takes, then terminates. `Join()` is an explicit barrier — "stop here until every thread in this group has finished" — which maps exactly to the two-phase model (phase 1 finishes, then phase 2 starts).

---

### `ManualResetEventSlim` instead of `AutoResetEvent`, `Mutex`, or `Monitor.Wait`

The priority barrier needs to **unblock all waiting threads at once** when the last priority file finishes. That is a broadcast, not a one-at-a-time release.

| Primitive | Behavior | Why not used |
|---|---|---|
| `AutoResetEvent` | Releases **one** waiting thread, then resets automatically | With 50 non-priority threads waiting, each would need its own signal. Not a broadcast. |
| `Mutex` | Mutual exclusion, one thread at a time | A mutex is for protecting a resource, not for signalling. Has thread affinity (see below). |
| `Monitor.Wait` / `Pulse` | Requires holding a `lock`, one `PulseAll` to broadcast | More complex to use correctly; higher risk of missed signals if `PulseAll` fires before any thread calls `Wait`. |
| `ManualResetEventSlim` | Stays **open** once `Set()` is called; all waiting threads unblock simultaneously; can be `Reset()` to close again | Exactly the "gate" semantics needed. |

The **Slim** suffix matters: `ManualResetEventSlim` uses CPU spin-waiting for a short time before falling back to a kernel wait object. For a barrier that is held only briefly (the duration of copying a few priority files), the spin avoids the overhead of a kernel context switch in the common case.

---

### `SemaphoreSlim(1, 1)` instead of `Mutex`

Both can restrict access to one thread at a time. The critical difference is **thread affinity**:

- A `Mutex` can only be released by the **same thread** that acquired it.
- A `SemaphoreSlim` has **no thread affinity**: any thread can call `Release()`.

Since acquire and release happen in the same thread here (wrapped in `try/finally`), a `Mutex` would technically work. `SemaphoreSlim` was chosen for three reasons:

1. **No kernel object overhead** when uncontended — it is a pure managed object until a thread actually needs to block.
2. **Easier to extend**: changing from `SemaphoreSlim(1, 1)` to `SemaphoreSlim(2, 2)` would allow two concurrent large-file transfers without changing any other code.
3. **Consistent with the rest of the codebase**: `SemaphoreSlim` is the standard .NET choice for lightweight counting semaphores.

---

### `Interlocked.Add` / `Interlocked.Decrement` instead of `lock`

`lock` works by acquiring a monitor, which involves a kernel object under contention (a context switch into the OS). For a simple integer counter shared between threads, that is excessive overhead.

`Interlocked` operations map directly to CPU atomic instructions (e.g. `LOCK XADD` on x86). The hardware guarantees that the read-modify-write happens as a single indivisible operation. No kernel transition, no context switch. This is the correct and idiomatic tool for shared counters in .NET.

---

### `Volatile.Read` / `Volatile.Write` instead of `lock`

The `cancelled` flag is a simple integer: written to `1` by one thread, read by many threads.

Modern CPUs and compilers can reorder instructions and cache values in registers for performance. Without `Volatile`, a thread could keep reading a stale `0` from its register even after another thread has written `1` to memory.

`Volatile.Write` forces the write to be immediately visible in main memory.
`Volatile.Read` forces a fresh read from main memory, bypassing any CPU cache or compiler optimization.

`lock` would also ensure visibility, but it carries the overhead of monitor acquisition. For a single flag with one writer and multiple readers, `Volatile` is sufficient and faster.

---

### `System.Threading.Lock` instead of `object` for `_lock`

Before .NET 9, the standard pattern was to declare `private readonly object _lock = new object()` and use `lock (_lock) { ... }`. This works, but `object` has no special meaning — the JIT treats it as a general-purpose heap allocation.

In .NET 9, `System.Threading.Lock` is a **purpose-built mutual exclusion type**. The JIT recognizes it specifically and can generate more efficient code. It also prevents accidental misuse (locking on `this`, on a string, etc.) because the type makes the intent explicit.

---

### `BackupStrategyBase` abstract class instead of duplicating `CopyGroup`

`FullBackupStrategy` and `DifferentialBackupStrategy` share identical threading logic. The only difference between them is one predicate: "should this specific file be copied?"

- Full backup: always copy → `(file, target) => true`
- Differential backup: copy only if newer or absent → `(file, target) => !File.Exists(target) || file.LastWriteTime > ...`

Without a base class, the entire `CopyGroup` method (~80 lines) and `TryEncrypt` would be copy-pasted in both files. A bug fix or improvement would need to be applied twice. The abstract base class moves all shared logic into one place and exposes the variation through a `Func<FileInfo, string, bool> shouldCopy` delegate parameter.