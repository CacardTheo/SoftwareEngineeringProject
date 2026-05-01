using EasySaveWpf.ViewModels;

namespace EasySaveWpf;

public class BackupExecutionContext
{
    public DailyLogManager LogManager { get; init; } = null!;
    public OutputFormat LogFormat { get; init; } = OutputFormat.Json;
    public AppSettings Settings { get; init; } = null!;
    public CryptoSoftService CryptoService { get; init; } = null!;
    public Action<string, string, long> OnFileCopied { get; init; } = null!;
    public Func<bool> CanCopyNextFile { get; init; } = null!;
}
