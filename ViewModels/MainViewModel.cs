using System.Collections.ObjectModel;
using System.Windows.Input;
using EasySaveWpf;
using Avalonia.Threading;

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasySaveWpf.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly LanguageManager _languageManager;
    private readonly SettingsManager _settingsManager;
    private readonly AppSettings _settings;
    private readonly BackupProcessor _backupProcessor;
    private readonly ConfigManager _configManager;

    private readonly List<BackupJob> _jobs;

    public ObservableCollection<BackupJobViewModel> Jobs { get; } = new();

    private string _selectedLanguage = "en";
    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (SetField(ref _selectedLanguage, value))
            {
                ChangeLanguage(value);
            }
        }
    }

    public List<string> AvailableLanguages { get; } = new() { "en", "fr" };

    // Callbacks set by the main window to show modal dialogs
    public Func<Task<BackupJob?>>? RequestAddJob { get; set; }
    public Func<AppSettings, Task<AppSettings?>>? RequestSettings { get; set; }

    public ICommand RunAllCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand AddJobCommand { get; }

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
            new BusinessSoftwareMonitor(),
            new CryptoSoftService(),
            _settings);

        _configManager = new ConfigManager();
        _jobs = _configManager.LoadJobs();

        _backupProcessor.ProgressChanged += OnJobProgressChanged;

        _selectedLanguage = _settings.Language;

        FillJobCards();

        RunAllCommand = new AsyncCommand(RunAllAsync);
        AddJobCommand = new AsyncCommand(AddJobAsync);
        OpenSettingsCommand = new AsyncCommand(OpenSettingsAsync);
    }

    private void FillJobCards()
    {
        foreach (BackupJob job in _jobs)
        {
            Jobs.Add(CreateCard(job));
        }
    }

    private BackupJobViewModel CreateCard(BackupJob job)
    {
        return new BackupJobViewModel(
            job,
            onRun: RunCardAsync,
            onDelete: DeleteCard);
    }

    private async Task RunCardAsync(BackupJobViewModel card)
    {
        // Prevent the user from starting the same job multiple times concurrently
        card.IsRunning = true;
        card.Status = BackupStatus.In_Progress;
        card.Progression = 0;

        try
        {
            int index = _jobs.IndexOf(card.Job);
            // Execute the heavy backup process in a background thread to keep the UI responsive
            await Task.Run(() => RunJobByIndex(index));
        }
        finally
        {
            card.IsRunning = false;
        }
    }

    private void DeleteCard(BackupJobViewModel card)
    {
        int index = _jobs.IndexOf(card.Job);
        if (DeleteJob(index))
        {
            Jobs.Remove(card);
        }
    }

    private async Task RunAllAsync()
    {
        // Lock all cards to prevent concurrent executions
        foreach (var card in Jobs)
            card.IsRunning = true;

        try
        {
            await Task.Run(() => RunAllJobs());
        }
        finally
        {
            foreach (var card in Jobs)
                card.IsRunning = false;
        }
    }

    private async Task AddJobAsync()
    {
        if (RequestAddJob == null) return;

        BackupJob? newJob = await RequestAddJob.Invoke();
        if (newJob == null) return;

        if (AddJob(newJob))
            Jobs.Add(CreateCard(newJob));
    }

    private async Task OpenSettingsAsync()
    {
        if (RequestSettings == null) return;

        AppSettings current = GetSettings();
        AppSettings? updated = await RequestSettings.Invoke(current);
        if (updated == null) return;

        ApplySettings(updated);
        SetField(ref _selectedLanguage, updated.Language, nameof(SelectedLanguage));
    }

    private void OnJobProgressChanged(string jobName, BackupStatus status, int progression, string currentFile, bool blocked)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            UpdateCardProgress(jobName, status, progression, currentFile, blocked);
        });
    }

    private void UpdateCardProgress(string jobName, BackupStatus status, int progression, string currentFile, bool blocked)
    {
        foreach (BackupJobViewModel card in Jobs)
        {
            if (card.Name == jobName)
            {
                card.ApplyProgress(jobName, status, progression, currentFile, blocked);
                break;
            }
        }
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
        foreach (BackupJob job in _jobs)
        {
            if (job.Name == name)
                return false;
        }

        BackupType backupType = BackupType.Differential;
        if (type.Equals("Full", StringComparison.OrdinalIgnoreCase))
        {
            backupType = BackupType.Full;
        }

        _jobs.Add(new BackupJob
        {
            Name = name,
            SourceDir = source,
            TargetDir = target,
            Type = backupType
        });

        _configManager.SaveJobs(_jobs);
        return true;
    }

    public bool AddJob(BackupJob job)
    {
        if (job.Name == null)
            return false;

        foreach (BackupJob existingJob in _jobs)
        {
            if (existingJob.Name == job.Name)
                return false;
        }

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
        _settings.BusinessSoftwareProcesses = CreateUniqueList(processNames);
        SaveSettings();
    }

    public void ApplySettings(AppSettings updated)
    {
        _settings.LogFormat = updated.LogFormat;
        _settings.StateFormat = updated.StateFormat;
        _settings.BusinessSoftwareProcesses = updated.BusinessSoftwareProcesses;
        _settings.EncryptedExtensions = updated.EncryptedExtensions;
        _settings.EncryptionKey = updated.EncryptionKey;
        _languageManager.SetLanguage(updated.Language);
        _settings.Language = updated.Language;
        SaveSettings();
    }

    public void UpdateEncryptedExtensions(IEnumerable<string> extensions)
    {
        _settings.EncryptedExtensions = CreateUniqueExtensionList(extensions);
        SaveSettings();
    }

    public void SetEncryptionKey(string key)
    {
        _settings.EncryptionKey = key;
        SaveSettings();
    }

    private void SaveSettings()
    {
        _settingsManager.Save(_settings);
    }

    private static List<string> CreateUniqueList(IEnumerable<string> values)
    {
        var result = new List<string>();

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            string cleanValue = value.Trim();
            bool alreadyAdded = false;

            foreach (string existing in result)
            {
                if (string.Equals(existing, cleanValue, StringComparison.OrdinalIgnoreCase))
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
                result.Add(cleanValue);
        }

        return result;
    }

    private static List<string> CreateUniqueExtensionList(IEnumerable<string> values)
    {
        var result = new List<string>();

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            string cleanValue = value.Trim().ToLowerInvariant();
            bool alreadyAdded = false;

            foreach (string existing in result)
            {
                if (existing == cleanValue)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
                result.Add(cleanValue);
        }

        return result;
    }

    // Parses a user input string (e.g. "1-3" or "1;4;5") into a list of 0-based job indices
    private static List<int> ParseIndices(string input, int maxCount)
    {
        var indices = new List<int>();

        if (input.Contains('-'))
        {
            // Handle range format like "1-5"
            string[] parts = input.Split('-');
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
                {
                    for (int i = start; i <= end; i++)
                    {
                        if (i > 0 && i <= maxCount)
                        {
                            int zeroBased = i - 1;
                            if (!indices.Contains(zeroBased))
                                indices.Add(zeroBased);
                        }
                    }
                }
            }
        }
        else if (input.Contains(';'))
        {
            // Handle list format like "1;3;4"
            string[] parts = input.Split(';');
            foreach (string part in parts)
            {
                if (int.TryParse(part, out int id) && id > 0 && id <= maxCount)
                {
                    int zeroBased = id - 1;
                    if (!indices.Contains(zeroBased))
                        indices.Add(zeroBased);
                }
            }
        }
        else
        {
            if (int.TryParse(input, out int id) && id > 0 && id <= maxCount)
            {
                indices.Add(id - 1);
            }
        }

        indices.Sort();
        return indices;
    }
}