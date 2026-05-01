using System.Windows.Input;
using EasySaveWpf;

namespace EasySaveWpf.ViewModels;

public class JobCardViewModel : ViewModelBase
{
    private BackupStatus _status = BackupStatus.Inactive;
    private int _progression;
    private bool _isRunning;
    private string _currentFile = string.Empty;
    private bool _blockedByBusinessSoftware;

    public BackupJob Job { get; }
    private LanguageManager LangMgr => LanguageManager.GetInstance();

    public string Name => Job.Name ?? string.Empty;
    public string TypeLabel => Job.Type.ToString();
    public string SourceDir => Job.SourceDir ?? string.Empty;
    public string TargetDir => Job.TargetDir ?? string.Empty;
    public string SourceLineText => $"{LangMgr.GetText("gui_from")} {SourceDir}";
    public string TargetLineText => $"{LangMgr.GetText("gui_to")} {TargetDir}";
    public string BlockedMessage => LangMgr.GetText("gui_blocked_msg");

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
    public string CurrentFileLabel => $"{LangMgr.GetText("gui_current_file")} {CurrentFile}";

    public string StatusText => Status switch
    {
        BackupStatus.In_Progress => $"{Progression}%",
        BackupStatus.Ended => LangMgr.GetText("gui_done"),
        BackupStatus.Error => LangMgr.GetText("gui_error"),
        BackupStatus.Inactive when BlockedByBusinessSoftware => LangMgr.GetText("gui_blocked"),
        _ => LangMgr.GetText("gui_idle")
    };

    public string StatusColor => Status switch
    {
        BackupStatus.In_Progress => "#4A90D9",
        BackupStatus.Ended => "#4CAF50",
        BackupStatus.Error => "#F44336",
        BackupStatus.Inactive when BlockedByBusinessSoftware => "#FF9800",
        _ => "#9E9E9E"
    };

    public ICommand RunCommand { get; }
    public ICommand DeleteCommand { get; }

    public JobCardViewModel(BackupJob job, Func<JobCardViewModel, Task> onRun, Action<JobCardViewModel> onDelete)
    {
        Job = job;

        RunCommand = new AsyncRelayCommand(
            () => onRun(this),
            () => !IsRunning);

        DeleteCommand = new RelayCommand(
            () => onDelete(this),
            () => !IsRunning);
    }

    public void ApplyProgress(BackupProgressEventArgs args)
    {
        Status = args.Status;
        Progression = Math.Max(0, args.Progression);
        BlockedByBusinessSoftware = args.BlockedByBusinessSoftware;
        if (!string.IsNullOrEmpty(args.CurrentFile))
            CurrentFile = args.CurrentFile;
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
