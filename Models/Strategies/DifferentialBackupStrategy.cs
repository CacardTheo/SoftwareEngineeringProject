using System.Diagnostics;
using EasyLog;
using EasySaveWpf.ViewModels;

namespace EasySaveWpf
{
    public class DifferentialBackupStrategy : BackupStrategyBase
    {
        public DifferentialBackupStrategy(BackupSyncContext context) : base(context) { }

        public override void Backup(BackupJob job, LogService logService, AppSettings settings, CryptoSoftService cryptoService, Action<string, string, long> onFileCopied, Func<bool> canCopyNextFile, Action<string, string, long>? onBytesWritten = null)
        {
            if (string.IsNullOrEmpty(job.SourceDir) || string.IsNullOrEmpty(job.TargetDir))
                throw new ArgumentException(_languageManager.GetText("log_error_missing_paths"));

            if (!File.Exists(job.SourceDir) && !Directory.Exists(job.SourceDir))
                throw new DirectoryNotFoundException(_languageManager.GetText("log_error_source_not_found"));

            if (File.Exists(job.SourceDir))
            {
                if (!canCopyNextFile())
                    throw new InvalidOperationException("BUSINESS_SOFTWARE_DETECTED");

                CopySingleFile(job.SourceDir, job.TargetDir, job, logService, settings, cryptoService, onFileCopied, onBytesWritten);
                return;
            }

            if (!Directory.Exists(job.TargetDir))
                Directory.CreateDirectory(job.TargetDir);

            DirectoryInfo sourceInfo = new DirectoryInfo(job.SourceDir);
            FileInfo[] files;
            try
            {
                files = sourceInfo.GetFiles("*.*", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                throw new IOException(_languageManager.GetText("error_finding_files") + ex.Message, ex);
            }

            var prioritized = files.Where(f => settings.PrioritizedExtensions.Contains(f.Extension.ToLower())).ToList();
            var regular = files.Except(prioritized).ToList();

            _context.RegisterPriorityFiles(prioritized.Count);

            static bool ShouldCopy(FileInfo f, string t) =>
                !File.Exists(t) || f.LastWriteTime > File.GetLastWriteTime(t);

            CopyGroup(prioritized, isPriorityGroup: true,  job, logService, settings, cryptoService, onFileCopied, canCopyNextFile, onBytesWritten, shouldCopy: ShouldCopy);
            CopyGroup(regular,     isPriorityGroup: false, job, logService, settings, cryptoService, onFileCopied, canCopyNextFile, onBytesWritten, shouldCopy: ShouldCopy);
        }

        private static void CopySingleFile(string sourcePath, string targetDir, BackupJob job, LogService logService, AppSettings settings, CryptoSoftService cryptoService, Action<string, string, long> onFileCopied, Action<string, string, long>? onBytesWritten = null)
        {
            FileInfo fileInfo = new(sourcePath);
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            string targetPath = Path.Combine(targetDir, fileInfo.Name);

            if (!File.Exists(targetPath) || fileInfo.LastWriteTime > File.GetLastWriteTime(targetPath))
            {
                Stopwatch sw = Stopwatch.StartNew();
                FileHelper.CopyFile(sourcePath, targetPath, onBytesWritten);
                sw.Stop();

                long encryptionTime = TryEncrypt(targetPath, fileInfo.Extension, cryptoService, settings);
                onFileCopied(sourcePath, targetPath, fileInfo.Length);

                logService.Save(new LogEntry
                {
                    BackupName = job.Name ?? string.Empty,
                    SourceFilePath = sourcePath,
                    TargetFilePath = targetPath,
                    FileSize = fileInfo.Length,
                    FileTransferTimeMs = sw.ElapsedMilliseconds,
                    EncryptionTimeMs = encryptionTime,
                    Event = "FileCopied"
                });
            }
        }
    }
}
