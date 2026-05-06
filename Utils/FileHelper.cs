namespace EasySaveWpf
{
    public static class FileHelper
    {
        public static void CopyFile(string sourcePath, string targetPath)
        {
            const int bufferSize = 4096;
            using FileStream source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read);
            using FileStream target = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
            byte[] buffer = new byte[bufferSize];
            int bytesRead;
            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                target.Write(buffer, 0, bytesRead);
        }
    }
}
