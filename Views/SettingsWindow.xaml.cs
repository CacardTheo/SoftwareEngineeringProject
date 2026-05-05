using Avalonia.Markup.Xaml;
using EasySaveWpf.ViewModels;
using EasySaveWpf;

namespace EasySaveWpf.Views;

public partial class SettingsWindow : Avalonia.Controls.Window
{
    // Événements levés quand l'utilisateur sauvegarde ou annule
    public event Action<AppSettings>? Saved;
    public event Action? Cancelled;

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();

        var vm = new SettingsViewModel(current);
        DataContext = vm;

        vm.Saved += settings =>
        {
            Saved?.Invoke(settings);
            Close();
        };

        vm.Cancelled += () =>
        {
            Cancelled?.Invoke();
            Close();
        };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
