using SoftwareEngineeringProject;

public interface IBackupStrategy
{
    // Callback: (sourceFile, destFile, fileSize) called after each file is copied
    void Backup(BackupJob job, Action<string, string, long> onFileCopied);
}