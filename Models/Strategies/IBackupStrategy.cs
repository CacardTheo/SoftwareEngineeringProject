namespace EasySaveWpf
{
    public interface IBackupStrategy
    {
        /// <summary>
        /// Executes the backup logic per-file, checks business software before each file,
        /// optionally encrypts files, and logs each action in real-time.
        /// </summary>
        /// <param name="job">The backup job details.</param>
        /// <param name="context">Execution context (logging, settings, crypto, callbacks).</param>
        void Backup(BackupJob job, BackupExecutionContext context);
    }
}