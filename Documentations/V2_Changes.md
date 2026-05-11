# EasySave v2.0 – What I Changed 

Here's a rundown of everything I did to go from v1.0 to v2.0. 

Celui est juste pour vous les gars afin qu'on soit tous au meme niveau!

---

## 1. Switched the UI to a real graphical interface

v1.0 was console-only. For v2.0 I built a full GUI using Avalonia. The main window shows all backup jobs as individual cards — each card displays the job name, type (Full/Differential), source and target paths, a progress bar while it's running, and two buttons: run and delete. There's a top bar with the app title, a language switcher, and a settings button. At the bottom there's an "Add Job" button and a "Run All" button.

---

## 2. Removed the 5-job limit

In v1.0 there was a hard cap of 5 backup jobs. I removed that entirely. You can now create as many jobs as you want and they all persist between sessions.

---

## 3. Added a Settings window

There's now a dedicated settings dialog (modal window) where you can configure:
- The display language (EN/FR)
- The log file format (JSON or XML)
- The state file format (JSON or XML)
- Business software process names to block backups
- File extensions to encrypt (e.g. `.txt`, `.docx`)
- The encryption key passed to CryptoSoft

---

## 4. Added JSON/XML format selection for logs and state

Previously everything was hardcoded to JSON. I added an `OutputFormat` enum and wired it through the whole stack — `DailyLogManager` and `StateManager` both now support both formats. The user picks their preferred format in settings and it's saved in `AppSettings`.

---

## 5. Business software blocking

Before any backup starts (and before each individual file copy), the app checks whether a "business" process is running. If it detects one, it blocks or stops the backup and logs the event. The list of process names to watch is configurable in settings.

---

## 6. CryptoSoft integration

After each file is copied, if its extension is in the encrypted extensions list, the app calls `CryptoSoft.exe` with the file path and the encryption key. The time CryptoSoft takes (in ms) is recorded in the log entry. If it fails, the error code goes in the log too. If the exe isn't there, the copy still completes — encryption just gets skipped.

---

## 7. Enriched log entries

Log entries now include an `EncryptionTimeMs` field alongside the existing transfer time and file size fields. This applies to both JSON and XML output.

---

## 8. Architecture changes

I restructured the execution layer to avoid passing a growing list of parameters everywhere. I introduced a `BackupExecutionContext` object that bundles the log manager, settings, crypto service, and callbacks together, and the backup strategies receive that instead of individual arguments.

`BackupProcessor` now fires a `ProgressChanged` event as files are copied, which the GUI subscribes to in order to update the progress bars in real time without blocking the UI thread.

---

## 9. CryptoSoft — built as a separate project

CryptoSoft is now a standalone .NET 9 console project (`CryptoSoft/`) included in the solution. It applies XOR encryption with a user-defined key — the same operation both encrypts and decrypts. It takes two command-line arguments: the file path and the key.

Exit codes: `0` = success, `-1` = bad arguments or empty key, `-2` = file not found, `-3` = runtime error.

The `EasySaveWpf.csproj` build target automatically compiles CryptoSoft and copies the output (`CryptoSoft.exe`, `CryptoSoft.dll`, etc.) into `bin/Debug/net9.0/` so it's always available alongside the main app.

---

## 10. CLI support kept from v1.0

`Program.cs` now checks for command-line arguments at startup. If arguments are present (e.g. `EasySaveWpf.exe 1-3` or `EasySaveWpf.exe "1;3"`), the app runs the specified jobs headlessly and exits. If no arguments are given, the GUI launches normally.

---

## 11. New files created

| File | Purpose |
|---|---|
| `Models/Enums/OutputFormat.cs` | JSON or XML choice |
| `Models/Data/AppSettings.cs` | Persisted user config |
| `Models/Data/AppLogEntry.cs` | Enriched log entry with encryption time |
| `Models/Data/BackupExecutionContext.cs` | Groups execution parameters |
| `Models/Data/BackupProgressEventArgs.cs` | Event data for GUI progress updates |
| `ViewModels/ViewModelBase.cs` | INotifyPropertyChanged base class |
| `ViewModels/RelayCommand.cs` | Sync and async ICommand implementations |
| `ViewModels/CryptoSoftService.cs` | Wraps CryptoSoft.exe |
| `ViewModels/DailyLogManager.cs` | Writes daily log files (JSON/XML) |
| `ViewModels/SettingsManager.cs` | Loads/saves AppSettings to disk |
| `ViewModels/BusinessSoftwareMonitor.cs` | Detects running business processes |
| `ViewModels/JobCardViewModel.cs` | Observable state for one job card in the UI |
| `ViewModels/MainWindowViewModel.cs` | Main Avalonia window view model |
| `ViewModels/AddJobViewModel.cs` | Add job dialog view model |
| `ViewModels/SettingsViewModel.cs` | Settings dialog view model |
| `Views/MainWindow.xaml` | Full rewrite of the main window UI |
| `Views/SettingsWindow.xaml/.cs` | Settings modal dialog |
| `Views/AddJobWindow.xaml/.cs` | Add job modal dialog |
| `CryptoSoft/Program.cs` | XOR file encryptor/decryptor |
| `CryptoSoft/CryptoSoft.csproj` | Standalone console project for encryption |

---

## 12. Error display when a backup fails

Before this fix, if a backup encountered a fatal error — source directory doesn't exist, missing paths in the job config, access denied by the OS — the job card would still show the progress bar reaching 100% and the status as "Done", as if everything went fine. There was no visual feedback at all, and nothing was written to the log file to trace the failure.

This has been completely reworked. When a backup hits a fatal error now:

- The job card immediately shows the error message in **red**, directly below the progress bar. The message is the actual exception text (e.g. "Source directory not found", "Access to the path is denied").
- The progress bar **stops at its current position** instead of jumping to 100%. The status color turns red.
- The job status shows **"Error"** in red instead of the green "Done".
- A **log entry** is written with the exception type and message, so you always have a trace of what failed and why (e.g. `Error:DirectoryNotFoundException:Source not found`).
- The **state file** is updated to reflect the error status instead of marking the job as ended.

The error message is automatically **cleared** the next time the job runs successfully, so old errors don't stick around after a successful retry.

Errors that are detected before the backup even starts (e.g. the source path is empty or the directory simply doesn't exist) are caught the same way and shown immediately without starting the copy process.
