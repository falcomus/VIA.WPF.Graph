using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Demo.ViewModels;

namespace VIA.WPF.Graph.Demo;

public partial class MainWindow : Window
{
    private readonly UIXDemoViewModel viewModel = new();

    public MainWindow()
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
            CenterSelectedGraphNodeAfterLayoutPass();
            return;
        }

        if (e.PropertyName == nameof(UIXDemoViewModel.ActualSizeRequestVersion))
        {
            SetActualSizeAfterLayoutPass();
        }
    }

    private void OnNavigationTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ProductNavigationTreeItem treeItem)
        {
            viewModel.SelectNavigationTreeItem(treeItem);
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

    private void CenterSelectedGraphNodeAfterLayoutPass()
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                Size? viewportSize = GetGraphViewportSizeOrNull();
                if (viewModel.CurrentLayout is not { Succeeded: true } layout || viewportSize is null)
                {
                    return;
                }

                string? selectedNodeId = viewModel.SelectedNodeIds.FirstOrDefault() ?? viewModel.SelectedNodeId;
                GraphLayoutNode? selectedLayoutNode = selectedNodeId is null
                    ? null
                    : layout.Nodes.FirstOrDefault(node => StringComparer.Ordinal.Equals(node.NodeId, selectedNodeId));

                GraphRect targetBounds = selectedLayoutNode?.Bounds ?? GraphCanvasView.LayoutBounds;
                if (targetBounds.Width <= 0d || targetBounds.Height <= 0d)
                {
                    return;
                }

                double contentCenterX = targetBounds.X + (targetBounds.Width / 2d);
                double contentCenterY = targetBounds.Y + (targetBounds.Height / 2d);
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
                CenterSelectedGraphNodeAfterLayoutPass();
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
