using System.Diagnostics;
using EasyLog;
using EasySaveWpf.Utils;
using EasySaveWpf.ViewModels;

namespace EasySaveWpf
{
    public abstract class BackupStrategyBase : IBackupStrategy
    {
        protected readonly LanguageManager _languageManager;
        protected readonly BackupSyncContext _context;
        private readonly Lock _lock = new();

        protected BackupStrategyBase(BackupSyncContext context)
        {
            _languageManager = LanguageManager.GetInstance();
            _context = context;
        }

        public abstract void Backup(BackupJob job, LogService logService, AppSettings settings, CryptoSoftService cryptoService, Action<string, string, long> onFileCopied, Func<bool> canCopyNextFile, Action<string, string, long>? onBytesWritten = null);

        // Copies all files in the group in parallel.
        // isPriorityGroup: priority threads notify the cross-job barrier when done;
        //                  non-priority threads wait for ALL jobs' priority files first.
        // shouldCopy: returns false to skip a file (used by differential backup).
        protected void CopyGroup(
            List<FileInfo> group,
            bool isPriorityGroup,
            BackupJob job,
            LogService logService,
            AppSettings settings,
            CryptoSoftService cryptoService,
            Action<string, string, long> onFileCopied,
            Func<bool> canCopyNextFile,
            Action<string, string, long>? onBytesWritten,
            Func<FileInfo, string, bool> shouldCopy)
        {
            int cancelled = 0;
            var threads = new List<Thread>();

            foreach (FileInfo filePath in group)
            {
                FileInfo captured = filePath; // local capture to avoid closure bug
                var thread = new Thread(() =>
                {
                    try
                    {
                        if (Volatile.Read(ref cancelled) != 0) return;

                        if (!isPriorityGroup)
                        {
                            // Block until every priority file across ALL jobs is done.
                            _context.WaitForAllPriorityFiles();
                            if (Volatile.Read(ref cancelled) != 0) return;
                        }

                        if (!canCopyNextFile())
                        {
                            Volatile.Write(ref cancelled, 1);
                            return;
                        }

                        string targetPath = captured.FullName.Replace(job.SourceDir, job.TargetDir);

                        if (!shouldCopy(captured, targetPath)) return;

                        Stopwatch sw = Stopwatch.StartNew();

                        bool isLargeFile = _context.LargeFileSizeThresholdBytes > 0
                                           && captured.Length > _context.LargeFileSizeThresholdBytes;
                        if (isLargeFile) _context.AcquireLargeFileSlot();
                        try
                        {
                            string? dir = Path.GetDirectoryName(targetPath);
                            if (dir != null) Directory.CreateDirectory(dir);
                            FileHelper.CopyFile(captured.FullName, targetPath, onBytesWritten);
                            sw.Stop();
                        }
                        finally
                        {
                            if (isLargeFile) _context.ReleaseLargeFileSlot();
                        }

                        long encryptionTime = TryEncrypt(targetPath, captured.Extension, cryptoService, settings);

                        lock (_lock)
                        {
                            onFileCopied(captured.FullName, targetPath, captured.Length);
                            logService.Save(new LogEntry
                            {
                                BackupName = job.Name ?? string.Empty,
                                SourceFilePath = captured.FullName,
                                TargetFilePath = targetPath,
                                FileSize = captured.Length,
                                FileTransferTimeMs = sw.ElapsedMilliseconds,
                                EncryptionTimeMs = encryptionTime,
                                Event = "FileCopied"
                            });
                        }
                    }
                    catch (Exception)
                    {
                        lock (_lock)
                        {
                            logService.Save(new LogEntry
                            {
                                BackupName = job.Name ?? string.Empty,
                                SourceFilePath = captured.FullName,
                                TargetFilePath = "ERROR",
                                FileSize = captured.Length,
                                FileTransferTimeMs = -1,
                                EncryptionTimeMs = 0,
                                Event = "CopyError"
                            });
                        }
                    }
                    finally
                    {
                        // Always notify so waiting non-priority threads are never stuck.
                        if (isPriorityGroup) _context.NotifyPriorityFileDone();
                    }
                });

                threads.Add(thread);
            }

            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join(); // barrier: wait for all threads in the group

            if (Volatile.Read(ref cancelled) != 0)
                throw new InvalidOperationException("BUSINESS_SOFTWARE_DETECTED");
        }

        protected static long TryEncrypt(string targetPath, string extension, CryptoSoftService cryptoService, AppSettings settings)
        {
            foreach (string ext in settings.EncryptedExtensions)
            {
                if (ext.Equals(extension, StringComparison.OrdinalIgnoreCase))
                    return cryptoService.Encrypt(targetPath, settings.EncryptionKey);
            }
            return 0;
        }

        // shouldCopy: returns false to skip the file (used for differential strategy).
        protected static void CopySingleFile(
            string sourcePath,
            string targetDir,
            BackupJob job,
            LogService logService,
            AppSettings settings,
            CryptoSoftService cryptoService,
            Action<string, string, long> onFileCopied,
            Action<string, string, long>? onBytesWritten,
            Func<FileInfo, string, bool>? shouldCopy = null)
        {
            FileInfo fileInfo = new(sourcePath);
            Directory.CreateDirectory(targetDir);
            string targetPath = Path.Combine(targetDir, fileInfo.Name);

            if (shouldCopy != null && !shouldCopy(fileInfo, targetPath)) return;

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
