using System.Text.Json;

namespace EasySaveWpf.ViewModels;

public class SettingsManager
{
    private readonly string _settingsFilePath;

    public SettingsManager()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folderPath = Path.Combine(appData, "EasySave");

        if (!Directory.Exists(folderPath))
            Directory.CreateDirectory(folderPath);

        _settingsFilePath = Path.Combine(folderPath, "settings.json");
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
