namespace EasySaveWpf;

public class BackupProgressEventArgs : EventArgs
{
    public string JobName { get; init; } = string.Empty;
    public BackupStatus Status { get; init; }
    public int Progression { get; init; }
    public string CurrentFile { get; init; } = string.Empty;
    public bool BlockedByBusinessSoftware { get; init; }
}
