using SoftwareEngineeringProject;
using SoftwareEngineeringProject.ViewModels;

public class FullBackup : IBackupStrategy
{
    private readonly LanguageManager _languageManager;

    public FullBackup()
    {
        _languageManager = LanguageManager.GetInstance();
    }

    public void Backup(BackupJob job, Action<string, string, long> onFileCopied)
    {
        // Get all files in the source directory, including subdirectories
        string[] files;
        try
        {
            files = Directory.GetFiles(job.SourceDir, "*.*", SearchOption.AllDirectories);
        } catch (Exception ex)
        {
            Console.WriteLine($"{_languageManager.GetText("error_finding_files")}{ex.Message}");
            return;
        }

        foreach (string file in files)
        {
            // Prepare the destination path
            string relativePath = Path.GetRelativePath(job.SourceDir, file);
            string destFile = Path.Combine(job.TargetDir, relativePath);

            // Create the directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(destFile));

            // Copy the file (true means overwrite)
            long fileSize = new FileInfo(file).Length;
            File.Copy(file, destFile, true);

            // Report progress
            onFileCopied(file, destFile, fileSize);
        }
    }
}