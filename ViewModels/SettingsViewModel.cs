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
    private string _prioritizedExtensions = string.Empty;
    private int _largeFileSizeThresholdKb = 0;
    private string _encryptionKey = string.Empty;
    private LogMode _logMode = LogMode.Local;
    private string _dockerLogServerUrl = "127.0.0.1:5132";

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

    public string PrioritizedExtensions
    {
        get => _prioritizedExtensions;
        set => SetField(ref _prioritizedExtensions, value);
    }

    public int LargeFileSizeThresholdKb
    {
        get => _largeFileSizeThresholdKb;
        set => SetField(ref _largeFileSizeThresholdKb, value);
    }

    public string EncryptionKey
    {
        get => _encryptionKey;
        set => SetField(ref _encryptionKey, value);
    }

    public List<string> AvailableLanguages => LangMgr.GetAvailableLanguages();

    // Displays each language name in its own language, e.g. "English / Français / Русский" — readable regardless of the active language
    public string LanguageSelectorLabel
    {
        get
        {
            List<string> parts = new List<string>();
            foreach (string lang in LangMgr.GetAvailableLanguages())
            {
                string name = LangMgr.GetTextForLanguage(lang, "gui_language_name");
                parts.Add(name);
            }

            string result = string.Empty;
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0) result += " / ";
                result += parts[i];
            }
            return result;
        }
    }

    public List<LogFormat> AvailableFormats { get; } =
    [
        LogFormat.Json,
        LogFormat.Xml
    ];

    public LogMode LogMode
    {
        get => _logMode;
        set
        {
            if (SetField(ref _logMode, value))
                OnPropertyChanged(nameof(SelectedLogModeOption));
        }
    }

    public string DockerLogServerUrl
    {
        get => _dockerLogServerUrl;
        set => SetField(ref _dockerLogServerUrl, value);
    }

    public List<LogModeDisplayOption> LogModeOptions { get; }

    public LogModeDisplayOption SelectedLogModeOption
    {
        get => LogModeOptions.First(o => o.Mode == LogMode);
        set
        {
            if (value != null && value.Mode != LogMode)
                LogMode = value.Mode;
        }
    }

    public event Action<AppSettings>? Saved;
    public event Action? Cancelled;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public SettingsViewModel(AppSettings current)
    {
        LogModeOptions = new List<LogModeDisplayOption>
        {
            new(LogMode.Centralized, LangMgr.GetText("gui_log_mode_centralized")),
            new(LogMode.Local, LangMgr.GetText("gui_log_mode_local")),
            new(LogMode.Both, LangMgr.GetText("gui_log_mode_both")),
        };

        _language = current.Language;
        _logFormat = current.LogFormat;
        _stateFormat = current.StateFormat;
        _businessProcesses = string.Join(Environment.NewLine, current.BusinessSoftwareProcesses);
        _encryptedExtensions = string.Join(Environment.NewLine, current.EncryptedExtensions);
        _prioritizedExtensions = string.Join(Environment.NewLine, current.PrioritizedExtensions);
        _largeFileSizeThresholdKb = current.LargeFileSizeThresholdKb;
        _encryptionKey = current.EncryptionKey;
        _logMode = current.LogMode;
        _dockerLogServerUrl = current.DockerLogServerUrl;

        SaveCommand = new Command(Save);
        CancelCommand = new Command(() => Cancelled?.Invoke());
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
            EncryptionKey = EncryptionKey,
            LogMode = LogMode,
            DockerLogServerUrl = DockerLogServerUrl.Trim()
            PrioritizedExtensions = ParseLines(PrioritizedExtensions),
            LargeFileSizeThresholdKb = LargeFileSizeThresholdKb,
            EncryptionKey = EncryptionKey
        };
        Saved?.Invoke(updated);
    }

    private static List<string> ParseLines(string multiline)
    {
        var result = new List<string>();
        foreach (string line in multiline.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0 && !result.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
                result.Add(trimmed);
        }
        return result;
    }
}
