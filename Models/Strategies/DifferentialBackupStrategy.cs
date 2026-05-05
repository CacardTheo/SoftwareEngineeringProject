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

        public void Backup(BackupJob job, LogService logService, AppSettings settings, CryptoSoftService cryptoService, Action<string, string, long> onFileCopied, Func<bool> canCopyNextFile)
        {
            if (string.IsNullOrEmpty(job.SourceDir) || string.IsNullOrEmpty(job.TargetDir))
                return;

            // Si la source est un fichier unique, on le copie directement
            if (File.Exists(job.SourceDir))
            {
                if (!canCopyNextFile())
                    throw new InvalidOperationException("BUSINESS_SOFTWARE_DETECTED");

                CopySingleFile(job.SourceDir, job.TargetDir, job, logService, settings, cryptoService, onFileCopied);
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
                Console.WriteLine($"{_languageManager.GetText("error_finding_files")}{ex.Message}");
                return;
            }

            foreach (FileInfo file in files)
            {
                if (!canCopyNextFile())
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

                        long encryptionTime = TryEncrypt(targetFilePath, file.Extension, cryptoService, settings);
                        onFileCopied(file.FullName, targetFilePath, file.Length);

                        logService.Save(new LogEntry
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
                        logService.Save(new LogEntry
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

        private static void CopySingleFile(string sourcePath, string targetDir, BackupJob job, LogService logService, AppSettings settings, CryptoSoftService cryptoService, Action<string, string, long> onFileCopied)
        {
            FileInfo fileInfo = new FileInfo(sourcePath);
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            string targetPath = Path.Combine(targetDir, fileInfo.Name);

            // Différentiel : on copie uniquement si le fichier cible n'existe pas ou est plus ancien
            if (!File.Exists(targetPath) || fileInfo.LastWriteTime > File.GetLastWriteTime(targetPath))
            {
                Stopwatch sw = Stopwatch.StartNew();
                File.Copy(sourcePath, targetPath, true);
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
