using System.Text.Json;
using System.Windows.Input;
using EasySaveWpf;

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasySaveWpf.ViewModels;

public class SettingsManager : INotifyPropertyChanged
{
    private readonly string _settingsFilePath;

    public SettingsManager()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folderPath = Path.Combine(appData, "EasySave");

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        _settingsFilePath = Path.Combine(folderPath, "settings.json");
        SaveCommand = new Command(() => { });
        CancelCommand = new Command(() => { });
    }

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

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(settings, options);
        File.WriteAllText(_settingsFilePath, json);
    }
    private readonly LanguageManager _languageManager = LanguageManager.GetInstance();
    private string _language = "en";
    private OutputFormat _logFormat;
    private OutputFormat _stateFormat;
    private string _businessProcesses = string.Empty;
    private string _encryptedExtensions = string.Empty;
    private string _encryptionKey = string.Empty;

    public string Language
    {
        get => _language;
        set => SetField(ref _language, value);
    }

    public OutputFormat LogFormat
    {
        get => _logFormat;
        set => SetField(ref _logFormat, value);
    }

    public OutputFormat StateFormat
    {
        get => _stateFormat;
        set => SetField(ref _stateFormat, value);
    }

    /// <summary>Business software process names, one per line.</summary>
    public string BusinessProcesses
    {
        get => _businessProcesses;
        set => SetField(ref _businessProcesses, value);
    }

    /// <summary>File extensions to encrypt, one per line (e.g. ".txt").</summary>
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

    public List<string> AvailableLanguages { get; } = new() { "en", "fr" };

    public List<OutputFormat> AvailableFormats { get; } = new()
    {
        OutputFormat.Json,
        OutputFormat.Xml
    };

    public string WindowTitle => _languageManager.GetText("gui_settings_title");
    public string HeaderText => _languageManager.GetText("gui_settings_title");
    public string LanguageLabel => _languageManager.GetText("gui_language");
    public string LogFormatLabel => _languageManager.GetText("gui_log_format");
    public string StateFormatLabel => _languageManager.GetText("gui_state_format");
    public string BusinessProcessesLabel => _languageManager.GetText("gui_business_processes");
    public string EncryptedExtensionsLabel => _languageManager.GetText("gui_encrypted_extensions");
    public string EncryptionKeyLabel => _languageManager.GetText("gui_encryption_key");
    public string SaveLabel => _languageManager.GetText("gui_save");
    public string CancelLabel => _languageManager.GetText("gui_cancel");

    // Raised when user clicks Save
    public event Action<AppSettings>? Saved;
    // Raised when user cancels
    public event Action? Cancelled;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public SettingsManager(AppSettings current) : this()
    {
        _language = current.Language;
        _logFormat = current.LogFormat;
        _stateFormat = current.StateFormat;
        _businessProcesses = string.Join(Environment.NewLine, current.BusinessSoftwareProcesses);
        _encryptedExtensions = string.Join(Environment.NewLine, current.EncryptedExtensions);
        _encryptionKey = current.EncryptionKey;

        SaveCommand = new Command(Save);
        CancelCommand = new Command(() => Cancelled?.Invoke());
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

    private static List<string> ParseLines(string multiline)
    {
        var lines = new List<string>();
        string[] parts = multiline.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string part in parts)
        {
            string trimmed = part.Trim();
            if (trimmed.Length == 0)
                continue;

            bool alreadyAdded = false;
            foreach (string existing in lines)
            {
                if (string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
                lines.Add(trimmed);
        }

        return lines;
    }
}
