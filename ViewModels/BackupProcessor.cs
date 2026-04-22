using SoftwareEngineeringProject;

public class BackupProcessor
{
    private IBackupStrategy strategy;

    // Use this with the EasyLog dll
    // private EasyLog logger;
    private StateManager stateManager;

    public void Execute(BackupJob job)
    {
        //strategy.Backup(job);
    }
}