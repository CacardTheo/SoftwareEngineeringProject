using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SoftwareEngineeringProject.ViewModels
{
    public class ConfigManager
    {
        private readonly string _configFilePath;
        private readonly string _settingsFilePath;

        public ConfigManager()
        {
            // Use AppData/Roaming/EasySave to store the configuration.
            // This ensures the file is accessible on any server and avoids "c:\temp" or relative dev paths.
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folderPath = Path.Combine(appData, "EasySave");

            // Ensure the folder exists before trying to read/write
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            _configFilePath = Path.Combine(folderPath, "backup_jobs.json");
            _settingsFilePath = Path.Combine(folderPath, "settings.json");
        }

        public void SaveJobs(List<BackupJob> jobs)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(jobs, options);
                File.WriteAllText(_configFilePath, json);

                Console.WriteLine($"[CONFIG] Jobs saved successfully to: {_configFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Config save error: {ex.Message}");
            }
        }

        public List<BackupJob> LoadJobs()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    // If no config exists yet, return an empty list or create a default one
                    return new List<BackupJob>();
                }

                string json = File.ReadAllText(_configFilePath);
                return JsonSerializer.Deserialize<List<BackupJob>>(json) ?? new List<BackupJob>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Config read error: {ex.Message}");
                return new List<BackupJob>();
            }
        }

        public void SaveLogFormat(string format)
        {
            try
            {
                var settings = LoadSettingsDict();
                settings["logFormat"] = format;
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(settings, options));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Settings save error: {ex.Message}");
            }
        }

        public string LoadLogFormat()
        {
            try
            {
                if (!File.Exists(_settingsFilePath)) return "JSON";
                string json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return settings != null && settings.TryGetValue("logFormat", out string? format) ? format : "JSON";
            }
            catch
            {
                return "JSON";
            }
        }

        public void SaveStateFormat(string format)
        {
            try
            {
                var settings = LoadSettingsDict();
                settings["stateFormat"] = format;
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(_settingsFilePath, JsonSerializer.Serialize(settings, options));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Settings save error: {ex.Message}");
            }
        }

        public string LoadStateFormat()
        {
            try
            {
                if (!File.Exists(_settingsFilePath)) return "JSON";
                string json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return settings != null && settings.TryGetValue("stateFormat", out string? format) ? format : "JSON";
            }
            catch
            {
                return "JSON";
            }
        }

        private Dictionary<string, string> LoadSettingsDict()
        {
            if (!File.Exists(_settingsFilePath))
                return new Dictionary<string, string>();
            string json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
    }
}