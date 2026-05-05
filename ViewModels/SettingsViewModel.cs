using System.Windows.Input;
using EasyLog;
using EasySaveWpf;

namespace EasySaveWpf.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private LanguageManager LangMgr => LanguageManager.GetInstance();
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

    public string WindowTitle => LangMgr.GetText("gui_settings_title");
    public string HeaderText => LangMgr.GetText("gui_settings_title");
    public string LanguageLabel => LangMgr.GetText("gui_language");
    public string LogFormatLabel => LangMgr.GetText("gui_log_format");
    public string StateFormatLabel => LangMgr.GetText("gui_state_format");
    public string BusinessProcessesLabel => LangMgr.GetText("gui_business_processes");
    public string EncryptedExtensionsLabel => LangMgr.GetText("gui_encrypted_extensions");
    public string EncryptionKeyLabel => LangMgr.GetText("gui_encryption_key");
    public string SaveLabel => LangMgr.GetText("gui_save");
    public string CancelLabel => LangMgr.GetText("gui_cancel");

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

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(LogFormatLabel));
        OnPropertyChanged(nameof(StateFormatLabel));
        OnPropertyChanged(nameof(BusinessProcessesLabel));
        OnPropertyChanged(nameof(EncryptedExtensionsLabel));
        OnPropertyChanged(nameof(EncryptionKeyLabel));
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(CancelLabel));
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