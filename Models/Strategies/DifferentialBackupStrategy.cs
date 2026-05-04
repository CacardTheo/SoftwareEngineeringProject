using System.Diagnostics;
using EasyLog;
using EasySaveWpf;
using EasySaveWpf.ViewModels;

namespace EasySaveWpf
{
    public class DifferentialBackupStrategy : IBackupStrategy
    {
        private readonly LanguageManager _languageManager;

        public DifferentialBackupStrategy()
        {
            _languageManager = LanguageManager.GetInstance();
        }

        public void Backup(BackupJob job, BackupExecutionContext context)
        {
            if (string.IsNullOrEmpty(job.SourceDir) || string.IsNullOrEmpty(job.TargetDir))
                return;

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
                Console.WriteLine($"{_languageManager.GetText("error_finding_files")}{ex.Message}");
                return;
            }

            foreach (FileInfo file in files)
            {
                if (!context.CanCopyNextFile())
                    throw new InvalidOperationException("BUSINESS_SOFTWARE_DETECTED");

                string targetFilePath = file.FullName.Replace(job.SourceDir, job.TargetDir);

                if (!File.Exists(targetFilePath) || file.LastWriteTime > File.GetLastWriteTime(targetFilePath))
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    try
                    {
                        string? targetDirectory = Path.GetDirectoryName(targetFilePath);
                        if (targetDirectory != null && !Directory.Exists(targetDirectory))
                            Directory.CreateDirectory(targetDirectory);

                        File.Copy(file.FullName, targetFilePath, true);
                        stopwatch.Stop();

                        long encryptionTime = TryEncrypt(targetFilePath, file.Extension, context);
                        context.OnFileCopied(file.FullName, targetFilePath, file.Length);

                        context.LogService.Save(new LogEntry
                        {
                            BackupName = job.Name ?? string.Empty,
                            SourceFilePath = file.FullName,
                            TargetFilePath = targetFilePath,
                            FileSize = file.Length,
                            FileTransferTimeMs = stopwatch.ElapsedMilliseconds,
                            EncryptionTimeMs = encryptionTime,
                            Event = "FileCopied"
                        });
                    }
                    catch (InvalidOperationException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        stopwatch.Stop();
                        context.LogService.Save(new LogEntry
                        {
                            BackupName = job.Name ?? string.Empty,
                            SourceFilePath = file.FullName,
                            TargetFilePath = targetFilePath,
                            FileSize = file.Length,
                            FileTransferTimeMs = -1,
                            EncryptionTimeMs = 0,
                            Event = "CopyError"
                        });
                    }
                }
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