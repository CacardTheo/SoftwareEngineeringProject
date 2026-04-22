using SoftwareEngineeringProject;

public class FullBackup : IBackupStrategy
{
    public void Backup(BackupJob job)
    {
        // 1. Get all files in the source directory
        string[] files = Directory.GetFiles(job.SourceDir, "*.*", SearchOption.AllDirectories);

        foreach (string file in files)
        {
            // 2. Prepare the destination path
            string relativePath = Path.GetRelativePath(job.SourceDir, file);
            string destFile = Path.Combine(job.TargetDir, relativePath);

            // 3. Create the directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(destFile));

            // 4. Copy the file (true means overwrite)
            File.Copy(file, destFile, true);
            
            // Logic for StateManager and Logging would go here
            Console.WriteLine("Copied: " + file);
        }
    }
}