using Avalonia;
using EasySaveWpf.ViewModels;

namespace EasySaveWpf;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            RunCli(args[0]);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void RunCli(string input)
    {
        var model = new MainViewModel();
        bool success = model.RunJob(input);
        Environment.Exit(success ? 0 : 1);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
