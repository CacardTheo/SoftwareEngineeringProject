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

    // Callbacks définis par la MainWindow pour ouvrir les fenêtres de dialogue
    // Le callback reçoit une Action à appeler quand l'utilisateur valide ou annule
    public Action<Action<BackupJob?>>? RequestAddJob { get; set; }
    public Action<AppSettings, Action<AppSettings?>>? RequestSettings { get; set; }

    public Command RunAllCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand AddJobCommand { get; }

    private bool _isRunningAll = false;

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

        RunAllCommand = new Command(RunAll, () => !_isRunningAll);
        AddJobCommand = new Command(ShowAddJobDialog);
        OpenSettingsCommand = new Command(ShowSettingsDialog);
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
            onRun: RunCard,
            onDelete: DeleteCard);
    }

    // Lance le backup d'une carte sur un thread de fond
    // pour ne pas bloquer le thread UI pendant la copie des fichiers
    private void RunCard(BackupJobViewModel card)
    {
        card.IsRunning = true;
        card.Status = BackupStatus.In_Progress;
        card.Progression = 0;

        int index = _jobs.IndexOf(card.Job);

        Thread thread = new Thread(() =>
        {
            try
            {
                RunJobByIndex(index);
            }
            catch (Exception) { }
            finally
            {
                // On repasse sur le thread UI pour modifier les propriétés liées à l'interface
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    card.IsRunning = false;
                });
            }
        });
        thread.IsBackground = true;
        thread.Start();
    }

    private void DeleteCard(BackupJobViewModel card)
    {
        int index = _jobs.IndexOf(card.Job);
        if (DeleteJob(index))
        {
            Jobs.Remove(card);
        }
    }

    // Lance tous les backups sur un thread de fond
    private void RunAll()
    {
        _isRunningAll = true;
        RunAllCommand.RaiseCanExecuteChanged();

        foreach (BackupJobViewModel card in Jobs)
            card.IsRunning = true;

        Thread thread = new Thread(() =>
        {
            try
            {
                RunAllJobs();
            }
            catch (Exception) { }
            finally
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _isRunningAll = false;
                    RunAllCommand.RaiseCanExecuteChanged();
                    foreach (BackupJobViewModel card in Jobs)
                        card.IsRunning = false;
                });
            }
        });
        thread.IsBackground = true;
        thread.Start();
    }

    // Ouvre la fenêtre d'ajout de job via un callback
    // Le callback sera appelé quand l'utilisateur confirme ou annule
    private void ShowAddJobDialog()
    {
        RequestAddJob?.Invoke(newJob =>
        {
            if (newJob == null) return;
            if (AddJob(newJob))
                Jobs.Add(CreateCard(newJob));
        });
    }

    // Ouvre la fenêtre des paramètres via un callback
    private void ShowSettingsDialog()
    {
        AppSettings current = GetSettings();
        RequestSettings?.Invoke(current, updated =>
        {
            if (updated == null) return;
            ApplySettings(updated);
            SetField(ref _selectedLanguage, updated.Language, nameof(SelectedLanguage));
        });
    }

    private void OnJobProgressChanged(string jobName, BackupStatus status, int progression, string currentFile, bool blocked, string errorMessage)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            UpdateCardProgress(jobName, status, progression, currentFile, blocked, errorMessage);
        });
    }

    private void UpdateCardProgress(string jobName, BackupStatus status, int progression, string currentFile, bool blocked, string errorMessage)
    {
        foreach (BackupJobViewModel card in Jobs)
        {
            if (card.Name == jobName)
            {
                card.ApplyProgress(jobName, status, progression, currentFile, blocked, errorMessage);
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

        // One shared context for the whole batch: enforces cross-job large-file limit.
        var context = new BackupSyncContext(_settings.LargeFileSizeThresholdKb);

        var results = new bool[indicesToRun.Count];
        var threads = new List<Thread>();

        for (int i = 0; i < indicesToRun.Count; i++)
        {
            int capturedI = i;
            int capturedIndex = indicesToRun[i];
            var thread = new Thread(() =>
            {
                try
                {
                    results[capturedI] = _backupProcessor.Execute(_jobs[capturedIndex], context);
                }
                catch (Exception)
                {
                    results[capturedI] = false;
                }
            });
            threads.Add(thread);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        bool allSucceeded = true;
        foreach (bool r in results) allSucceeded &= r;
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
        var context = new BackupSyncContext(_settings.LargeFileSizeThresholdKb);
        return _backupProcessor.Execute(_jobs[index], context);
    }

    public List<BackupJob> GetJobs() => _jobs;

    public AppSettings GetSettings() => _settings;

    public void UpdateBusinessSoftwareProcesses(IEnumerable<string> processNames)
    {
        _settings.BusinessSoftwareProcesses = ToUniqueList(processNames);
        SaveSettings();
    }

    public void ApplySettings(AppSettings updated)
    {
        _settings.LogFormat = updated.LogFormat;
        _settings.StateFormat = updated.StateFormat;
        _settings.BusinessSoftwareProcesses = updated.BusinessSoftwareProcesses;
        _settings.EncryptedExtensions = updated.EncryptedExtensions;
        _settings.PrioritizedExtensions = updated.PrioritizedExtensions;
        _settings.EncryptionKey = updated.EncryptionKey;
        _languageManager.SetLanguage(updated.Language);
        _settings.Language = updated.Language;
        SaveSettings();
    }

    public void UpdateEncryptedExtensions(IEnumerable<string> extensions)
    {
        _settings.EncryptedExtensions = ToUniqueList(extensions, lowercase: true);
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

    private static List<string> ToUniqueList(IEnumerable<string> values, bool lowercase = false)
    {
        var result = new List<string>();
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            string clean = lowercase ? value.Trim().ToLowerInvariant() : value.Trim();
            if (!result.Contains(clean, StringComparer.OrdinalIgnoreCase))
                result.Add(clean);
        }
        return result;
    }

    // Transforme une saisie utilisateur ("1-3" ou "1;4;5") en liste d'indices 0-based
    private static List<int> ParseIndices(string input, int maxCount)
    {
        var indices = new List<int>();

        if (input.Contains('-'))
        {
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
