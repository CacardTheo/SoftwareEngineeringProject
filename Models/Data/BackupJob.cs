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
    }
}
