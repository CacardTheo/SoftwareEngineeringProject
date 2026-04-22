using System;
using System.IO;
using System.Diagnostics;
using EasyLog;

namespace SoftwareEngineeringProject
{
    public class FullBackupStrategy : IBackupStrategy
    {
        public void Backup(BackupJob job, LogService logService)
        {
            if (string.IsNullOrEmpty(job.SourceDirectory) || string.IsNullOrEmpty(job.TargetDirectory))
            {
                Console.WriteLine("[ERROR] Missing paths in BackupJob.");
                return;
            }

            Console.WriteLine($"[STRATEGY] Checking source: {job.SourceDirectory}");

            try
            {
                if (!Directory.Exists(job.SourceDirectory))
                {
                    Console.WriteLine("[ERROR] Source directory does not exist!");
                    return;
                }

                // Attempt to get files - this is where permission errors usually trigger
                string[] files = Directory.GetFiles(job.SourceDirectory, "*.*", SearchOption.AllDirectories);
                Console.WriteLine($"[STRATEGY] Found {files.Length} files to process.");

                foreach (string filePath in files)
                {
                    FileInfo fileInfo = new FileInfo(filePath);
                    string targetPath = filePath.Replace(job.SourceDirectory, job.TargetDirectory);

                    Stopwatch sw = Stopwatch.StartNew();
                    try
                    {
                        string? dir = Path.GetDirectoryName(targetPath);
                        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                        File.Copy(filePath, targetPath, true);
                        sw.Stop();

                        Console.WriteLine($"[SUCCESS] Copied: {fileInfo.Name}");

                        logService.Save(new LogEntry
                        {
                            BackupName = job.Name,
                            SourceFilePath = filePath,
                            TargetFilePath = targetPath,
                            FileSize = fileInfo.Length,
                            FileTransferTimeMs = sw.ElapsedMilliseconds,
                            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        Console.WriteLine($"[ERROR] Failed to copy {fileInfo.Name}: {ex.Message}");

                        logService.Save(new LogEntry
                        {
                            BackupName = job.Name,
                            SourceFilePath = filePath,
                            TargetFilePath = "ERROR",
                            FileSize = fileInfo.Length,
                            FileTransferTimeMs = -1, // Requirement: negative if error
                            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        });
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"[ACCESS DENIED] Permission issue with {job.SourceDirectory}: {ex.Message}");

                // Log the directory-level error
                logService.Save(new LogEntry
                {
                    BackupName = job.Name,
                    SourceFilePath = job.SourceDirectory,
                    TargetFilePath = "ACCESS_DENIED",
                    FileSize = 0,
                    FileTransferTimeMs = -1,
                    Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[STRATEGY ERROR] An unexpected error occurred: {ex.Message}");
            }
        }
    }
}