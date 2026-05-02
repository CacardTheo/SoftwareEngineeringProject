using System.Collections.Generic;

namespace EasyLog
{
    public interface ILogSerializer
    {
        string FileExtension { get; }
        List<LogEntry> Load(string filePath);
        void Save(List<LogEntry> logs, string filePath);
    }
}
