public class FullBackupStrategy : IBackupStrategy
{
    public void Copy(string SourceDirectory, string TargetDirectory)
    {
        foreach (string dirPath in Directory.GetDirectories(SourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dirPath.Replace(SourceDirectory, TargetDirectory));
        }

        foreach (string newPath in Directory.GetFiles(SourceDirectory, "*.*", SearchOption.AllDirectories))
        {
            File.Copy(newPath, newPath.Replace(SourceDirectory, TargetDirectory), true);
        }
    }
}