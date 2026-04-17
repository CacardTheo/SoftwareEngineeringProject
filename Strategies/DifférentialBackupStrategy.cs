public class DifferentialBackupStrategy : IBackupStrategy
{
    public void Copy(string SourceDirectory, string TargetDirectory)
    {
        foreach (string dirPath in Directory.GetDirectories(SourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(dirPath.Replace(SourceDirectory, TargetDirectory));
        }

        foreach (string newPath in Directory.GetFiles(SourceDirectory, "*.*", SearchOption.AllDirectories))
        {
            if (!File.Exists(newPath.Replace(SourceDirectory, TargetDirectory)) || File.GetLastWriteTime(newPath) > File.GetLastWriteTime(newPath.Replace(SourceDirectory, TargetDirectory)))
            {
                File.Copy(newPath, newPath.Replace(SourceDirectory, TargetDirectory), true);
            }
        }
    }
}