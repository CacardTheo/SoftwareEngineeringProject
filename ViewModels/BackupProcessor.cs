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

        // State: Inactive
        stateManager.UpdateJobState(new StateEntry
        {
            Name = job.Name,
            SourceFilePath = job.SourceDir,
            TargetFilePath = job.TargetDir,
            State = BackupStatus.Inactive,
            TotalFilesToCopy = 0,
            TotalFilesSize = 0,
            NbFilesLeftToDo = 0,
            Progression = 0,
            LastRun = DateTime.Now
        });

        try
        {
            // State: In Progress
            stateManager.UpdateJobState(new StateEntry
            {
                Name = job.Name,
                SourceFilePath = job.SourceDir,
                TargetFilePath = job.TargetDir,
                State = BackupStatus.In_Progress,
                TotalFilesToCopy = 0,
                TotalFilesSize = 0,
                NbFilesLeftToDo = 0,
                Progression = 0,
                LastRun = DateTime.Now
            });

            // Execute backup strategy
            strategy.Backup(job);

            // State: Ended
            stateManager.UpdateJobState(new StateEntry
            {
                Name = job.Name,
                SourceFilePath = job.SourceDir,
                TargetFilePath = job.TargetDir,
                State = BackupStatus.Ended,
                TotalFilesToCopy = 0,
                TotalFilesSize = 0,
                NbFilesLeftToDo = 0,
                Progression = 0,
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