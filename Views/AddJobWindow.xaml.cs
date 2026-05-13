using Avalonia.Markup.Xaml;
using EasySaveWpf.ViewModels;
using EasySaveWpf;

namespace EasySaveWpf.Views;

public partial class AddJobWindow : Avalonia.Controls.Window
{
    // Events raised when the user confirms or cancels
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
