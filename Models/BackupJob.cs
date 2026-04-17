using System;
using System.Collections.Generic;
using System.Text;

namespace SoftwareEngineeringProject
{
    public class BackupJob
    {
        public string Name { get; set; }
        public string SourceDir { get; set; }
        public string TargetDir { get; set; }
        public readonly IBackupStrategy _strategy;

        public BackupJob (string name, string source, string target, IBackupStrategy strategy)
        {
            Name = name;
            SourceDir = source;
            TargetDir = target;
            _strategy = strategy;
        }

        public void Execute()
        {
            _strategy.Copy(SourceDir, TargetDir);
        }
    }
}
