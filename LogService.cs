using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;

namespace EasyLog
{
    public class LogService
    {
        private readonly ILogSerializer _serializer;
        private readonly string _logFolder;
        private readonly LogMode _mode;
        private readonly string _dockerUrl;

        public LogService(LogFormat format, LogMode mode, string dockerUrl)
            : this(format == LogFormat.Xml ? new XmlLogSerializer() : new JsonLogSerializer())
        {
            _mode = mode;
            _dockerUrl = dockerUrl;
        }

        public LogService(LogFormat format)
        {
            _serializer = format == LogFormat.Xml
                ? new XmlLogSerializer()
                : new JsonLogSerializer();

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _logFolder = Path.Combine(appData, "EasySave", "Logs");

            _mode = LogMode.Local;
            _dockerUrl = string.Empty;
        }

        public LogService(ILogSerializer serializer)
        {
            _serializer = serializer;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _logFolder = Path.Combine(appData, "EasySave", "Logs");
        }

        public void Save(LogEntry entry)
        {
            if (!Directory.Exists(_logFolder))
                Directory.CreateDirectory(_logFolder);

            string filePath = Path.Combine(
                _logFolder,
                $"{DateTime.Now:yyyy-MM-dd}.{_serializer.FileExtension}"
            );

            List<LogEntry> logs = new List<LogEntry>();

            if (File.Exists(filePath))
            {
                try
                {
                    logs = _serializer.Load(filePath);
                }
                catch
                {
                    logs = new List<LogEntry>();
                }
            }

            logs.Add(entry);
            _serializer.Save(logs, filePath);

            // DOCKER CENTRAL LOGGING
            if (_mode == LogMode.Centralized || _mode == LogMode.Both)
            {
                SendToDocker(entry);
            }
        }

        // DOCKER SENDER (SAFE)
        private async void SendToDocker(LogEntry entry)
        {
            try
            {
                using HttpClient client = new HttpClient();

                var payload = new
                {
                    machineName = Environment.MachineName,
                    userName = Environment.UserName,
                    backupName = entry.BackupName,
                    sourceFilePath = entry.SourceFilePath,
                    targetFilePath = entry.TargetFilePath,
                    fileSize = entry.FileSize,
                    fileTransferTimeMs = entry.FileTransferTimeMs,
                    encryptionTimeMs = entry.EncryptionTimeMs,
                    eventName = entry.Event,
                    timestamp = entry.Timestamp
                };

                await client.PostAsJsonAsync(_dockerUrl, payload);
            }
            catch
            {
                // to never break backup process
            }
        }
    }
}