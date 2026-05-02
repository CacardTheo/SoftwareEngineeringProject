using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace EasyLog
{
    public class JsonLogSerializer : ILogSerializer
    {
        public string FileExtension => "json";

        public List<LogEntry> Load(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<LogEntry>>(json) ?? new List<LogEntry>();
        }

        public void Save(List<LogEntry> logs, string filePath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(filePath, JsonSerializer.Serialize(logs, options));
        }
    }
}
