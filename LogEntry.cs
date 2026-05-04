using System;

namespace EasyLog
{
    public class LogEntry
    {
        // Format: yyyy-MM-dd HH:mm:ss as requested
        public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        public string? BackupName { get; set; }

        // Supports UNC format (e.g., \\Server\Share\File)
        public string? SourceFilePath { get; set; }

        public string? TargetFilePath { get; set; }

        public long FileSize { get; set; }

        // Milliseconds (will be set to -1 in the strategy if an error occurs)
        public long FileTransferTimeMs { get; set; }
        public long EncrytpionTimeMs { get; set; }
        public string Event { get; set; } = string.Empty;
    }
}