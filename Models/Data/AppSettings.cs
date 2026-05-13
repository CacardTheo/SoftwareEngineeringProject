using EasyLog;

namespace EasySaveWpf;


public class AppSettings
{
    public string Language { get; set; } = "en";
    public List<string> EncryptedExtensions { get; set; } = new();
    public string EncryptionKey { get; set; } = string.Empty;
    public List<string> BusinessSoftwareProcesses { get; set; } = new();
    public LogFormat LogFormat { get; set; } = LogFormat.Json;
    public LogFormat StateFormat { get; set; } = LogFormat.Json;
    public LogMode LogMode { get; set; } = LogMode.Local;
    public string DockerLogServerUrl { get; set; } = "127.0.0.1:5132";
    public List<string> PrioritizedExtensions { get; set; } = new();
    public int LargeFileSizeThresholdKb { get; set; } = 0;
}
