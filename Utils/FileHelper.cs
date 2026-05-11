namespace EasySaveWpf.Utils
{
    public static class FileHelper
    {
        public static void CopyFile(string sourcePath, string targetPath, Action<string, string, long>? onBytesWritten = null)
        {
            const int bufferSize = 81920;
            using FileStream source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
            using FileStream target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.None);
            byte[] buffer = new byte[bufferSize];
            int bytesRead;
            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                target.Write(buffer, 0, bytesRead);
                onBytesWritten?.Invoke(sourcePath, targetPath, bytesRead);
            }
        }
    }
}
