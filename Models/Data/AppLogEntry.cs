namespace EasySaveWpf;

public class AppLogEntry
{
    public string Timestamp { get; set; } = string.Empty;
    public string BackupName { get; set; } = string.Empty;
    public string SourceFilePath { get; set; } = string.Empty;
    public string TargetFilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public long FileTransferTimeMs { get; set; }
    public long EncryptionTimeMs { get; set; }
    public string Event { get; set; } = string.Empty;
}