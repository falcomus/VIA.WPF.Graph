using System.Windows;
using VIA.WPF.Graph.Demo.ViewModels;

namespace VIA.WPF.Graph.Demo;

public partial class MainWindow : Window
{
    private readonly UIXDemoViewModel viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
