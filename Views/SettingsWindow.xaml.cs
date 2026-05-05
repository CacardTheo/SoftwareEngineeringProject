using Avalonia.Markup.Xaml;
using EasySaveWpf.ViewModels;
using EasySaveWpf;

namespace EasySaveWpf.Views;

public partial class SettingsWindow : Avalonia.Controls.Window
{
    private readonly TaskCompletionSource<AppSettings?> _tcs = new();

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();

        var vm = new SettingsManager(current);
        DataContext = vm;

        vm.Saved += settings =>
        {
            _tcs.TrySetResult(settings);
            Close();
        };

        vm.Cancelled += () =>
        {
            _tcs.TrySetResult(null);
            Close();
        };

        Closed += (_, _) => _tcs.TrySetResult(null);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public Task<AppSettings?> GetResultAsync() => _tcs.Task;
}
