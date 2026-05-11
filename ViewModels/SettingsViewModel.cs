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
    private string _encryptionKey = string.Empty;
    private int _largeFileSizeThresholdKb = 0;

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

    // Affiche le nom de chaque langue dans sa propre langue, ex: "English / Français / Русский"
    // Reste lisible quelle que soit la langue active
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
        _prioritizedExtensions = string.Join(Environment.NewLine, current.PrioritizedExtensions);
        _largeFileSizeThresholdKb = current.LargeFileSizeThresholdKb;

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
            PrioritizedExtensions = ParseLines(PrioritizedExtensions),
            LargeFileSizeThresholdKb = LargeFileSizeThresholdKb
        };
        Saved?.Invoke(updated);
    }

    private static List<string> ParseLines(string multiline)
    {
        string[] lines = multiline.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            bool alreadyAdded = false;
            foreach (string existing in result)
            {
                if (string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
                result.Add(trimmed);
        }

        return result;
    }
}