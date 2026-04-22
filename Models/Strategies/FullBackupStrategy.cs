using SoftwareEngineeringProject;
using SoftwareEngineeringProject.ViewModels;

public class FullBackup : IBackupStrategy
{
    private readonly LanguageManager _languageManager;

    public FullBackup()
    {
        _languageManager = LanguageManager.GetInstance();
    }
    public void Backup(BackupJob job)
    {
        // 1. Get all files in the source directory
        string[] files;
        // On récupère TOUS les fichiers d'un coup, même dans les sous-dossiers
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