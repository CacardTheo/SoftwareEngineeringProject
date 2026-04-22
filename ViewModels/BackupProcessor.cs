using SoftwareEngineeringProject;

public class BackupProcessor
{
    private IBackupStrategy strategy;

    // Uses EasyLog to log errors and info
    // private EasyLog logger;
    private StateManager stateManager;

    public BackupProcessor(StateManager stateManager)
    {
        this.stateManager = stateManager;
    }

    public void Execute(BackupJob job)
    {
        // Select strategy from job type
        switch (job.Type)
        {
            case BackupType.Full:
                strategy = new FullBackup();
                break;
            case BackupType.Differential:
                strategy = new DifferentialBackup();
                break;
            default:
                throw new ArgumentException($"Unknown backup type: {job.Type}");
        }

        // Calculate file stats before starting
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

        // State: Inactive (before starting)
        stateManager.UpdateJobState(new StateEntry
        {
            Name = job.Name,
            SourceFilePath = job.SourceDir,
            TargetFilePath = job.TargetDir,
            State = BackupStatus.Inactive,
            TotalFilesToCopy = totalFiles,
            TotalFilesSize = totalSize,
            NbFilesLeftToDo = totalFiles,
            Progression = 0,
            LastRun = DateTime.Now
        });

        try
        {
            // Execute backup strategy with progress callback
            long bytesCopied = 0;
            int filesCopied = 0;

            strategy.Backup(job, (sourceFile, destFile, fileSize) =>
            {
                filesCopied++;
                bytesCopied += fileSize;
                int progression = totalSize > 0 ? (int)((bytesCopied * 100) / totalSize) : 0;

                // Update state for each file copied
                stateManager.UpdateJobState(new StateEntry
                {
                    Name = job.Name,
                    SourceFilePath = sourceFile,
                    TargetFilePath = destFile,
                    State = BackupStatus.In_Progress,
                    TotalFilesToCopy = totalFiles,
                    TotalFilesSize = totalSize,
                    NbFilesLeftToDo = totalFiles - filesCopied,
                    Progression = progression,
                    LastRun = DateTime.Now
                });
            });

            // State: Ended
            stateManager.UpdateJobState(new StateEntry
            {
                Name = job.Name,
                SourceFilePath = job.SourceDir,
                TargetFilePath = job.TargetDir,
                State = BackupStatus.Ended,
                TotalFilesToCopy = totalFiles,
                TotalFilesSize = totalSize,
                NbFilesLeftToDo = 0,
                Progression = 100,
                LastRun = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            // On error, update state accordingly
            stateManager.UpdateJobState(new StateEntry
            {
                Name = job.Name,
                SourceFilePath = job.SourceDir,
                TargetFilePath = job.TargetDir,
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