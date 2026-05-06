using System;
using System.Collections.Generic;
using System.IO;

namespace EasyLog
{
    public class LogService
    {
        private readonly string _logFolder;
        private readonly ILogSerializer _serializer;

        public LogService(ILogSerializer? serializer = null)
        {
            _serializer = serializer ?? new JsonLogSerializer();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _logFolder = Path.Combine(appData, "EasySave", "Logs");
        }
        public LogService (LogFormat format) : this(format == LogFormat.Xml ? new XmlLogSerializer() : new JsonLogSerializer())
        {
            
        }

        public void Save(LogEntry entry)
        {
            if (!Directory.Exists(_logFolder))
                Directory.CreateDirectory(_logFolder);

            string filePath = Path.Combine(_logFolder, $"{DateTime.Now:yyyy-MM-dd}.{_serializer.FileExtension}");

            List<LogEntry> logs = new List<LogEntry>();

            if (File.Exists(filePath))
            {
                try { logs = _serializer.Load(filePath); }
                catch { logs = new List<LogEntry>(); }
            }

            logs.Add(entry);
            _serializer.Save(logs, filePath);
        }
    }
}
