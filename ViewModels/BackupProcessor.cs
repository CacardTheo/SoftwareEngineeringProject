public class BackupProcessor
{
    private IBackupStrategy strategy;
    private EasyLog logger;
    private StateManager stateManager;

    public void Execute(BackupJob job)
    {
        //strategy.Backup(job);
    }
}