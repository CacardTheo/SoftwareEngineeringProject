using System.Diagnostics;
using EasyLog;
using EasySaveWpf.ViewModels;

namespace EasySaveWpf
{
    public class FullBackupStrategy : BackupStrategyBase
    {
        public FullBackupStrategy(BackupSyncContext context) : base(context) { }

        public override void Backup(BackupJob job, LogService logService, AppSettings settings, CryptoSoftService cryptoService, Action<string, string, long> onFileCopied, Func<bool> canCopyNextFile, Action<string, string, long>? onBytesWritten = null)
        {
            if (string.IsNullOrEmpty(job.SourceDir) || string.IsNullOrEmpty(job.TargetDir))
                throw new ArgumentException(_languageManager.GetText("log_error_missing_paths"));

            try
            {
                if (!File.Exists(job.SourceDir) && !Directory.Exists(job.SourceDir))
                    throw new DirectoryNotFoundException(_languageManager.GetText("log_error_source_not_found"));

                if (File.Exists(job.SourceDir))
                {
                    if (!canCopyNextFile())
                        throw new InvalidOperationException("BUSINESS_SOFTWARE_DETECTED");

                    CopySingleFile(job.SourceDir, job.TargetDir, job, logService, settings, cryptoService, onFileCopied, onBytesWritten);
                    return;
                }

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

                CopyGroup(prioritized, isPriorityGroup: true,  job, logService, settings, cryptoService, onFileCopied, canCopyNextFile, onBytesWritten, shouldCopy: (f, t) => true);
                CopyGroup(regular,     isPriorityGroup: false, job, logService, settings, cryptoService, onFileCopied, canCopyNextFile, onBytesWritten, shouldCopy: (f, t) => true);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException(_languageManager.GetText("log_error_access_denied") + ex.Message, ex);
            }
        }

        private static void CopySingleFile(string sourcePath, string targetDir, BackupJob job, LogService logService, AppSettings settings, CryptoSoftService cryptoService, Action<string, string, long> onFileCopied, Action<string, string, long>? onBytesWritten = null)
        {
            FileInfo fileInfo = new FileInfo(sourcePath);
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            string targetPath = Path.Combine(targetDir, fileInfo.Name);

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
