using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using VIA.WPF.Graph.Demo.ViewModels;

namespace VIA.WPF.Graph.Demo;

public partial class MainWindow : Window
{
    private readonly GraphvizVerificationViewModel viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        GraphCanvasHost.SizeChanged += OnGraphCanvasHostSizeChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        GraphCanvasHost.SizeChanged -= OnGraphCanvasHostSizeChanged;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosed(e);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FitGraphAfterLayoutPass();
    }

    private void OnGraphCanvasHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        FitGraphAfterLayoutPass();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GraphvizVerificationViewModel.CurrentLayout)
            or nameof(GraphvizVerificationViewModel.ActiveViewMode))
        {
            FitGraphAfterLayoutPass();
        }
    }

    private void FitGraphAfterLayoutPass()
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                double viewportWidth = GraphCanvasHost.ActualWidth - GraphCanvasHost.BorderThickness.Left - GraphCanvasHost.BorderThickness.Right;
                double viewportHeight = GraphCanvasHost.ActualHeight - GraphCanvasHost.BorderThickness.Top - GraphCanvasHost.BorderThickness.Bottom;

                if (viewModel.CurrentLayout is not { Succeeded: true }
                    || viewportWidth <= 1d
                    || viewportHeight <= 1d)
                {
                    return;
                }

                GraphCanvasView.FitToGraph(new Size(viewportWidth, viewportHeight));
            },
            DispatcherPriority.Loaded);
    }
}
