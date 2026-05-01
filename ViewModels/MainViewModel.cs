namespace EasySaveWpf.ViewModels;

public class MainViewModel
{
    private readonly LanguageManager _languageManager;
    private readonly SettingsManager _settingsManager;
    private readonly AppSettings _settings;
    private readonly BackupProcessor _backupProcessor;
    private readonly ConfigManager _configManager;

    private List<BackupJob> _jobs;

    public event EventHandler<BackupProgressEventArgs>? JobProgressChanged
    {
        add => _backupProcessor.ProgressChanged += value;
        remove => _backupProcessor.ProgressChanged -= value;
    }

    public MainViewModel()
    {
        _languageManager = LanguageManager.GetInstance();
        _settingsManager = new SettingsManager();
        _settings = _settingsManager.Load();

        _languageManager.SetLanguage(_settings.Language);

        StateManager stateManager = new StateManager();
        stateManager.SetFormat(_settings.StateFormat);

        _backupProcessor = new BackupProcessor(
            stateManager,
            new DailyLogManager(),
            new BusinessSoftwareMonitor(),
            new CryptoSoftService(),
            () => _settings);

        _configManager = new ConfigManager();
        _jobs = _configManager.LoadJobs();
    }

    public string GetText(string key) => _languageManager.GetText(key);

    public bool ChangeLanguage(string lang)
    {
        try
        {
            _languageManager.SetLanguage(lang);
            _settings.Language = lang;
            SaveSettings();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool CreateJob(string name, string source, string target, string type)
    {
        if (_jobs.Any(j => j.Name == name)) return false;

        _jobs.Add(new BackupJob
        {
            Name = name,
            SourceDir = source,
            TargetDir = target,
            Type = type.Equals("Full", StringComparison.OrdinalIgnoreCase) ? BackupType.Full : BackupType.Differential
        });

        _configManager.SaveJobs(_jobs);
        return true;
    }

    public bool AddJob(BackupJob job)
    {
        if (job.Name == null || _jobs.Any(j => j.Name == job.Name)) return false;
        _jobs.Add(job);
        _configManager.SaveJobs(_jobs);
        return true;
    }

    public bool DeleteJob(int index)
    {
        if (index < 0 || index >= _jobs.Count) return false;
        _jobs.RemoveAt(index);
        _configManager.SaveJobs(_jobs);
        return true;
    }

    public bool RunJob(string input)
    {
        if (_jobs.Count == 0) return false;

        List<int> indicesToRun = ParseIndices(input, _jobs.Count);
        bool allSucceeded = true;

        foreach (int index in indicesToRun)
        {
            bool succeeded = _backupProcessor.Execute(_jobs[index]);
            allSucceeded &= succeeded;

            if (!succeeded)
                break;
        }

        return allSucceeded;
    }

    public bool RunAllJobs()
    {
        if (_jobs.Count == 0) return false;
        return RunJob($"1-{_jobs.Count}");
    }

    public bool RunJobByIndex(int index)
    {
        if (index < 0 || index >= _jobs.Count) return false;
        return _backupProcessor.Execute(_jobs[index]);
    }

    public List<BackupJob> GetJobs() => _jobs;

    public AppSettings GetSettings() => _settings;

    public void UpdateBusinessSoftwareProcesses(IEnumerable<string> processNames)
    {
        _settings.BusinessSoftwareProcesses = processNames
            .Select(n => n.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        SaveSettings();
    }

    public void SetLogFormat(OutputFormat format)
    {
        _settings.LogFormat = format;
        SaveSettings();
    }

    public void SetStateFormat(OutputFormat format)
    {
        _settings.StateFormat = format;
        SaveSettings();
    }

    public void UpdateEncryptedExtensions(IEnumerable<string> extensions)
    {
        _settings.EncryptedExtensions = extensions
            .Select(e => e.Trim().ToLowerInvariant())
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct()
            .ToList();
        SaveSettings();
    }

    public void SetEncryptionKey(string key)
    {
        _settings.EncryptionKey = key;
        SaveSettings();
    }

    private void SaveSettings() => _settingsManager.Save(_settings);

    private static List<int> ParseIndices(string input, int maxCount)
    {
        var indices = new HashSet<int>();

        if (input.Contains('-'))
        {
            var parts = input.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
                for (int i = start; i <= end; i++)
                    if (i > 0 && i <= maxCount) indices.Add(i - 1);
        }
        else if (input.Contains(';'))
        {
            foreach (var part in input.Split(';'))
                if (int.TryParse(part, out int id) && id > 0 && id <= maxCount)
                    indices.Add(id - 1);
        }
        else
        {
            if (int.TryParse(input, out int id) && id > 0 && id <= maxCount)
                indices.Add(id - 1);
        }

        return indices.OrderBy(i => i).ToList();
    }
}
