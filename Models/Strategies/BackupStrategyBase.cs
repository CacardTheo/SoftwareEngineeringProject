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

        public abstract void Backup(
            BackupJob job,
            LogService logService,
            AppSettings settings,
            CryptoSoftService cryptoService,
            Action<string, string, long> onFileCopied,
            ManualResetEventSlim businessSoftwareGate,
            ManualResetEventSlim userPauseGate,
            CancellationToken cancellationToken,
            Action<string, string, long>? onBytesWritten = null);

        // Copies all files in the group in parallel.
        // - Priority threads notify the cross-job barrier when done.
        // - Non-priority threads wait for ALL jobs' priority files before starting.
        // - Pause (user or business software) is checked before starting each file thread.
        // - Stop (cancellation) is checked per 80 KB chunk inside each thread.
        // - Partial files are deleted on cancellation.
        protected void CopyGroup(
            List<FileInfo> group,
            bool isPriorityGroup,
            BackupJob job,
            LogService logService,
            AppSettings settings,
            CryptoSoftService cryptoService,
            Action<string, string, long> onFileCopied,
            ManualResetEventSlim businessSoftwareGate,
            ManualResetEventSlim userPauseGate,
            CancellationToken cancellationToken,
            Action<string, string, long>? onBytesWritten,
            Func<FileInfo, string, bool> shouldCopy)
        {
            var threads = new List<Thread>();

            foreach (FileInfo filePath in group)
            {
                // Pause at file boundary: wait for both gates before starting each file.
                cancellationToken.ThrowIfCancellationRequested();
                WaitForGates(businessSoftwareGate, userPauseGate, cancellationToken);

                FileInfo captured = filePath;
                var thread = new Thread(() =>
                {
                    string? currentTargetFile = null;
                    bool isLargeFile = false;
                    try
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        if (!isPriorityGroup)
                        {
                            // Block until every priority file across ALL jobs is done.
                            _context.WaitForAllPriorityFiles(cancellationToken);
                            if (cancellationToken.IsCancellationRequested) return;
                        }

                        string targetPath = captured.FullName.Replace(job.SourceDir!, job.TargetDir!);

                        if (!shouldCopy(captured, targetPath)) return;

                        string? dir = Path.GetDirectoryName(targetPath);
                        if (dir != null) Directory.CreateDirectory(dir);

                        Stopwatch sw = Stopwatch.StartNew();

                        isLargeFile = _context.LargeFileSizeThresholdBytes > 0
                                      && captured.Length > _context.LargeFileSizeThresholdBytes;
                        if (isLargeFile) _context.AcquireLargeFileSlot();
                        try
                        {
                            // Wrap callback: check stop (cancellation) per chunk.
                            void OnChunk(string src, string dest, long bytes)
                            {
                                currentTargetFile = dest;
                                cancellationToken.ThrowIfCancellationRequested();
                                onBytesWritten?.Invoke(src, dest, bytes);
                            }
                            FileHelper.CopyFile(captured.FullName, targetPath, OnChunk);
                            sw.Stop();
                        }
                        catch (OperationCanceledException)
                        {
                            sw.Stop();
                            if (currentTargetFile != null && File.Exists(currentTargetFile))
                                try { File.Delete(currentTargetFile); } catch { }
                            return; // swallow inside thread; caller checks token after Join
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
                    catch (OperationCanceledException) { /* swallow; caller checks token after Join */ }
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
                        if (isPriorityGroup) _context.NotifyPriorityFileDone();
                    }
                });

                threads.Add(thread);
                thread.Start();
            }

            foreach (var t in threads) t.Join();

            // Re-throw if cancelled (threads swallowed OCE internally).
            cancellationToken.ThrowIfCancellationRequested();
        }

        protected static void CopySingleFile(
            string sourcePath,
            string targetDir,
            BackupJob job,
            LogService logService,
            AppSettings settings,
            CryptoSoftService cryptoService,
            Action<string, string, long> onFileCopied,
            ManualResetEventSlim businessSoftwareGate,
            ManualResetEventSlim userPauseGate,
            CancellationToken cancellationToken,
            Action<string, string, long>? onBytesWritten,
            Func<FileInfo, string, bool>? shouldCopy = null)
        {
            WaitForGates(businessSoftwareGate, userPauseGate, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            FileInfo fileInfo = new(sourcePath);
            Directory.CreateDirectory(targetDir);
            string targetPath = Path.Combine(targetDir, fileInfo.Name);

            if (shouldCopy != null && !shouldCopy(fileInfo, targetPath)) return;

            string? currentTargetFile = null;
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                void OnChunk(string src, string dest, long bytes)
                {
                    currentTargetFile = dest;
                    cancellationToken.ThrowIfCancellationRequested();
                    onBytesWritten?.Invoke(src, dest, bytes);
                }
                FileHelper.CopyFile(sourcePath, targetPath, OnChunk);
                sw.Stop();
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                if (currentTargetFile != null && File.Exists(currentTargetFile))
                    try { File.Delete(currentTargetFile); } catch { }
                throw;
            }

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

        protected static long TryEncrypt(string targetPath, string extension, CryptoSoftService cryptoService, AppSettings settings)
        {
            foreach (string ext in settings.EncryptedExtensions)
            {
                if (ext.Equals(extension, StringComparison.OrdinalIgnoreCase))
                    return cryptoService.Encrypt(targetPath, settings.EncryptionKey);
            }
            return 0;
        }

        private static void WaitForGates(ManualResetEventSlim businessSoftwareGate, ManualResetEventSlim userPauseGate, CancellationToken ct)
        {
            while (!businessSoftwareGate.IsSet || !userPauseGate.IsSet)
            {
                ct.ThrowIfCancellationRequested();
                if (!businessSoftwareGate.IsSet) businessSoftwareGate.Wait(ct);
                if (!userPauseGate.IsSet) userPauseGate.Wait(ct);
            }
            ct.ThrowIfCancellationRequested();
        }
    }
}
