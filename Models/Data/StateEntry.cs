using System; 

namespace EasySaveWpf
{
    public class StateEntry
    {
        public string Name { get; set; }
        public BackupStatus State { get; set; }
        public int Progression { get; set; }
        public long TotalFilesSize { get; set; }
        public int TotalFilesToCopy { get; set; }
        public string SourceFilePath { get; set; }
        public string TargetFilePath { get; set; }
        public int NbFilesLeftToDo { get; set; }
        public long SizeRemaining { get; set; }
        public DateTime LastRun { get; set; }
    }
}