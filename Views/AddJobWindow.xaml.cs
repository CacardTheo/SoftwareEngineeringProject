using Avalonia.Markup.Xaml;
using EasySaveWpf.ViewModels;
using EasySaveWpf;

namespace EasySaveWpf.Views;

public partial class AddJobWindow : Avalonia.Controls.Window
{
    // Événements levés quand l'utilisateur confirme ou annule
    public event Action<BackupJob>? JobCreated;
    public event Action? Cancelled;

    public AddJobWindow()
    {
        InitializeComponent();

        var vm = new AddJobViewModel();
        DataContext = vm;

        vm.JobCreated += job =>
        {
            JobCreated?.Invoke(job);
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
