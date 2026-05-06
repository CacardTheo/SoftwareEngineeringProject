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