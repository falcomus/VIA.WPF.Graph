using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using VIA.WPF.Graph.Core.Layout;
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
        FitGraphAfterLayoutPass(force: true);
    }

    private void OnGraphCanvasHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        FitGraphAfterLayoutPass(force: false);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GraphvizVerificationViewModel.CurrentLayout)
            or nameof(GraphvizVerificationViewModel.ActiveViewMode))
        {
            FitGraphAfterLayoutPass(force: false);
            return;
        }

        if (e.PropertyName == nameof(GraphvizVerificationViewModel.FitRequestVersion))
        {
            FitGraphAfterLayoutPass(force: true);
            return;
        }

        if (e.PropertyName == nameof(GraphvizVerificationViewModel.CenterRequestVersion))
        {
            CenterGraphAfterLayoutPass();
        }
    }

    private void FitGraphAfterLayoutPass(bool force)
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                if (!force && viewModel.IsFreeNavigationEnabled)
                {
                    return;
                }

                Size? viewportSize = GetGraphViewportSizeOrNull();
                if (viewModel.CurrentLayout is not { Succeeded: true } || viewportSize is null)
                {
                    return;
                }

                GraphCanvasView.FitToGraph(viewportSize.Value);
            },
            DispatcherPriority.Loaded);
    }

    private void CenterGraphAfterLayoutPass()
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                Size? viewportSize = GetGraphViewportSizeOrNull();
                GraphRect bounds = GraphCanvasView.LayoutBounds;
                if (viewModel.CurrentLayout is not { Succeeded: true }
                    || viewportSize is null
                    || bounds.Width <= 0d
                    || bounds.Height <= 0d)
                {
                    return;
                }

                double contentCenterX = bounds.X + (bounds.Width / 2d);
                double contentCenterY = bounds.Y + (bounds.Height / 2d);
                GraphCanvasView.PanX = (viewportSize.Value.Width / 2d) - (contentCenterX * GraphCanvasView.Zoom);
                GraphCanvasView.PanY = (viewportSize.Value.Height / 2d) - (contentCenterY * GraphCanvasView.Zoom);
            },
            DispatcherPriority.Loaded);
    }

    private Size? GetGraphViewportSizeOrNull()
    {
        double viewportWidth = GraphCanvasHost.ActualWidth - GraphCanvasHost.BorderThickness.Left - GraphCanvasHost.BorderThickness.Right;
        double viewportHeight = GraphCanvasHost.ActualHeight - GraphCanvasHost.BorderThickness.Top - GraphCanvasHost.BorderThickness.Bottom;

        return viewportWidth <= 1d || viewportHeight <= 1d
            ? null
            : new Size(viewportWidth, viewportHeight);
    }
}
