using EasySaveWpf.Services;
using EasySaveWpf.ViewModels;

namespace EasySaveWpf
{
    public interface IBackupStrategy
    {
        void Backup(
            BackupJob job,
            BackupLogRouter logService,
            AppSettings settings,
            CryptoSoftService cryptoService,
            Action<string, string, long> onFileCopied,
            ManualResetEventSlim businessSoftwareGate,
            ManualResetEventSlim userPauseGate,
            CancellationToken cancellationToken,
            Action<string, string, long>? onBytesWritten = null);
    }
}
