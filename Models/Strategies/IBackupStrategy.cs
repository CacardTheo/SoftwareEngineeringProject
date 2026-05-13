using EasyLog;
using EasySaveWpf.ViewModels;

namespace EasySaveWpf
{
    public interface IBackupStrategy
    {
        void Backup(
            BackupJob job,
            LogService logService,
            AppSettings settings,
            CryptoSoftService cryptoService,
            Action<string, string, long> onFileCopied,
            ManualResetEventSlim businessSoftwareGate,
            ManualResetEventSlim userPauseGate,
            CancellationToken cancellationToken,
            Action<string, string, long>? onBytesWritten = null);
    }
}
