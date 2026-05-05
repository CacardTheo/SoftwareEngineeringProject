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

        vm.RequestAddJob = async () =>
        {
            var dialog = new AddJobWindow();
            await dialog.ShowDialog(this);
            return await dialog.GetResultAsync();
        };

        vm.RequestSettings = async current =>
        {
            var dialog = new SettingsWindow(current);
            await dialog.ShowDialog(this);
            return await dialog.GetResultAsync();
        };

        DataContext = vm;
    }
}

