using EasyLog;

namespace SoftwareEngineeringProject
{
    public interface IBackupStrategy
    {
        /// <summary>
        /// Executes the backup logic and logs each file action in real-time.
        /// </summary>
        /// <param name="job">The backup job details.</param>
        /// <param name="logService">The logging service from EasyLog.dll.</param>
        /// <param name="onFileCopied">The callback action to invoke when a file is copied.</param>
        void Backup(BackupJob job, LogService logService, Action<string, string, long> onFileCopied);
    }
}