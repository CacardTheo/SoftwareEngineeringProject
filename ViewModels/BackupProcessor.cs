using SoftwareEngineeringProject;
using EasyLog;
public class BackupProcessor
{
    private IBackupStrategy strategy;

    // Uses EasyLog to log errors and info
    // private EasyLog logger;
    private StateManager stateManager;

    private readonly LogService _logService;

    public BackupProcessor(StateManager stateManager, ILogSerializer logSerializer)
    {
        this.stateManager = stateManager;
        _logService = new LogService(logSerializer);
    }

    public void Execute(BackupJob job)
    {
        // Select strategy from job type
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
            files = Directory.GetFiles(job.SourceDir, "*.*", SearchOption.AllDirectories);
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

        // State: Inactive
        stateManager.UpdateJobState(new StateEntry
        {
            Name = job.Name ?? "Unnamed Job",
            SourceFilePath = job.SourceDir ?? "Unknown Source",
            TargetFilePath = job.TargetDir ?? "Unknown Target",
            State = BackupStatus.Inactive,
            TotalFilesToCopy = totalFiles,
            TotalFilesSize = totalSize,
            NbFilesLeftToDo = totalFiles,
            Progression = 0,
            LastRun = DateTime.Now
        });

        try
        {
            // State: In Progress
            // Execute backup strategy with progress callback
            long bytesCopied = 0;
            int filesCopied = 0;

            strategy.Backup(job, _logService, (sourceFile, destFile, fileSize)  =>
            {
                filesCopied++;
                bytesCopied += fileSize;
                int progression = totalSize > 0 ? (int)((bytesCopied * 100) / totalSize) : 0;
                stateManager.UpdateJobState(new StateEntry
                {
                    Name = job.Name ?? "Unnamed Job",
                    SourceFilePath = job.SourceDir ?? "Unknown Source",
                    TargetFilePath = job.TargetDir ?? "Unknown Target",
                    State = BackupStatus.In_Progress,
                    TotalFilesToCopy = totalFiles,
                    TotalFilesSize = totalSize,
                    NbFilesLeftToDo = totalFiles,
                    Progression = progression,
                    LastRun = DateTime.Now
                });
            });


                // State: Ended
                stateManager.UpdateJobState(new StateEntry
                {
                    Name = job.Name ?? "Unnamed Job",
                    SourceFilePath = job.SourceDir ?? "Unknown Source",
                    TargetFilePath = job.TargetDir ?? "Unknown Target",
                    State = BackupStatus.Ended,
                    TotalFilesToCopy = totalFiles,
                    TotalFilesSize = totalSize,
                    NbFilesLeftToDo = 0,
                    Progression = 100,
                    LastRun = DateTime.Now
                });
            }
        catch
        {
            // On error, update state accordingly
            stateManager.UpdateJobState(new StateEntry
            {
                Name = job.Name ?? "Unnamed Job",
                SourceFilePath = job.SourceDir ?? "Unknown Source",
                TargetFilePath = job.TargetDir ?? "Unknown Target",
                State = BackupStatus.Error,
                TotalFilesToCopy = 0,
                TotalFilesSize = 0,
                NbFilesLeftToDo = 0,
                Progression = 0,
                LastRun = DateTime.Now
            });

            // logger.Log(ex.Message);

            throw;
        }
    }
}