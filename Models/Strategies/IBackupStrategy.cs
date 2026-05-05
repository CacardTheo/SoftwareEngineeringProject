using EasySaveWpf.ViewModels;

namespace EasySaveWpf
{
    public interface IBackupStrategy
    {
        /// <summary>
        /// Executes the backup logic per-file, checks business software before each file,
        /// optionally encrypts files, and logs each action in real-time.
        /// </summary>
        /// <param name="job">The backup job details.</param>
        /// <param name="logManager">The daily log manager.</param>
        /// <param name="logFormat">The output format for logs.</param>
        /// <param name="settings">The app settings.</param>
        /// <param name="cryptoService">The crypto service.</param>
        /// <param name="onFileCopied">Callback for file copied.</param>
        /// <param name="canCopyNextFile">Callback to check if can copy next file.</param>
        void Backup(BackupJob job, DailyLogManager logManager, OutputFormat logFormat, AppSettings settings, CryptoSoftService cryptoService, Action<string, string, long> onFileCopied, Func<bool> canCopyNextFile);
    }
}