using Avalonia.Controls;
using EasySaveWpf;
using EasySaveWpf.ViewModels;

namespace EasySaveWpf.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var vm = new MainViewModel();

        // On passe des callbacks au ViewModel pour qu'il puisse
        // demander l'ouverture d'une fenêtre sans connaître la View
        vm.RequestAddJob = callback =>
        {
            var dialog = new AddJobWindow();
            dialog.JobCreated += job => callback(job);
            dialog.Cancelled += () => callback(null);
            dialog.Show(this);
        };

        vm.RequestSettings = (current, callback) =>
        {
            var dialog = new SettingsWindow(current);
            dialog.Saved += settings => callback(settings);
            dialog.Cancelled += () => callback(null);
            dialog.Show(this);
        };

        DataContext = vm;
    }
}
