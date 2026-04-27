using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasySaveWpf.ViewModels
{
    public class ConfigManager
    {
        private readonly string _configFilePath;

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
    }
}