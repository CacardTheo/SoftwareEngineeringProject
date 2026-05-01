using System.Windows.Input;
using EasySaveWpf;

namespace EasySaveWpf.ViewModels;

public class AddJobViewModel : ViewModelBase
{
    private LanguageManager LangMgr => LanguageManager.GetInstance();
    private readonly RelayCommand _createCommand;
    private string _name = string.Empty;
    private string _sourceDir = string.Empty;
    private string _targetDir = string.Empty;
    private BackupType _selectedType = BackupType.Full;

    public string Name
    {
        get => _name;
        set
        {
            if (SetField(ref _name, value))
                _createCommand.RaiseCanExecuteChanged();
        }
    }

    public string SourceDir
    {
        get => _sourceDir;
        set
        {
            if (SetField(ref _sourceDir, value))
                _createCommand.RaiseCanExecuteChanged();
        }
    }

    public string TargetDir
    {
        get => _targetDir;
        set
        {
            if (SetField(ref _targetDir, value))
                _createCommand.RaiseCanExecuteChanged();
        }
    }

    public string WindowTitle => LangMgr.GetText("gui_add_job_title");
    public string HeaderText => LangMgr.GetText("gui_add_job_title");
    public string NameLabel => LangMgr.GetText("gui_add_job_name");
    public string SourceLabel => LangMgr.GetText("gui_add_job_source");
    public string TargetLabel => LangMgr.GetText("gui_add_job_target");
    public string TypeLabel => LangMgr.GetText("gui_add_job_type");
    public string CreateLabel => LangMgr.GetText("gui_create");
    public string CancelLabel => LangMgr.GetText("gui_cancel");

    public BackupType SelectedType
    {
        get => _selectedType;
        set => SetField(ref _selectedType, value);
    }

    public List<BackupType> AvailableTypes { get; } = new()
    {
        BackupType.Full,
        BackupType.Differential
    };

    public string? ErrorMessage { get; private set; }

    public BackupJob? Result { get; private set; }

    // Raised when the user clicks Create (with validation) — host window closes on success
    public event Action<BackupJob>? JobCreated;
    // Raised when user cancels
    public event Action? Cancelled;

    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }

    public AddJobViewModel()
    {
        _createCommand = new RelayCommand(TryCreate, CanCreate);
        CreateCommand = _createCommand;
        CancelCommand = new RelayCommand(() => Cancelled?.Invoke());
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(NameLabel));
        OnPropertyChanged(nameof(SourceLabel));
        OnPropertyChanged(nameof(TargetLabel));
        OnPropertyChanged(nameof(TypeLabel));
        OnPropertyChanged(nameof(CreateLabel));
        OnPropertyChanged(nameof(CancelLabel));
    }

    private bool CanCreate() =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(SourceDir) &&
        !string.IsNullOrWhiteSpace(TargetDir);

    private void TryCreate()
    {
        if (!CanCreate()) return;

        Result = new BackupJob
        {
            Name = Name.Trim(),
            SourceDir = SourceDir.Trim(),
            TargetDir = TargetDir.Trim(),
            Type = SelectedType
        };
        JobCreated?.Invoke(Result);
    }
}
