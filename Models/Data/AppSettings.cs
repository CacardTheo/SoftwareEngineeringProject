using EasySaveWpf.ViewModels;
namespace EasySaveWpf;

public class AppSettings
{
    public string Language { get; set; } = "en";
    public List<string> EncryptedExtensions { get; set; } = new();
    public string EncryptionKey { get; set; } = string.Empty;
    public List<string> BusinessSoftwareProcesses { get; set; } = new();
    public OutputFormat LogFormat { get; set; } = OutputFormat.Json;
    public OutputFormat StateFormat { get; set; } = OutputFormat.Json;
}