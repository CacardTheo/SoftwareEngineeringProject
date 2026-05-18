using EasyLog;

namespace EasySaveWpf.Services;

/// <summary>
/// Routes log entries according to AppSettings.LogMode.
/// </summary>
public sealed class BackupLogRouter
{
    private static readonly Lock _globalWriteLock = new();

    private readonly AppSettings _settings;
    private LogService? _localLogService;

    public BackupLogRouter(AppSettings settings) => _settings = settings;

    public void Save(LogEntry entry)
    {
        bool local = _settings.LogMode is LogMode.Local or LogMode.Both;
        bool central = _settings.LogMode is LogMode.Centralized or LogMode.Both;

        if (local)
        {
            lock (_globalWriteLock)
            {
                _localLogService ??= new LogService(_settings.LogFormat, LogMode.Local, string.Empty);
                _localLogService.Save(entry);
            }
        }

        if (central)
            CentralLogSocketClient.TrySendFireAndForget(_settings, entry);
    }
}
