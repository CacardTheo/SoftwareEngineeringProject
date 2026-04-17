using System;
using System.Collections.Generic;
using System.Text;

namespace SoftwareEngineeringProject
{
    public enum BackupType
    {
        Full,
        Differential
    }
    public class BackupJob
    {
        public string Name { get; set; }
        public string SourceDirectory { get; set; }
        public string TargetDirectory { get; set; }
        public BackupType Type { get; set; }

        public BackupJob (string name, string source, string target, BackupType type)
        {
            Name = name;
            SourceDirectory = source;
            TargetDirectory = target;
            Type = type;
        }
    }
}
