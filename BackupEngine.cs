using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Enumeration;
using System.Runtime.ExceptionServices;

namespace SoftwareEngineeringProject
{
    public class BackupEngine
    {
        public void Execute(BackupJob job)
        {
            if (!Directory.Exists(job.SourceDirectory))
            {
                Console.WriteLine($"Error: Source directory {job.SourceDirectory} not found.");
                return;
            }
            if (!Directory.Exists(job.TargetDirectory))
            {
                Directory.CreateDirectory(job.TargetDirectory);
                Console.WriteLine($"Target directory created: {job.TargetDirectory}");
            }
            Console.WriteLine($"Executing job {job.Name} from {job.SourceDirectory} to {job.TargetDirectory}");
            string[] files = Directory.GetFiles(job.SourceDirectory);
            Console.WriteLine($"Number of files in {job.SourceDirectory}: {files.Count()}");
            CopyFiles(job, files);
        }

        public void CopyFiles(BackupJob job, string[] files)
        {
            foreach (string filePath in files)
            {
                string fileName = Path.GetFileName(filePath);
                string destPath = Path.Combine(job.TargetDirectory, fileName);
                if (job.Type == BackupType.Full)
                    File.Copy(filePath, destPath, true);
                else if (job.Type == BackupType.Differential)
                {
                    if (!File.Exists(destPath) || File.GetLastWriteTime(filePath) > File.GetLastWriteTime(destPath))
                    {
                        File.Copy(filePath, destPath, true);
                    }
                }
                else
                {
                    Console.WriteLine($"Job type {job.Type} doesn't exists");
                    return;
                }

                Console.WriteLine($"Copy of {fileName} done.");
            }
        }
    }
}
