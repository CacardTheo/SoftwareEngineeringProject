using EasyLog;

namespace EasySaveWpf.ViewModels;

public sealed class LogModeDisplayOption
{
    public LogMode Mode { get; }
    public string Label { get; }

    public LogModeDisplayOption(LogMode mode, string label)
    {
        Mode = mode;
        Label = label;
    }
}
