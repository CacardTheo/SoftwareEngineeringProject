using EasyLog;
using EasySaveWpf;
using EasySaveWpf.Services;
using EasySaveWpf.ViewModels;
using EasySaveWpf.Utils;

namespace EasySaveWpf
{
    public class DifferentialBackupStrategy : BackupStrategyBase
    {
        public DifferentialBackupStrategy(BackupSyncContext context) : base(context) { }
        public override void Backup(
            BackupJob job,
            LogService logService,
            AppSettings settings,
            CryptoSoftService cryptoService,
            Action<string, string, long> onFileCopied,
            ManualResetEventSlim businessSoftwareGate,
            ManualResetEventSlim userPauseGate,
            CancellationToken cancellationToken,
            Action<string, string, long>? onBytesWritten = null)
        {
            if (string.IsNullOrEmpty(job.SourceDir) || string.IsNullOrEmpty(job.TargetDir))
                throw new ArgumentException(_languageManager.GetText("log_error_missing_paths"));

            if (!File.Exists(job.SourceDir) && !Directory.Exists(job.SourceDir))
                throw new DirectoryNotFoundException(_languageManager.GetText("log_error_source_not_found"));

            if (File.Exists(job.SourceDir))
            {
                static bool IsNewer(FileInfo f, string t) => !File.Exists(t) || f.LastWriteTime > File.GetLastWriteTime(t);
                CopySingleFile(job.SourceDir, job.TargetDir, job, logService, settings, cryptoService,
                    onFileCopied, businessSoftwareGate, userPauseGate, cancellationToken, onBytesWritten, shouldCopy: IsNewer);
                return;
            }

            if (!Directory.Exists(job.TargetDir))
                Directory.CreateDirectory(job.TargetDir);

            DirectoryInfo sourceInfo = new(job.SourceDir);
            FileInfo[] files;
            try
            {
                files = sourceInfo.GetFiles("*.*", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                throw new IOException(_languageManager.GetText("error_finding_files") + ex.Message, ex);
            }

            var prioritized = files.Where(f => settings.PrioritizedExtensions.Contains(f.Extension.ToLowerInvariant())).ToList();
            var regular = files.Except(prioritized).ToList();

            _context.RegisterPriorityFiles(prioritized.Count);

            static bool ShouldCopy(FileInfo f, string t) =>
                !File.Exists(t) || f.LastWriteTime > File.GetLastWriteTime(t);
            CopyGroup(prioritized, isPriorityGroup: true,  job, logService, settings, cryptoService,
                onFileCopied, businessSoftwareGate, userPauseGate, cancellationToken, onBytesWritten, shouldCopy: ShouldCopy);
            CopyGroup(regular,     isPriorityGroup: false, job, logService, settings, cryptoService,
                onFileCopied, businessSoftwareGate, userPauseGate, cancellationToken, onBytesWritten, shouldCopy: ShouldCopy);
        }
    }
}
