using System.Diagnostics;

namespace EasySaveWpf.ViewModels;

public class BusinessSoftwareMonitor
{
    public bool TryFindRunningBusinessSoftware(IEnumerable<string> configuredProcessNames, out string detectedProcess)
    {
        detectedProcess = string.Empty;

        foreach (string configuredName in configuredProcessNames)
        {
            string normalizedName = NormalizeProcessName(configuredName);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                continue;
            }

            Process[] running = Process.GetProcessesByName(normalizedName);
            if (running.Length > 0)
            {
                detectedProcess = normalizedName;
                return true;
            }
        }

        return false;
    }

    private static string NormalizeProcessName(string processName)
    {
        string name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = Path.GetFileNameWithoutExtension(name);
        }

        return name;
    }
}