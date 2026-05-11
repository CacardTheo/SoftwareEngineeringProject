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