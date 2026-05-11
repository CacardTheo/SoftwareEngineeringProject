using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasySaveWpf.ViewModels
{
    public class ConfigManager
    {
        private readonly string _configFilePath;
        private readonly LanguageManager _languageManager = LanguageManager.GetInstance();

        public ConfigManager()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folderPath = Path.Combine(appData, "EasySave");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            _configFilePath = Path.Combine(folderPath, "backup_jobs.json");
        }

        public void SaveJobs(List<BackupJob> jobs)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(jobs, options);
                File.WriteAllText(_configFilePath, json);

                Console.WriteLine($"{_languageManager.GetText("config_jobs_saved")}{_configFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{_languageManager.GetText("config_save_error")}{ex.Message}");
            }
        }

        public List<BackupJob> LoadJobs()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return [];

                string json = File.ReadAllText(_configFilePath);
                return JsonSerializer.Deserialize<List<BackupJob>>(json) ?? [];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{_languageManager.GetText("config_read_error")}{ex.Message}");
                return [];
            }
        }
    }
}
