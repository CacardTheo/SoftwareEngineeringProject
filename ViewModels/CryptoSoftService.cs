using System.Diagnostics;

namespace EasySaveWpf.ViewModels;

public class CryptoSoftService
{
    private const string DefaultExecutableName = "CryptoSoft.exe";

    public string ExecutablePath { get; set; } = DefaultExecutableName;

    public long Encrypt(string filePath, string key)
    {
        string exePath = ResolveExecutablePath();

        if (!File.Exists(exePath))
        {
            // CryptoSoft not installed — no encryption performed
            return 0;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"\"{filePath}\" \"{key}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            Stopwatch sw = Stopwatch.StartNew();
            using Process process = Process.Start(startInfo)!;
            process.WaitForExit();
            sw.Stop();

            if (process.ExitCode < 0)
            {
                return process.ExitCode; // propagate CryptoSoft error code
            }

            return sw.ElapsedMilliseconds;
        }
        catch
        {
            return -1;
        }
    }

    private string ResolveExecutablePath()
    {
        if (Path.IsPathRooted(ExecutablePath))
        {
            return ExecutablePath;
        }

        return Path.Combine(AppContext.BaseDirectory, ExecutablePath);
    }
}
