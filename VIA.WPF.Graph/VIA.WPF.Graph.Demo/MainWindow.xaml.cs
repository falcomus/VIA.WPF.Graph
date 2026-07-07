using System.Windows;
using VIA.WPF.Graph.Demo.ViewModels;

namespace VIA.WPF.Graph.Demo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new GraphvizVerificationViewModel();
    }
}
