using SoftwareEngineeringProject;
using System.Text.Json;

public class ConfigManager
{
    private readonly string _configFilePath = "backup_jobs.json";
    public void SaveJobs(List<BackupJob> jobs)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(jobs, options);
        File.WriteAllText(_configFilePath, json);
    }

    public List<BackupJob> LoadJobs()
    {
        if (!File.Exists(_configFilePath))
            return new List<BackupJob>();

        string json = File.ReadAllText(_configFilePath);
        return JsonSerializer.Deserialize<List<BackupJob>>(json) ?? new List<BackupJob>();
    }
}