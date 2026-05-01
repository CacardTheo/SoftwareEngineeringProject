using Avalonia.Markup.Xaml;
using EasySaveWpf.ViewModels;
using EasySaveWpf;

namespace EasySaveWpf.Views;

public partial class AddJobWindow : Avalonia.Controls.Window
{
    private readonly TaskCompletionSource<BackupJob?> _tcs = new();

    public AddJobWindow()
    {
        InitializeComponent();

        var vm = new AddJobViewModel();
        DataContext = vm;

        vm.JobCreated += job =>
        {
            _tcs.TrySetResult(job);
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

    public Task<BackupJob?> GetResultAsync() => _tcs.Task;
}
