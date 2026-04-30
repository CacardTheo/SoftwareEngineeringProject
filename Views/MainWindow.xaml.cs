using Avalonia.Controls;
using EasySaveWpf.ViewModels;

namespace EasySaveWpf.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
