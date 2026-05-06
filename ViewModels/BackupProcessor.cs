using EasySaveWpf;
using EasyLog;

namespace EasySaveWpf.ViewModels;

public class BackupProcessor
{
    private readonly StateManager _stateManager;
    private readonly BusinessSoftwareMonitor _businessSoftwareMonitor;
    private readonly CryptoSoftService _cryptoSoftService;
    private readonly AppSettings _settings;

    public event Action<string, BackupStatus, int, string, bool>? ProgressChanged;

    public BackupProcessor(
        StateManager stateManager,
        BusinessSoftwareMonitor businessSoftwareMonitor,
        CryptoSoftService cryptoSoftService,
        AppSettings settings)
    {
        _stateManager = stateManager;
        _businessSoftwareMonitor = businessSoftwareMonitor;
        _cryptoSoftService = cryptoSoftService;
        _settings = settings;
    }

    public bool Execute(BackupJob job)
    {
        AppSettings settings = _settings;
        _stateManager.SetFormat(settings.StateFormat);
        var logService = new LogService(settings.LogFormat);

        if (IsBusinessSoftwareRunning(settings, out string detectedBeforeStart))
        {
            LogBusinessSoftwareBlock(job, detectedBeforeStart, logService, "BlockedBeforeStart");
            RaiseProgress(job.Name, BackupStatus.Inactive, 0, blocked: true);
            return false;
        }

        IBackupStrategy strategy;

        switch (job.Type)
        {
            case BackupType.Full:
                strategy = new FullBackupStrategy();
                break;
            case BackupType.Differential:
                strategy = new DifferentialBackupStrategy();
                break;
            default:
                throw new ArgumentException($"Unknown backup type: {job.Type}");
        }

        string[] files;
        try
        {
            if (File.Exists(job.SourceDir))
                files = new string[] { job.SourceDir };
            else
                files = Directory.GetFiles(job.SourceDir ?? string.Empty, "*.*", SearchOption.AllDirectories);
        }
        catch
        {
            files = Array.Empty<string>();
        }

        int totalFiles = files.Length;
        long totalSize = 0;

        foreach (string file in files)
        {
            totalSize += new FileInfo(file).Length;
        }

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
            int lastReportedProgression = -1;

            strategy.Backup(job, logService, settings, _cryptoSoftService, OnFileCopied, CanContinue, OnBytesWritten);

            void OnBytesWritten(string sourceFile, string destFile, long bytes)
            {
                bytesCopied += bytes;
                int progression = totalSize > 0 ? (int)(bytesCopied * 100 / totalSize) : 0;
                if (progression == lastReportedProgression) return;
                lastReportedProgression = progression;

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
            }

            void OnFileCopied(string sourceFile, string destFile, long fileSize)
            {
                filesCopied++;
                int progression = totalSize > 0 ? (int)(bytesCopied * 100 / totalSize) : 0;
                lastReportedProgression = progression;

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
            }

            bool CanContinue() => !IsBusinessSoftwareRunning(settings, out _);

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
        catch (InvalidOperationException ex)
        {
            if (ex.Message == "BUSINESS_SOFTWARE_DETECTED")
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

            throw;
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
        ProgressChanged?.Invoke(jobName ?? string.Empty, status, progression, currentFile, blocked);
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
