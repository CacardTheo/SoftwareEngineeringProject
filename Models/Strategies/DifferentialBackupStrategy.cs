using SoftwareEngineeringProject;
using SoftwareEngineeringProject.ViewModels;

public class DifferentialBackup : IBackupStrategy
{
    private readonly LanguageManager _languageManager;

    public DifferentialBackup()
    {
        _languageManager = LanguageManager.GetInstance();
    }

    public void Backup(BackupJob job)
    {
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
            // Calcul du chemin de destination
            string relativePath = Path.GetRelativePath(job.SourceDir, file);
            string destFile = Path.Combine(job.TargetDir, relativePath);

            bool shouldCopy = false;

            if (!File.Exists(destFile))
            {
                shouldCopy = true;
            }
            else
            {
                DateTime sourceTime = File.GetLastWriteTime(file);
                DateTime destTime = File.GetLastWriteTime(destFile);

                if (sourceTime > destTime)
                {
                    shouldCopy = true;
                }
            }

            if (shouldCopy)
            {
                string parentFolder = Path.GetDirectoryName(destFile);
                if (!Directory.Exists(parentFolder))
                {
                    Directory.CreateDirectory(parentFolder);
                }

                File.Copy(file, destFile, true);
            }
        }
    }
}