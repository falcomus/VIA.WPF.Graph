using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Demo.ViewModels;

namespace VIA.WPF.Graph.Demo.Views;

public partial class UIXDemoView : UserControl
{
    private readonly UIXDemoViewModel viewModel = new();

    public UIXDemoView()
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        GraphCanvasHost.SizeChanged += OnGraphCanvasHostSizeChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FitGraphAfterLayoutPass(force: true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        GraphCanvasHost.SizeChanged -= OnGraphCanvasHostSizeChanged;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnGraphCanvasHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        FitGraphAfterLayoutPass(force: false);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UIXDemoViewModel.CurrentLayout)
            or nameof(UIXDemoViewModel.ActiveViewMode))
        {
            FitGraphAfterLayoutPass(force: false);
            return;
        }

        if (e.PropertyName == nameof(UIXDemoViewModel.FitRequestVersion))
        {
            FitGraphAfterLayoutPass(force: true);
            return;
        }

        if (e.PropertyName == nameof(UIXDemoViewModel.CenterRequestVersion))
        {
            CenterGraphAfterLayoutPass();
            return;
        }

        if (e.PropertyName == nameof(UIXDemoViewModel.ActualSizeRequestVersion))
        {
            SetActualSizeAfterLayoutPass();
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

                GraphCanvasView.FitToGraph(viewportSize.Value, padding: 52d);
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

    private void SetActualSizeAfterLayoutPass()
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                GraphCanvasView.Zoom = 1d;
                CenterGraphAfterLayoutPass();
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
