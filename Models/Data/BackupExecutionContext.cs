using EasySaveWpf.ViewModels;
using EasyLog;

namespace EasySaveWpf;

public class BackupExecutionContext
{
    public LogService LogService { get; init; } = null!;
    public AppSettings Settings { get; init; } = null!;
    public CryptoSoftService CryptoService { get; init; } = null!;
    public Action<string, string, long> OnFileCopied { get; init; } = null!;
    public Func<bool> CanCopyNextFile { get; init; } = null!;
}
