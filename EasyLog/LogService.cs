using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasyLog
{
    public class LogService
    {
        private readonly string _logFolder;

        public LogService()
        {
            // Dynamically find the AppData folder and create an EasySave subfolder
            // This works on any server/PC regardless of drive letters
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _logFolder = Path.Combine(appData, "EasySave", "Logs");
        }

        public void Save(LogEntry entry)
        {
            try
            {
                if (!Directory.Exists(_logFolder))
                {
                    Directory.CreateDirectory(_logFolder);
                }

                string fileName = $"{DateTime.Now:yyyy-MM-dd}.json";
                string filePath = Path.Combine(_logFolder, fileName);

                List<LogEntry> logs = new List<LogEntry>();

                if (File.Exists(filePath))
                {
                    try
                    {
                        string json = File.ReadAllText(filePath);
                        logs = JsonSerializer.Deserialize<List<LogEntry>>(json) ?? new List<LogEntry>();
                    }
                    catch { logs = new List<LogEntry>(); }
                }

                logs.Add(entry);

                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(filePath, JsonSerializer.Serialize(logs, options));

                // Feedback for the user to find the file
                Console.WriteLine($"[LOG] Entry added to: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOG ERROR] {ex.Message}");
            }
        }
    }
}