
namespace SoftwareEngineeringProject
{
    public class StateEntry
    {
        public string JobName { get; set; }
        public BackupStatus Status { get; set; }
        public int Progress { get; set; }
        public long SizeToTransfer { get; set; }
        public string CurrentSourceFile { get; set; }
        public string CurrentTargetFile { get; set; }
        public int NumberOfFilesRemaining { get; set; }
        public long SizeRemaining { get; set; }
        public DateTime LastRun { get; set; }
    }
}