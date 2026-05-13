using System.Text.Json;
using System.Text.Json.Serialization;
using EasySaveWpf.Utils;

namespace EasySaveWpf.ViewModels;

public class SettingsManager
{
    private readonly string _settingsFilePath;

    public SettingsManager()
    {
        _settingsFilePath = Path.Combine(FileHelper.GetAppDataFolder(), "settings.json");
    }

    private static JsonSerializerOptions SerializerOptions { get; } = new JsonSerializerOptions
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
                return new AppSettings();

            string json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_settingsFilePath, json);
    }
}
