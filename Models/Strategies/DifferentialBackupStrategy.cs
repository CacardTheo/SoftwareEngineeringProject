using System;
using System.IO;
using System.Diagnostics;
using EasyLog;

namespace SoftwareEngineeringProject
{
    public class DifferentialBackupStrategy : IBackupStrategy
    {
        public void Backup(BackupJob job, LogService logService)
        {
            if (string.IsNullOrEmpty(job.SourceDirectory) || string.IsNullOrEmpty(job.TargetDirectory))
            {
                Console.WriteLine("[STRATEGY] Error: Source or Target directory is missing.");
                return;
            }

            Console.WriteLine($"\n[STRATEGY] Starting Differential Backup for: {job.Name}");

            if (!Directory.Exists(job.TargetDirectory))
            {
                Directory.CreateDirectory(job.TargetDirectory);
            }

            try
            {
                DirectoryInfo sourceInfo = new DirectoryInfo(job.SourceDirectory);
                FileInfo[] files = sourceInfo.GetFiles("*.*", SearchOption.AllDirectories);

                foreach (FileInfo file in files)
                {
                    string targetFilePath = file.FullName.Replace(job.SourceDirectory, job.TargetDirectory);

                    // Logic: Only copy if file is new or modified
                    if (!File.Exists(targetFilePath) || file.LastWriteTime > File.GetLastWriteTime(targetFilePath))
                    {
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        try
                        {
                            string? targetDirectory = Path.GetDirectoryName(targetFilePath);
                            if (targetDirectory != null && !Directory.Exists(targetDirectory))
                            {
                                Directory.CreateDirectory(targetDirectory);
                            }

                            File.Copy(file.FullName, targetFilePath, true);
                            stopwatch.Stop();

                            // Real-time logging for each copied file
                            logService.Save(new LogEntry
                            {
                                BackupName = job.Name,
                                SourceFilePath = file.FullName,
                                TargetFilePath = targetFilePath,
                                FileSize = file.Length,
                                FileTransferTimeMs = stopwatch.ElapsedMilliseconds,
                                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            });
                        }
                        catch (Exception)
                        {
                            stopwatch.Stop();
                            // Log error with -1 as per requirements
                            logService.Save(new LogEntry
                            {
                                BackupName = job.Name,
                                SourceFilePath = file.FullName,
                                TargetFilePath = targetFilePath,
                                FileSize = file.Length,
                                FileTransferTimeMs = -1,
                                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                            });
                        }
                    }
                }

                Console.WriteLine("[STRATEGY] Differential backup completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[STRATEGY] Critical error during differential backup: {ex.Message}");
            }
        }
    }
}