using EasyLog;

namespace SoftwareEngineeringProject.ViewModels
{
    public class BackupProcessor
    {
        // The service from EasyLog.dll used for real-time logging
        private readonly LogService _logService = new LogService();

        public void Execute(BackupJob job, IBackupStrategy strategy)
        {
            // We pass the log service to the strategy.
            // The strategy will now handle logging each file transfer 
            // in real-time with specific transfer times and error handling.
            strategy.Backup(job, _logService);
        }
    }
}