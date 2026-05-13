using Avalonia.Controls;
using EasySaveWpf;
using EasySaveWpf.ViewModels;

namespace EasySaveWpf.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();

        _vm = new MainViewModel();

        // Pass callbacks to the ViewModel so it can request window openings without knowing the View layer
        _vm.RequestAddJob = callback =>
        {
            var dialog = new AddJobWindow();
            dialog.JobCreated += job => callback(job);
            dialog.Cancelled += () => callback(null);
            dialog.Show(this);
        };

        _vm.RequestSettings = (current, callback) =>
        {
            var dialog = new SettingsWindow(current);
            dialog.Saved += settings => callback(settings);
            dialog.Cancelled += () => callback(null);
            dialog.Show(this);
        };

        DataContext = _vm;
        Closing += (_, _) => _vm.Cleanup();
    }
}
