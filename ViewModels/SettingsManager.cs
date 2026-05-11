using System.Text.Json;
using EasySaveWpf.Utils;

namespace EasySaveWpf.ViewModels;

public class SettingsManager
{
    private readonly string _settingsFilePath;

    public SettingsManager()
    {
        _settingsFilePath = Path.Combine(FileHelper.GetAppDataFolder(), "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
                return new AppSettings();

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
}
