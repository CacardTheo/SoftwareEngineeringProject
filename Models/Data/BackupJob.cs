using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;

namespace SoftwareEngineeringProject
{
    public class BackupJob
    {
        public string Name { get; set; }
        public string SourceDir { get; set; }
        public string TargetDir { get; set; }
        public BackupType Type { get; set; }

        public BackupJob (string name, string source, string target, BackupType type)
        {
            Validate(name, source, target);
            Name = name;
            SourceDir = source;
            TargetDir = target;
            Type = type;
        }

        public void Validate(string name, string source, string target)
        {
            if (string.IsNullOrWhiteSpace(name)) 
                throw new ArgumentException("Job name cannot be empty.");
            if (!Directory.Exists(source)) 
                throw new DirectoryNotFoundException($"Source directory not found: {source}");
            if (source.Equals(target, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Source and target directories must be different.");
        }
    }
}
