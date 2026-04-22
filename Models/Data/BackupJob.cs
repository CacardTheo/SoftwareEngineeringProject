namespace SoftwareEngineeringProject
{
    public class BackupJob
    {
        public string? Name { get; set; }
        public string? SourceDirectory { get; set; }
        public string? TargetDirectory { get; set; }
        public BackupType Type { get; set; }
    }
}