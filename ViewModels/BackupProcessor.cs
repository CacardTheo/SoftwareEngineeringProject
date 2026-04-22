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

        // State: Started
        stateManager.Update(new StateEntry
        {
            JobName = job.Name,
            Status = BackupStatus.Started,
            Progress = 0,
            LastRun = DateTime.Now
        });

        try
        {
            // State: In Progress
            stateManager.Update(new StateEntry
            {
                JobName = job.Name,
                Status = BackupStatus.In_Progress,
                Progress = 0,
                LastRun = DateTime.Now
            });

            // Execute backup strategy
            strategy.Backup(job);

            // State: Ended
            stateManager.Update(new StateEntry
            {
                JobName = job.Name,
                Status = BackupStatus.Ended,
                Progress = 100,
                LastRun = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            // On error, update state accordingly
            stateManager.Update(new StateEntry
            {
                JobName = job.Name,
                Status = BackupStatus.Error,
                Progress = 0,
                LastRun = DateTime.Now
            });

            // logger.Log(ex.Message);

            throw;
        }
    }
}