using EasySaveWpf;
using EasyLog;
using System.Threading;

namespace EasySaveWpf.ViewModels;


public class BackupProcessor
{
    private readonly StateManager _stateManager;
    private readonly BusinessSoftwareMonitor _businessSoftwareMonitor;
    private readonly CryptoSoftService _cryptoSoftService;
    private readonly AppSettings _settings;
    private readonly ManualResetEventSlim _businessSoftwareGate;


    public event Action<string, BackupStatus, int, string, bool, string>? ProgressChanged;

    public BackupProcessor(
        StateManager stateManager,
        BusinessSoftwareMonitor businessSoftwareMonitor,
        CryptoSoftService cryptoSoftService,
        AppSettings settings,
        ManualResetEventSlim businessSoftwareGate)
    {
        _stateManager = stateManager;
        _businessSoftwareMonitor = businessSoftwareMonitor;
        _cryptoSoftService = cryptoSoftService;
        _settings = settings;
        _businessSoftwareGate = businessSoftwareGate;
    }

    public bool Execute(BackupJob job, BackupSyncContext syncContext, ManualResetEventSlim userPauseGate, CancellationToken cancellationToken)
    {
        AppSettings settings = _settings;
        _stateManager.SetFormat(settings.StateFormat);
        var logService = new LogService(settings.LogFormat);

        IBackupStrategy strategy = job.Type switch
        {
            BackupType.Full => new FullBackupStrategy(syncContext),
            BackupType.Differential => new DifferentialBackupStrategy(syncContext),
            _ => throw new ArgumentException($"Unknown backup type: {job.Type}")
        };

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
            totalSize += new FileInfo(file).Length;

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

        long bytesCopied = 0;
        int filesCopied = 0;
        int lastReportedProgression = -1;

        try
        {
            strategy.Backup(job, logService, settings, _cryptoSoftService,
                OnFileCopied, _businessSoftwareGate, userPauseGate, cancellationToken, OnBytesWritten);

            void OnBytesWritten(string sourceFile, string destFile, long bytes)
            {
                long current = Interlocked.Add(ref bytesCopied, bytes);
                int progression = totalSize > 0 ? (int)(current * 100 / totalSize) : 0;
                if (progression == Volatile.Read(ref lastReportedProgression)) return;
                Volatile.Write(ref lastReportedProgression, progression);

                int fc = Volatile.Read(ref filesCopied);
                long bc = Volatile.Read(ref bytesCopied);
                _stateManager.UpdateJobState(new StateEntry
                {
                    Name = job.Name ?? "Unnamed Job",
                    SourceFilePath = sourceFile,
                    TargetFilePath = destFile,
                    State = BackupStatus.In_Progress,
                    TotalFilesToCopy = totalFiles,
                    TotalFilesSize = totalSize,
                    NbFilesLeftToDo = Math.Max(0, totalFiles - fc),
                    SizeRemaining = Math.Max(0, totalSize - bc),
                    Progression = progression,
                    LastRun = DateTime.Now
                });
                RaiseProgress(job.Name, BackupStatus.In_Progress, progression, currentFile: sourceFile);
            }

            void OnFileCopied(string sourceFile, string destFile, long fileSize)
            {
                int fc = Interlocked.Increment(ref filesCopied);
                long bc = Volatile.Read(ref bytesCopied);
                int progression = totalSize > 0 ? (int)(bc * 100 / totalSize) : 0;
                Volatile.Write(ref lastReportedProgression, progression);

                _stateManager.UpdateJobState(new StateEntry
                {
                    Name = job.Name ?? "Unnamed Job",
                    SourceFilePath = sourceFile,
                    TargetFilePath = destFile,
                    State = BackupStatus.In_Progress,
                    TotalFilesToCopy = totalFiles,
                    TotalFilesSize = totalSize,
                    NbFilesLeftToDo = Math.Max(0, totalFiles - fc),
                    SizeRemaining = Math.Max(0, totalSize - bc),
                    Progression = progression,
                    LastRun = DateTime.Now
                });
                RaiseProgress(job.Name, BackupStatus.In_Progress, progression, currentFile: sourceFile);
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
        catch (OperationCanceledException)
        {
            long bc = Volatile.Read(ref bytesCopied);
            int fc = Volatile.Read(ref filesCopied);
            _stateManager.UpdateJobState(new StateEntry
            {
                Name = job.Name ?? "Unnamed Job",
                SourceFilePath = job.SourceDir ?? string.Empty,
                TargetFilePath = job.TargetDir ?? string.Empty,
                State = BackupStatus.Inactive,
                TotalFilesToCopy = totalFiles,
                TotalFilesSize = totalSize,
                NbFilesLeftToDo = Math.Max(0, totalFiles - fc),
                SizeRemaining = Math.Max(0, totalSize - bc),
                Progression = totalSize > 0 ? (int)(bc * 100 / totalSize) : 0,
                LastRun = DateTime.Now
            });
            RaiseProgress(job.Name, BackupStatus.Inactive, 0);
            return false;
        }
        catch (Exception ex)
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

            logService.Save(new LogEntry
            {
                BackupName = job.Name ?? string.Empty,
                SourceFilePath = job.SourceDir ?? string.Empty,
                TargetFilePath = string.Empty,
                FileSize = 0,
                FileTransferTimeMs = -1,
                EncryptionTimeMs = 0,
                Event = $"Error:{ex.GetType().Name}:{ex.Message}"
            });

            RaiseProgress(job.Name, BackupStatus.Error, 0, errorMessage: ex.Message);
            throw;
        }
    }

    private void RaiseProgress(string? jobName, BackupStatus status, int progression,
        string currentFile = "", bool blocked = false, string errorMessage = "")
    {
        ProgressChanged?.Invoke(jobName ?? string.Empty, status, progression, currentFile, blocked, errorMessage);
    }

}
