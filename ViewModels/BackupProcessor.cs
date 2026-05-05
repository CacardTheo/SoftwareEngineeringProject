using EasySaveWpf;
using EasyLog;

namespace EasySaveWpf.ViewModels;

public class BackupProcessor
{
    private readonly StateManager _stateManager;
    private readonly BusinessSoftwareMonitor _businessSoftwareMonitor;
    private readonly CryptoSoftService _cryptoSoftService;
    private readonly Func<AppSettings> _settingsProvider;

    public event EventHandler<BackupProgressEventArgs>? ProgressChanged;

    public BackupProcessor(
        StateManager stateManager,
        BusinessSoftwareMonitor businessSoftwareMonitor,
        CryptoSoftService cryptoSoftService,
        Func<AppSettings> settingsProvider)
    {
        _stateManager = stateManager;
        _businessSoftwareMonitor = businessSoftwareMonitor;
        _cryptoSoftService = cryptoSoftService;
        _settingsProvider = settingsProvider;
    }

    public bool Execute(BackupJob job)
    {
        AppSettings settings = _settingsProvider();
        _stateManager.SetFormat(settings.StateFormat);
        var logService = new LogService(settings.LogFormat);

        if (IsBusinessSoftwareRunning(settings, out string detectedBeforeStart))
        {
            LogBusinessSoftwareBlock(job, detectedBeforeStart, logService, "BlockedBeforeStart");
            RaiseProgress(job.Name, BackupStatus.Inactive, 0, blocked: true);
            return false;
        }

        IBackupStrategy strategy = job.Type switch
        {
            BackupType.Full => new FullBackupStrategy(),
            BackupType.Differential => new DifferentialBackupStrategy(),
            _ => throw new ArgumentException($"Unknown backup type: {job.Type}")
        };

        string[] files;
        try
        {
            files = Directory.GetFiles(job.SourceDir ?? string.Empty, "*.*", SearchOption.AllDirectories);
        }
        catch
        {
            files = Array.Empty<string>();
        }

        int totalFiles = files.Length;
        long totalSize = files.Sum(file => new FileInfo(file).Length);

        _stateManager.UpdateJobState(new StateEntry
        {
            Name = job.Name ?? "Unnamed Job",
            SourceFilePath = job.SourceDir ?? string.Empty,
            TargetFilePath = job.TargetDir ?? string.Empty,
            State = BackupStatus.Inactive,
            TotalFilesToCopy = totalFiles,
            TotalFilesSize = totalSize,
            NbFilesLeftToDo = totalFiles,
            SizeRemaining = totalSize,
            Progression = 0,
            LastRun = DateTime.Now
        });

        RaiseProgress(job.Name, BackupStatus.In_Progress, 0);

        try
        {
            long bytesCopied = 0;
            int filesCopied = 0;

            var context = new BackupExecutionContext
            {
                LogService = logService,
                Settings = settings,
                CryptoService = _cryptoSoftService,
                OnFileCopied = (sourceFile, destFile, fileSize) =>
                {
                    filesCopied++;
                    bytesCopied += fileSize;
                    int progression = totalSize > 0 ? (int)((bytesCopied * 100) / totalSize) : 0;

                    _stateManager.UpdateJobState(new StateEntry
                    {
                        Name = job.Name ?? "Unnamed Job",
                        SourceFilePath = sourceFile,
                        TargetFilePath = destFile,
                        State = BackupStatus.In_Progress,
                        TotalFilesToCopy = totalFiles,
                        TotalFilesSize = totalSize,
                        NbFilesLeftToDo = Math.Max(0, totalFiles - filesCopied),
                        SizeRemaining = Math.Max(0, totalSize - bytesCopied),
                        Progression = progression,
                        LastRun = DateTime.Now
                    });

                    RaiseProgress(job.Name, BackupStatus.In_Progress, progression, currentFile: sourceFile);
                },
                CanCopyNextFile = () => !IsBusinessSoftwareRunning(settings, out _)
            };

            strategy.Backup(job, context);

            _stateManager.UpdateJobState(new StateEntry
            {
                Name = job.Name ?? "Unnamed Job",
                SourceFilePath = job.SourceDir ?? string.Empty,
                TargetFilePath = job.TargetDir ?? string.Empty,
                State = BackupStatus.Ended,
                TotalFilesToCopy = totalFiles,
                TotalFilesSize = totalSize,
                NbFilesLeftToDo = 0,
                SizeRemaining = 0,
                Progression = 100,
                LastRun = DateTime.Now
            });

            RaiseProgress(job.Name, BackupStatus.Ended, 100);
            return true;
        }
        catch (InvalidOperationException ex) when (ex.Message == "BUSINESS_SOFTWARE_DETECTED")
        {
            IsBusinessSoftwareRunning(settings, out string detectedProcess);
            LogBusinessSoftwareBlock(job, detectedProcess, logService, "StoppedDuringExecution");
            RaiseProgress(job.Name, BackupStatus.Inactive, -1, blocked: true);

            _stateManager.UpdateJobState(new StateEntry
            {
                Name = job.Name ?? "Unnamed Job",
                SourceFilePath = job.SourceDir ?? string.Empty,
                TargetFilePath = job.TargetDir ?? string.Empty,
                State = BackupStatus.Inactive,
                LastRun = DateTime.Now
            });

            return false;
        }
        catch
        {
            _stateManager.UpdateJobState(new StateEntry
            {
                Name = job.Name ?? "Unnamed Job",
                SourceFilePath = job.SourceDir ?? string.Empty,
                TargetFilePath = job.TargetDir ?? string.Empty,
                State = BackupStatus.Error,
                TotalFilesToCopy = 0,
                TotalFilesSize = 0,
                NbFilesLeftToDo = 0,
                SizeRemaining = 0,
                Progression = 0,
                LastRun = DateTime.Now
            });

            RaiseProgress(job.Name, BackupStatus.Error, 0);
            throw;
        }
    }

    private void RaiseProgress(string? jobName, BackupStatus status, int progression,
        string currentFile = "", bool blocked = false)
    {
        ProgressChanged?.Invoke(this, new BackupProgressEventArgs
        {
            JobName = jobName ?? string.Empty,
            Status = status,
            Progression = progression,
            CurrentFile = currentFile,
            BlockedByBusinessSoftware = blocked
        });
    }

    private bool IsBusinessSoftwareRunning(AppSettings settings, out string detectedProcess) =>
        _businessSoftwareMonitor.TryFindRunningBusinessSoftware(settings.BusinessSoftwareProcesses, out detectedProcess);

    private void LogBusinessSoftwareBlock(BackupJob job, string processName, LogService logService, string eventName)
    {
        logService.Save(new LogEntry
        {
            BackupName = job.Name ?? string.Empty,
            SourceFilePath = job.SourceDir ?? string.Empty,
            TargetFilePath = job.TargetDir ?? string.Empty,
            FileSize = 0,
            FileTransferTimeMs = 0,
            EncryptionTimeMs = 0,
            Event = string.IsNullOrWhiteSpace(processName) ? eventName : $"{eventName}:{processName}"
        });
    }
}