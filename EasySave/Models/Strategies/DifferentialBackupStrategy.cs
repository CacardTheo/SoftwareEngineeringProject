using System;
using System.IO;
using System.Diagnostics;
using EasyLog;
using SoftwareEngineeringProject;
using SoftwareEngineeringProject.ViewModels;

namespace SoftwareEngineeringProject
{
    public class DifferentialBackupStrategy : IBackupStrategy
    {
        private readonly LanguageManager _languageManager;

        public DifferentialBackupStrategy()
        {
            _languageManager = LanguageManager.GetInstance();
        }
        public void Backup(BackupJob job, LogService logService, Action<string, string, long> onFileCopied)
        {
            if (string.IsNullOrEmpty(job.SourceDir) || string.IsNullOrEmpty(job.TargetDir))
            {
                Console.WriteLine("[STRATEGY] Error: Source or Target directory is missing.");
                return;
            }

            Console.WriteLine($"\n[STRATEGY] Starting Differential Backup for: {job.Name}");

            if (!Directory.Exists(job.TargetDir))
            {
                Directory.CreateDirectory(job.TargetDir);
            }

            DirectoryInfo sourceInfo = new DirectoryInfo(job.SourceDir);
            FileInfo[] files;
            // On récupère TOUS les fichiers d'un coup, même dans les sous-dossiers
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
                string targetFilePath = file.FullName.Replace(job.SourceDir, job.TargetDir);

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

                        onFileCopied(file.Name, targetFilePath, file.Length);

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
    }
}
