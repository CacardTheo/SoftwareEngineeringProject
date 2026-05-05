using System.Windows.Input;
using EasyLog;
using EasySaveWpf;

namespace EasySaveWpf.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private string _language = "en";
    private LogFormat _logFormat;
    private LogFormat _stateFormat;
    private string _businessProcesses = string.Empty;
    private string _encryptedExtensions = string.Empty;
    private string _encryptionKey = string.Empty;

    public string Language
    {
        get => _language;
        set => SetField(ref _language, value);
    }

    public LogFormat LogFormat
    {
        get => _logFormat;
        set => SetField(ref _logFormat, value);
    }

    public LogFormat StateFormat
    {
        get => _stateFormat;
        set => SetField(ref _stateFormat, value);
    }

    public string BusinessProcesses
    {
        get => _businessProcesses;
        set => SetField(ref _businessProcesses, value);
    }

    public string EncryptedExtensions
    {
        get => _encryptedExtensions;
        set => SetField(ref _encryptedExtensions, value);
    }

    public string EncryptionKey
    {
        get => _encryptionKey;
        set => SetField(ref _encryptionKey, value);
    }

    public List<string> AvailableLanguages => LangMgr.GetAvailableLanguages();

    public List<LogFormat> AvailableFormats { get; } =
    [
        LogFormat.Json,
        LogFormat.Xml
    ];



    public event Action<AppSettings>? Saved;
    public event Action? Cancelled;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public SettingsViewModel(AppSettings current)
    {
        _language = current.Language;
        _logFormat = current.LogFormat;
        _stateFormat = current.StateFormat;
        _businessProcesses = string.Join(Environment.NewLine, current.BusinessSoftwareProcesses);
        _encryptedExtensions = string.Join(Environment.NewLine, current.EncryptedExtensions);
        _encryptionKey = current.EncryptionKey;

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(() => Cancelled?.Invoke());
    }

    private void Save()
    {
        var updated = new AppSettings
        {
            Language = Language,
            LogFormat = LogFormat,
            StateFormat = StateFormat,
            BusinessSoftwareProcesses = ParseLines(BusinessProcesses),
            EncryptedExtensions = ParseLines(EncryptedExtensions),
            EncryptionKey = EncryptionKey
        };
        Saved?.Invoke(updated);
    }

    private static List<string> ParseLines(string multiline) =>
        multiline.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                 .Select(s => s.Trim())
                 .Where(s => s.Length > 0)
                 .Distinct(StringComparer.OrdinalIgnoreCase)
                 .ToList();
}