using System.Diagnostics;

namespace EasySaveWpf.ViewModels;

public class BusinessSoftwareMonitor
{

    private bool _isMonitoring; // flag to keep the loop running or not.

    private bool _wasPreviouslyDetected;  // flag to track if the software was detected in the previous check.

    public event Action<string>? OnBusinessSoftwareDetected;

    public event Action? OnBusinessSoftwareClosed;

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

    public void StartMonitoring(IEnumerable<string> configuredProcessNames)
    {
        _isMonitoring = true;
        _wasPreviouslyDetected = false;

        List<string> processList = configuredProcessNames.ToList();

        Task.Run(() =>
        {
            while (_isMonitoring)
            {
            bool isRunningNow = TryFindRunningBusinessSoftware(processList, out string detectedName);

                if (isRunningNow && !_wasPreviouslyDetected)
                {
                    OnBusinessSoftwareDetected?.Invoke(detectedName);

                }
                else if (!isRunningNow && _wasPreviouslyDetected)
                {
                    OnBusinessSoftwareClosed?.Invoke();
                }

                _wasPreviouslyDetected = isRunningNow;
                Thread.Sleep(1000); // Check every 1 seconds
            }
        });
    }

    public void StopMonitoring()
    {
        _isMonitoring = false;
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