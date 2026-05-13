using System.Collections.ObjectModel;
using System.Windows.Input;
using EasySaveWpf;
using Avalonia.Threading;

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasySaveWpf.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly SettingsManager _settingsManager;
    private readonly AppSettings _settings;
    private readonly BackupProcessor _backupProcessor;
    private readonly ConfigManager _configManager;

    private readonly List<BackupJob> _jobs;

    private readonly ManualResetEventSlim _businessSoftwareGate = new ManualResetEventSlim(true);
    private readonly BusinessSoftwareMonitor _businessSoftwareMonitor;

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

    // Callbacks set by MainWindow to open dialog windows; the callback is invoked when the user confirms or cancels
    public Action<Action<BackupJob?>>? RequestAddJob { get; set; }
    public Action<AppSettings, Action<AppSettings?>>? RequestSettings { get; set; }

    public Command RunAllCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand AddJobCommand { get; }

    private bool _isRunningAll = false;

    public MainViewModel()
    {
        _settingsManager = new SettingsManager();
        _settings = _settingsManager.Load();

        LanguageManager.Instance.SetLanguage(_settings.Language);

        StateManager stateManager = new StateManager();
        stateManager.SetFormat(_settings.StateFormat);

        _businessSoftwareMonitor = new BusinessSoftwareMonitor();
        
        _businessSoftwareMonitor.OnBusinessSoftwareDetected += (processName) =>
        {
            _businessSoftwareGate.Reset(); // to pause all jobs
        };

        _businessSoftwareMonitor.OnBusinessSoftwareClosed += () =>
        {
            _businessSoftwareGate.Set(); // to resume all jobs
        };

        _businessSoftwareMonitor.StartMonitoring(_settings.BusinessSoftwareProcesses);

        _backupProcessor = new BackupProcessor(
            stateManager,
            _businessSoftwareMonitor,
            CryptoSoftService.Instance,
            _settings,
            _businessSoftwareGate);

        _configManager = new ConfigManager();
        _jobs = _configManager.LoadJobs();

        _backupProcessor.ProgressChanged += OnJobProgressChanged;

        _selectedLanguage = _settings.Language;

        FillJobCards();

        RunAllCommand = new Command(RunAll, () => !_isRunningAll);
        AddJobCommand = new Command(ShowAddJobDialog);
        OpenSettingsCommand = new Command(ShowSettingsDialog);
    }

    public void Cleanup()
    {
        _businessSoftwareMonitor.StopMonitoring();
        _businessSoftwareGate.Dispose();
        foreach (BackupJobViewModel card in Jobs)
            card.Dispose();
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

    private void RunCard(BackupJobViewModel card)
    {
        card.ResetForNewRun();
        card.IsRunning = true;
        card.Status = BackupStatus.In_Progress;
        card.Progression = 0;

        int index = _jobs.IndexOf(card.Job);
        var syncContext = new BackupSyncContext(_settings.LargeFileSizeThresholdKb);
        var userPauseGate = card.UserPauseGate;
        var cancellationToken = card.CancellationToken;

        Thread thread = new Thread(() =>
        {
            try
            {
                RunJobByIndex(index, syncContext, userPauseGate, cancellationToken);
            }
            catch (Exception) { }
            finally
            {
                syncContext.Dispose();
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    card.IsRunning = false;
                    card.IsPaused = false;
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
            card.Dispose();
        }
    }

    private void RunAll()
    {
        _isRunningAll = true;
        RunAllCommand.RaiseCanExecuteChanged();

        var cards = Jobs.ToList();
        foreach (BackupJobViewModel card in cards)
        {
            card.ResetForNewRun();
            card.IsRunning = true;
            card.Status = BackupStatus.In_Progress;
            card.Progression = 0;
        }

        Thread coordinator = new Thread(() =>
        {
            var syncContext = new BackupSyncContext(_settings.LargeFileSizeThresholdKb);
            var jobThreads = new List<Thread>();

            foreach (BackupJobViewModel card in cards)
            {
                int index = _jobs.IndexOf(card.Job);
                var userPauseGate = card.UserPauseGate;
                var cancellationToken = card.CancellationToken;
                BackupJobViewModel captured = card;

                Thread jobThread = new Thread(() =>
                {
                    try
                    {
                        RunJobByIndex(index, syncContext, userPauseGate, cancellationToken);
                    }
                    catch (Exception) { }
                    finally
                    {
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            captured.IsRunning = false;
                            captured.IsPaused = false;
                        });
                    }
                });
                jobThread.IsBackground = true;
                jobThreads.Add(jobThread);
            }

            foreach (var t in jobThreads) t.Start();
            foreach (var t in jobThreads) t.Join();
            syncContext.Dispose();

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                _isRunningAll = false;
                RunAllCommand.RaiseCanExecuteChanged();
            });
        });
        coordinator.IsBackground = true;
        coordinator.Start();
    }

    // Opens the add-job dialog via a callback invoked when the user confirms or cancels
    private void ShowAddJobDialog()
    {
        RequestAddJob?.Invoke(newJob =>
        {
            if (newJob == null) return;
            if (AddJob(newJob))
                Jobs.Add(CreateCard(newJob));
        });
    }

    // Opens the settings dialog via a callback
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


    public static string GetText(string key) => LanguageManager.Instance.GetText(key);

    public bool ChangeLanguage(string lang)
    {
        try
        {
            LanguageManager.Instance.SetLanguage(lang);
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

        using var syncContext = new BackupSyncContext(_settings.LargeFileSizeThresholdKb);
        bool allSucceeded = true;
        foreach (int index in indicesToRun)
        {
            using var pauseGate = new ManualResetEventSlim(true);
            bool succeeded = _backupProcessor.Execute(_jobs[index], syncContext, pauseGate, CancellationToken.None);
            allSucceeded &= succeeded;

            if (!succeeded)
                break;
        }

        return allSucceeded;
    }

    public bool RunAllJobs()
    {
        if (_jobs.Count == 0) return false;

        using var syncContext = new BackupSyncContext(_settings.LargeFileSizeThresholdKb);
        bool allSucceeded = true;
        var threads = new List<Thread>();
        var results = new bool[_jobs.Count];
        var pauseGates = new ManualResetEventSlim[_jobs.Count];

        for (int i = 0; i < _jobs.Count; i++)
        {
            pauseGates[i] = new ManualResetEventSlim(true);
            int captured = i;
            var thread = new Thread(() =>
            {
                try
                {
                    results[captured] = _backupProcessor.Execute(
                        _jobs[captured], syncContext, pauseGates[captured], CancellationToken.None);
                }
                finally
                {
                    pauseGates[captured].Dispose();
                }
            });
            thread.IsBackground = true;
            threads.Add(thread);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        foreach (bool r in results) allSucceeded &= r;
        return allSucceeded;
    }

    public bool RunJobByIndex(int index, BackupSyncContext? syncContext = null, ManualResetEventSlim? userPauseGate = null, CancellationToken cancellationToken = default)
    {
        if (index < 0 || index >= _jobs.Count) return false;
        syncContext ??= new BackupSyncContext(_settings.LargeFileSizeThresholdKb);
        return _backupProcessor.Execute(_jobs[index], syncContext, userPauseGate ?? new ManualResetEventSlim(true), cancellationToken);
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
        _settings.LargeFileSizeThresholdKb = updated.LargeFileSizeThresholdKb;
        _settings.EncryptionKey = updated.EncryptionKey;
        LanguageManager.Instance.SetLanguage(updated.Language);
        _settings.Language = updated.Language;
        SaveSettings();
        _businessSoftwareMonitor.StopMonitoring();
        _businessSoftwareMonitor.StartMonitoring(_settings.BusinessSoftwareProcesses);
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

    // Converts user input ("1-3" or "1;4;5") into a 0-based index list
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
