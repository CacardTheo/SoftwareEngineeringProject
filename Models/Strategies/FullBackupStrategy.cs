using System;
using System.IO;
using System.Diagnostics;
using EasySaveWpf;
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

        public void Backup(BackupJob job, BackupExecutionContext context)
        {
            if (string.IsNullOrEmpty(job.SourceDir) || string.IsNullOrEmpty(job.TargetDir))
            {
                Console.WriteLine("[ERROR] Missing paths in BackupJob.");
                return;
            }

            try
            {
                if (!Directory.Exists(job.SourceDir))
                {
                    Console.WriteLine("[ERROR] Source directory does not exist!");
                    return;
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(job.SourceDir, "*.*", SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{_languageManager.GetText("error_finding_files")}{ex.Message}");
                    return;
                }

                foreach (string filePath in files)
                {
                    if (!context.CanCopyNextFile())
                        throw new InvalidOperationException("BUSINESS_SOFTWARE_DETECTED");

                    FileInfo fileInfo = new FileInfo(filePath);
                    string targetPath = filePath.Replace(job.SourceDir, job.TargetDir);

                    Stopwatch sw = Stopwatch.StartNew();
                    try
                    {
                        string? dir = Path.GetDirectoryName(targetPath);
                        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                        File.Copy(filePath, targetPath, true);
                        sw.Stop();

                        long encryptionTime = TryEncrypt(targetPath, fileInfo.Extension, context);
                        context.OnFileCopied(filePath, targetPath, fileInfo.Length);

                        context.LogManager.Save(new AppLogEntry
                        {
                            BackupName = job.Name ?? string.Empty,
                            SourceFilePath = filePath,
                            TargetFilePath = targetPath,
                            FileSize = fileInfo.Length,
                            FileTransferTimeMs = sw.ElapsedMilliseconds,
                            EncryptionTimeMs = encryptionTime,
                            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            Event = "FileCopied"
                        }, context.LogFormat);
                    }
                    catch (InvalidOperationException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        Console.WriteLine($"[ERROR] Failed to copy {fileInfo.Name}: {ex.Message}");

                        context.LogManager.Save(new AppLogEntry
                        {
                            BackupName = job.Name ?? string.Empty,
                            SourceFilePath = filePath,
                            TargetFilePath = "ERROR",
                            FileSize = fileInfo.Length,
                            FileTransferTimeMs = -1,
                            EncryptionTimeMs = 0,
                            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            Event = "CopyError"
                        }, context.LogFormat);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"[ACCESS DENIED] {ex.Message}");
                context.LogManager.Save(new AppLogEntry
                {
                    BackupName = job.Name ?? string.Empty,
                    SourceFilePath = job.SourceDir ?? string.Empty,
                    TargetFilePath = "ACCESS_DENIED",
                    FileSize = 0,
                    FileTransferTimeMs = -1,
                    EncryptionTimeMs = 0,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Event = "AccessDenied"
                }, context.LogFormat);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[STRATEGY ERROR] {ex.Message}");
            }
        }

        private static long TryEncrypt(string targetPath, string extension, BackupExecutionContext context)
        {
            bool shouldEncrypt = context.Settings.EncryptedExtensions
                .Any(e => e.Equals(extension, StringComparison.OrdinalIgnoreCase));

            return shouldEncrypt
                ? context.CryptoService.Encrypt(targetPath, context.Settings.EncryptionKey)
                : 0;
        }
    }
}