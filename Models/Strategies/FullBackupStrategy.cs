using System.Diagnostics;
using EasyLog;
using EasySaveWpf;
using EasySaveWpf.Services;
using EasySaveWpf.ViewModels;

namespace EasySaveWpf
{
    public class FullBackupStrategy : IBackupStrategy
    {
        private readonly LanguageManager _languageManager;

        public FullBackupStrategy()
        {
            _languageManager = LanguageManager.GetInstance();
        }

        public void Backup(BackupJob job, BackupLogRouter logRouter, AppSettings settings, CryptoSoftService cryptoService, Action<string, string, long> onFileCopied, Func<bool> canCopyNextFile, Action<string, string, long>? onBytesWritten = null)
        {
            if (string.IsNullOrEmpty(job.SourceDir) || string.IsNullOrEmpty(job.TargetDir))
                throw new ArgumentException(_languageManager.GetText("log_error_missing_paths"));

            try
            {
                if (!File.Exists(job.SourceDir) && !Directory.Exists(job.SourceDir))
                    throw new DirectoryNotFoundException(_languageManager.GetText("log_error_source_not_found"));

                // Si la source est un fichier unique, on le copie directement
                if (File.Exists(job.SourceDir))
                {
                    if (!canCopyNextFile())
                        throw new InvalidOperationException("BUSINESS_SOFTWARE_DETECTED");

                    CopySingleFile(job.SourceDir, job.TargetDir, job, logRouter, settings, cryptoService, onFileCopied, onBytesWritten);
                    return;
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(job.SourceDir, "*.*", SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    throw new IOException(_languageManager.GetText("error_finding_files") + ex.Message, ex);
                }

                foreach (string filePath in files)
                {
                    if (!canCopyNextFile())
                        throw new InvalidOperationException("BUSINESS_SOFTWARE_DETECTED");

                    FileInfo fileInfo = new FileInfo(filePath);
                    string targetPath = filePath.Replace(job.SourceDir, job.TargetDir);

                    Stopwatch sw = Stopwatch.StartNew();
                    try
                    {
                        string? dir = Path.GetDirectoryName(targetPath);
                        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                        FileHelper.CopyFile(filePath, targetPath, onBytesWritten);
                        sw.Stop();

                        long encryptionTime = TryEncrypt(targetPath, fileInfo.Extension, cryptoService, settings);
                        onFileCopied(filePath, targetPath, fileInfo.Length);

                        logRouter.Save(new LogEntry
                        {
                            BackupName = job.Name ?? string.Empty,
                            SourceFilePath = filePath,
                            TargetFilePath = targetPath,
                            FileSize = fileInfo.Length,
                            FileTransferTimeMs = sw.ElapsedMilliseconds,
                            EncryptionTimeMs = encryptionTime,
                            Event = "FileCopied"
                        });
                    }
                    catch (InvalidOperationException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        Console.WriteLine($"{_languageManager.GetText("log_error_copy_failed")}{fileInfo.Name}: {ex.Message}");

                        logRouter.Save(new LogEntry
                        {
                            BackupName = job.Name ?? string.Empty,
                            SourceFilePath = filePath,
                            TargetFilePath = "ERROR",
                            FileSize = fileInfo.Length,
                            FileTransferTimeMs = -1,
                            EncryptionTimeMs = 0,
                            Event = "CopyError"
                        });
                    }
                }
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

        private static void CopySingleFile(string sourcePath, string targetDir, BackupJob job, BackupLogRouter logRouter, AppSettings settings, CryptoSoftService cryptoService, Action<string, string, long> onFileCopied, Action<string, string, long>? onBytesWritten = null)
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

            logRouter.Save(new LogEntry
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

        private static long TryEncrypt(string targetPath, string extension, CryptoSoftService cryptoService, AppSettings settings)
        {
            foreach (string ext in settings.EncryptedExtensions)
            {
                if (ext.Equals(extension, StringComparison.OrdinalIgnoreCase))
                    return cryptoService.Encrypt(targetPath, settings.EncryptionKey);
            }
            return 0;
        }
    }
}
