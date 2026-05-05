using System.Windows.Input;
using EasySaveWpf;

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasySaveWpf.ViewModels;

public class BackupJobViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    private readonly LanguageManager _languageManager = LanguageManager.GetInstance();
    private BackupStatus _status = BackupStatus.Inactive;
    private int _progression;
    private bool _isRunning;
    private string _currentFile = string.Empty;
    private bool _blockedByBusinessSoftware;

    public BackupJob Job { get; }

    public string Name => Job.Name ?? string.Empty;
    public string TypeLabel => Job.Type.ToString();
    public string SourceDir => Job.SourceDir ?? string.Empty;
    public string TargetDir => Job.TargetDir ?? string.Empty;
    public string SourceLineText => _languageManager.GetText("gui_from") + " " + SourceDir;
    public string TargetLineText => _languageManager.GetText("gui_to") + " " + TargetDir;
    public string BlockedMessage => _languageManager.GetText("gui_blocked_msg");

    public BackupStatus Status
    {
        get => _status;
        set
        {
            SetField(ref _status, value);
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(HasBeenRun));
                OnPropertyChanged(nameof(IsInProgress));
        }
    }

    public int Progression
    {
        get => _progression;
        set => SetField(ref _progression, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            SetField(ref _isRunning, value);
            OnPropertyChanged(nameof(IsNotRunning));
        }
    }

    public bool IsNotRunning => !_isRunning;

    public bool HasBeenRun  => Status != BackupStatus.Inactive;
    public bool IsInProgress => Status == BackupStatus.In_Progress;

    public bool BlockedByBusinessSoftware
    {
        get => _blockedByBusinessSoftware;
        set => SetField(ref _blockedByBusinessSoftware, value);
    }

    public string CurrentFile
    {
        get => _currentFile;
        set
        {
            if (SetField(ref _currentFile, value))
            {
                OnPropertyChanged(nameof(HasCurrentFile));
                OnPropertyChanged(nameof(CurrentFileLabel));
            }
        }
    }

    public bool HasCurrentFile => !string.IsNullOrWhiteSpace(CurrentFile);
    public string CurrentFileLabel => _languageManager.GetText("gui_current_file") + " " + CurrentFile;

    public string StatusText
    {
        get
        {
            if (Status == BackupStatus.In_Progress)
                return Progression + "%";

            if (Status == BackupStatus.Ended)
                return _languageManager.GetText("gui_done");

            if (Status == BackupStatus.Error)
                return _languageManager.GetText("gui_error");

            if (Status == BackupStatus.Inactive && BlockedByBusinessSoftware)
                return _languageManager.GetText("gui_blocked");

            return _languageManager.GetText("gui_idle");
        }
    }

    public string StatusColor
    {
        get
        {
            if (Status == BackupStatus.In_Progress)
                return "#4A90D9";

            if (Status == BackupStatus.Ended)
                return "#4CAF50";

            if (Status == BackupStatus.Error)
                return "#F44336";

            if (Status == BackupStatus.Inactive && BlockedByBusinessSoftware)
                return "#FF9800";

            return "#9E9E9E";
        }
    }

    public ICommand RunCommand { get; }
    public ICommand DeleteCommand { get; }

    public BackupJobViewModel(BackupJob job, Func<BackupJobViewModel, Task> onRun, Action<BackupJobViewModel> onDelete)
    {
        Job = job;

        RunCommand = new AsyncCommand(
            () => onRun(this),
            () => !IsRunning);

        DeleteCommand = new Command(
            () => onDelete(this),
            () => !IsRunning);
    }

    public void ApplyProgress(string jobName, BackupStatus status, int progression, string currentFile, bool blocked)
    {
        Status = status;
        Progression = Math.Max(0, progression);
        BlockedByBusinessSoftware = blocked;
        if (!string.IsNullOrEmpty(currentFile))
            CurrentFile = currentFile;
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(SourceLineText));
        OnPropertyChanged(nameof(TargetLineText));
        OnPropertyChanged(nameof(BlockedMessage));
        OnPropertyChanged(nameof(CurrentFileLabel));
        OnPropertyChanged(nameof(StatusText));
    }
}
