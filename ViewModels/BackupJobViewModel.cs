using System.Windows.Input;
using EasySaveWpf;

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasySaveWpf.ViewModels;

public class BackupJobViewModel : ViewModelBase
{
    private BackupStatus _status = BackupStatus.Inactive;
    private int _progression;
    private bool _isRunning;
    private string _currentFile = string.Empty;
    private bool _blockedByBusinessSoftware;
    private string _errorMessage = string.Empty;

    public BackupJob Job { get; }

    public string Name => Job.Name ?? string.Empty;
    public string TypeLabel => Job.Type.ToString();
    public string SourceDir => Job.SourceDir ?? string.Empty;
    public string TargetDir => Job.TargetDir ?? string.Empty;

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
            OnPropertyChanged(nameof(HasError));
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
            RunCommand.RaiseCanExecuteChanged();
            DeleteCommand.RaiseCanExecuteChanged();
        }
    }

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
            }
        }
    }

    public bool HasCurrentFile => !string.IsNullOrWhiteSpace(CurrentFile);

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetField(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => Status == BackupStatus.Error && !string.IsNullOrEmpty(_errorMessage);
    public string StatusText
    {
        get
        {
            if (Status == BackupStatus.In_Progress)
                return Progression + "%";

            if (Status == BackupStatus.Ended)
                return LangMgr["gui_done"];

            if (Status == BackupStatus.Error)
                return LangMgr["gui_error"];

            if (Status == BackupStatus.Inactive && BlockedByBusinessSoftware)
                return LangMgr["gui_blocked"];

            return LangMgr["gui_idle"];
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

    public Command RunCommand { get; }
    public Command DeleteCommand { get; }

    public BackupJobViewModel(BackupJob job, Action<BackupJobViewModel> onRun, Action<BackupJobViewModel> onDelete)
    {
        Job = job;

        RunCommand = new Command(
            () => onRun(this),
            () => !IsRunning);

        DeleteCommand = new Command(
            () => onDelete(this),
            () => !IsRunning);
            
        LanguageManager.Instance.PropertyChanged += (s, e) => OnPropertyChanged(nameof(StatusText));
    }

    public void ApplyProgress(string jobName, BackupStatus status, int progression, string currentFile, bool blocked, string errorMessage = "")
    {
        Status = status;
        Progression = Math.Max(0, progression);
        BlockedByBusinessSoftware = blocked;
        if (!string.IsNullOrEmpty(currentFile))
            CurrentFile = currentFile;
        ErrorMessage = status == BackupStatus.Error ? errorMessage : string.Empty;
    }

}
