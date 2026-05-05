using System.Windows.Input;
using EasySaveWpf;

namespace EasySaveWpf.ViewModels;

public class AddJobViewModel : ViewModelBase
{
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
