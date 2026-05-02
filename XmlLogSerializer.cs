using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace EasyLog
{
    public class XmlLogSerializer : ILogSerializer
    {
        public string FileExtension => "xml";

        public List<LogEntry> Load(string filePath)
        {
            var serializer = new XmlSerializer(typeof(List<LogEntry>));
            using var reader = new StreamReader(filePath);
            return (List<LogEntry>?)serializer.Deserialize(reader) ?? new List<LogEntry>();
        }

        public void Save(List<LogEntry> logs, string filePath)
        {
            var serializer = new XmlSerializer(typeof(List<LogEntry>));
            using var writer = new StreamWriter(filePath);
            serializer.Serialize(writer, logs);
        }
    }
}
