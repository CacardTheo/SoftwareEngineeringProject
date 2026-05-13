using EasySaveWpf.Services;
using EasySaveWpf.ViewModels;

namespace EasySaveWpf
{
    public interface IBackupStrategy
    {
        void Backup(BackupJob job, BackupLogRouter logRouter, AppSettings settings, CryptoSoftService cryptoService, Action<string, string, long> onFileCopied, Func<bool> canCopyNextFile, Action<string, string, long>? onBytesWritten = null);
    }
}