using EasySaveWpf;

namespace EasySaveWpf.ViewModels;

public class BackupProcessor
{
    private readonly StateManager _stateManager;
    private readonly DailyLogManager _dailyLogManager;
    private readonly BusinessSoftwareMonitor _businessSoftwareMonitor;
    private readonly CryptoSoftService _cryptoSoftService;
    private readonly AppSettings _settings;

    public event Action<string, BackupStatus, int, string, bool>? ProgressChanged;

    public BackupProcessor(
        StateManager stateManager,
        DailyLogManager dailyLogManager,
        BusinessSoftwareMonitor businessSoftwareMonitor,
        CryptoSoftService cryptoSoftService,
        AppSettings settings)
    {
        _stateManager = stateManager;
        _dailyLogManager = dailyLogManager;
        _businessSoftwareMonitor = businessSoftwareMonitor;
        _cryptoSoftService = cryptoSoftService;
        _settings = settings;
    }

    public bool Execute(BackupJob job)
    {
        AppSettings settings = _settings;
        _stateManager.SetFormat(settings.StateFormat);

        if (IsBusinessSoftwareRunning(settings, out string detectedBeforeStart))
        {
            LogBusinessSoftwareBlock(job, detectedBeforeStart, settings.LogFormat, "BlockedBeforeStart");
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
            int lastReportedProgress = -1;

            strategy.Backup(job, _dailyLogManager, settings.LogFormat, settings, _cryptoSoftService,
                OnFileCopied,
                CanContinue);

            void OnFileCopied(string sourceFile, string destFile, long fileSize)
            {
                filesCopied++;
                bytesCopied += fileSize;
                int progression = 0;
                if (totalSize > 0)
                {
                    progression = (int)((bytesCopied * 100) / totalSize);
                }

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

                if (progression != lastReportedProgress && (progression % 5 == 0 || progression == 100))
                {
                    lastReportedProgress = progression;
                    RaiseProgress(job.Name, BackupStatus.In_Progress, progression, currentFile: sourceFile);
                }
                if (progression != lastReportedProgress && (progression % 5 == 0 || progression == 100))
                {
                    lastReportedProgress = progression;
                    RaiseProgress(job.Name, BackupStatus.In_Progress, progression, currentFile: sourceFile);
                }
            }

            bool CanContinue()
            {
                return !IsBusinessSoftwareRunning(settings, out _);
            }

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
                LogBusinessSoftwareBlock(job, detectedProcess, settings.LogFormat, "StoppedDuringExecution");
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

    private void LogBusinessSoftwareBlock(BackupJob job, string processName, OutputFormat format, string eventName)
    {
        _dailyLogManager.Save(new AppLogEntry
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            BackupName = job.Name ?? string.Empty,
            SourceFilePath = job.SourceDir ?? string.Empty,
            TargetFilePath = job.TargetDir ?? string.Empty,
            FileSize = 0,
            FileTransferTimeMs = 0,
            EncryptionTimeMs = 0,
            Event = string.IsNullOrWhiteSpace(processName)
                ? eventName
                : $"{eventName}:{processName}"
        }, format);
    }
}

