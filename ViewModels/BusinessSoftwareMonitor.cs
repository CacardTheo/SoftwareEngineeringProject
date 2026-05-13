using System.Diagnostics;

namespace EasySaveWpf.ViewModels;

public class BusinessSoftwareMonitor
{
    private CancellationTokenSource? _cts;
    private bool _wasPreviouslyDetected;

    public event Action<string>? OnBusinessSoftwareDetected;
    public event Action? OnBusinessSoftwareClosed;

    public bool TryFindRunningBusinessSoftware(IEnumerable<string> configuredProcessNames, out string detectedProcess)
    {
        detectedProcess = string.Empty;

        foreach (string configuredName in configuredProcessNames)
        {
            string normalizedName = NormalizeProcessName(configuredName);
            if (string.IsNullOrWhiteSpace(normalizedName))
                continue;

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
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _wasPreviouslyDetected = false;

        List<string> processList = configuredProcessNames.ToList();
        CancellationToken token = _cts.Token;

        Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                bool isRunningNow = TryFindRunningBusinessSoftware(processList, out string detectedName);

                if (isRunningNow && !_wasPreviouslyDetected)
                    OnBusinessSoftwareDetected?.Invoke(detectedName);
                else if (!isRunningNow && _wasPreviouslyDetected)
                    OnBusinessSoftwareClosed?.Invoke();

                _wasPreviouslyDetected = isRunningNow;

                // WaitOne(1000) revient immédiatement si le token est annulé,
                // contrairement à Thread.Sleep qui aurait ignoré le signal d'arrêt.
                token.WaitHandle.WaitOne(1000);
            }
        }, token);
    }

    public void StopMonitoring()
    {
        _cts?.Cancel();
    }

    private static string NormalizeProcessName(string processName)
    {
        string name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = Path.GetFileNameWithoutExtension(name);
        return name;
    }
}
