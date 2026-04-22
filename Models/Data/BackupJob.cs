namespace SoftwareEngineeringProject
{
    public class BackupJob
    {
        public string? Name { get; set; }
        public string? SourceDir { get; set; }
        public string? TargetDir { get; set; }
        public BackupType Type { get; set; }
    }
}