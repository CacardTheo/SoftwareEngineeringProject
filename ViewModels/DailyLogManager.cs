using System.Text.Json;
using System.Xml.Serialization;

namespace EasySaveWpf.ViewModels;

public class DailyLogManager
{
    private readonly string _logsFolderPath;

    public DailyLogManager()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folderPath = Path.Combine(appData, "EasySave", "logs");

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        _logsFolderPath = folderPath;
    }

    public void Save(AppLogEntry entry, OutputFormat format)
    {
        List<AppLogEntry> entries = LoadForDay(format);
        entries.Add(entry);

        if (format == OutputFormat.Xml)
        {
            SaveXml(entries);
            return;
        }

        SaveJson(entries);
    }

    private List<AppLogEntry> LoadForDay(OutputFormat format)
    {
        if (format == OutputFormat.Xml)
        {
            string path = GetDayFilePath(OutputFormat.Xml);
            if (!File.Exists(path))
            {
                return new List<AppLogEntry>();
            }

            try
            {
                var serializer = new XmlSerializer(typeof(List<AppLogEntry>));
                using FileStream stream = File.OpenRead(path);
                return (List<AppLogEntry>?)serializer.Deserialize(stream) ?? new List<AppLogEntry>();
            }
            catch
            {
                return new List<AppLogEntry>();
            }
        }

        string jsonPath = GetDayFilePath(OutputFormat.Json);
        if (!File.Exists(jsonPath))
        {
            return new List<AppLogEntry>();
        }

        try
        {
            string json = File.ReadAllText(jsonPath);
            return JsonSerializer.Deserialize<List<AppLogEntry>>(json) ?? new List<AppLogEntry>();
        }
        catch
        {
            return new List<AppLogEntry>();
        }
    }

    private void SaveJson(List<AppLogEntry> entries)
    {
        string path = GetDayFilePath(OutputFormat.Json);
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(entries, options);
        File.WriteAllText(path, json);
    }

    private void SaveXml(List<AppLogEntry> entries)
    {
        string path = GetDayFilePath(OutputFormat.Xml);
        var serializer = new XmlSerializer(typeof(List<AppLogEntry>));

        using FileStream stream = File.Create(path);
        serializer.Serialize(stream, entries);
    }

    private string GetDayFilePath(OutputFormat format)
    {
        string extension = format == OutputFormat.Xml ? "xml" : "json";
        string fileName = $"{DateTime.Now:yyyy-MM-dd}.{extension}";
        return Path.Combine(_logsFolderPath, fileName);
    }
}