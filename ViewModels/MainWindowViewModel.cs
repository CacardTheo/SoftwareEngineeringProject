using System.Collections.ObjectModel;
using System.Windows.Input;
using EasySaveWpf;
using Avalonia.Threading;

namespace EasySaveWpf.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly MainViewModel _mainViewModel;

    public ObservableCollection<JobCardViewModel> Jobs { get; } = new();
    // Callbacks set by the main window to show modal dialogs
    public Func<Task<BackupJob?>>? RequestAddJob { get; set; }
    public Func<AppSettings, Task<AppSettings?>>? RequestSettings { get; set; }

    public ICommand RunAllCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand AddJobCommand { get; }

    public MainWindowViewModel()
    {
        _mainViewModel = new MainViewModel();

        _mainViewModel.JobProgressChanged += OnJobProgressChanged;

        AppSettings settings = _mainViewModel.GetSettings();

        foreach (var job in _mainViewModel.GetJobs())
            Jobs.Add(CreateCard(job));

        RunAllCommand = new AsyncRelayCommand(RunAllAsync);
        AddJobCommand = new AsyncRelayCommand(AddJobAsync);
        OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsAsync);
    }

    private JobCardViewModel CreateCard(BackupJob job)
    {
        return new JobCardViewModel(
            job,
            onRun: async card =>
            {
                card.IsRunning = true;
                card.Status = BackupStatus.In_Progress;
                card.Progression = 0;
                try
                {
                    int index = _mainViewModel.GetJobs().IndexOf(card.Job);
                    await Task.Run(() => _mainViewModel.RunJobByIndex(index));
                }
                finally
                {
                    card.IsRunning = false;
                }
            },
            onDelete: card =>
            {
                int index = _mainViewModel.GetJobs().IndexOf(card.Job);
                if (_mainViewModel.DeleteJob(index))
                    Jobs.Remove(card);
            });
    }

    private async Task RunAllAsync()
    {
        foreach (var card in Jobs)
            card.IsRunning = true;

        try
        {
            await Task.Run(() => _mainViewModel.RunAllJobs());
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

        if (_mainViewModel.AddJob(newJob))
            Jobs.Add(CreateCard(newJob));
    }

    private async Task OpenSettingsAsync()
    {
        if (RequestSettings == null) return;

        AppSettings current = _mainViewModel.GetSettings();
        AppSettings? updated = await RequestSettings.Invoke(current);
        if (updated == null) return;

        _mainViewModel.ApplySettings(updated);
    }

    private void OnJobProgressChanged(object? sender, BackupProgressEventArgs args)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            var card = Jobs.FirstOrDefault(j => j.Name == args.JobName);
            card?.ApplyProgress(args);
        });
    }
}
