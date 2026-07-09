using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Demo.ViewModels;

namespace VIA.WPF.Graph.Demo;

public partial class MainWindow : Window
{
    private const double MinimumFitPadding = 92d;
    private const double MaximumFitPadding = 132d;
    private const double FitPaddingViewportRatio = 0.115d;

    private readonly UIXDemoViewModel viewModel = new();
    private bool isUpdatingGraphScrollBars;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        GraphSurfaceHost.SizeChanged += OnGraphSurfaceHostSizeChanged;
        GraphSurfaceView.SizeChanged += OnGraphSurfaceViewSizeChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FitGraphAfterLayoutPass(force: true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        GraphSurfaceHost.SizeChanged -= OnGraphSurfaceHostSizeChanged;
        GraphSurfaceView.SizeChanged -= OnGraphSurfaceViewSizeChanged;
        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnGraphSurfaceHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        FitGraphAfterLayoutPass(force: false);
        UpdateGraphScrollBarsAfterLayoutPass();
    }

    private void OnGraphSurfaceViewSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGraphScrollBarsAfterLayoutPass();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UIXDemoViewModel.VisibleLayout)
            or nameof(UIXDemoViewModel.ActiveViewMode))
        {
            FitGraphAfterLayoutPass(force: false);
            UpdateGraphScrollBarsAfterLayoutPass();
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
            return;
        }

        if (e.PropertyName == nameof(UIXDemoViewModel.SelectedTreeNodeId))
        {
            ScrollSelectedNavigationTreeItemIntoViewAfterLayoutPass();
            return;
        }

        if (e.PropertyName is nameof(UIXDemoViewModel.Zoom)
            or nameof(UIXDemoViewModel.PanX)
            or nameof(UIXDemoViewModel.PanY))
        {
            UpdateGraphScrollBarsAfterLayoutPass();
        }
    }

    private void OnNavigationTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ProductNavigationTreeItem treeItem)
        {
            viewModel.SelectNavigationTreeItem(treeItem);
        }
    }

    private void ScrollSelectedNavigationTreeItemIntoViewAfterLayoutPass()
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                if (string.IsNullOrWhiteSpace(viewModel.SelectedTreeNodeId))
                {
                    return;
                }

                NavigationTree.UpdateLayout();
                TreeViewItem? selectedItem = FindNavigationTreeItem(NavigationTree, viewModel.SelectedTreeNodeId);
                if (selectedItem is null)
                {
                    return;
                }

                selectedItem.BringIntoView();
                CenterNavigationTreeItem(selectedItem);
            },
            DispatcherPriority.Loaded);
    }

    private static TreeViewItem? FindNavigationTreeItem(ItemsControl parent, string treeItemId)
    {
        parent.UpdateLayout();

        foreach (object item in parent.Items)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem treeViewItem)
            {
                continue;
            }

            if (item is ProductNavigationTreeItem treeItem
                && StringComparer.Ordinal.Equals(treeItem.TreeItemId, treeItemId))
            {
                return treeViewItem;
            }

            TreeViewItem? childItem = FindNavigationTreeItem(treeViewItem, treeItemId);
            if (childItem is not null)
            {
                return childItem;
            }
        }

        return null;
    }

    private void CenterNavigationTreeItem(TreeViewItem treeViewItem)
    {
        ScrollViewer? scrollViewer = FindVisualDescendant<ScrollViewer>(NavigationTree);
        if (scrollViewer is null || scrollViewer.ViewportHeight <= 0d)
        {
            return;
        }

        try
        {
            Point itemPosition = treeViewItem.TransformToAncestor(scrollViewer).Transform(new Point(0d, 0d));
            double targetOffset = scrollViewer.VerticalOffset
                + itemPosition.Y
                - ((scrollViewer.ViewportHeight - treeViewItem.ActualHeight) / 2d);
            scrollViewer.ScrollToVerticalOffset(Math.Clamp(targetOffset, 0d, scrollViewer.ScrollableHeight));
        }
        catch (InvalidOperationException)
        {
            treeViewItem.BringIntoView();
        }
    }

    private static T? FindVisualDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
            {
                return typedChild;
            }

            T? descendant = FindVisualDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void OnGraphHorizontalScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isUpdatingGraphScrollBars)
        {
            return;
        }

        GraphRect bounds = GraphSurfaceView.LayoutBounds;
        if (bounds.Width <= 0d)
        {
            return;
        }

        viewModel.IsFreeNavigationEnabled = true;
        GraphSurfaceView.PanX = -e.NewValue - (bounds.X * GraphSurfaceView.Zoom);
        UpdateGraphScrollBarsAfterLayoutPass();
    }

    private void OnGraphVerticalScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isUpdatingGraphScrollBars)
        {
            return;
        }

        GraphRect bounds = GraphSurfaceView.LayoutBounds;
        if (bounds.Height <= 0d)
        {
            return;
        }

        viewModel.IsFreeNavigationEnabled = true;
        GraphSurfaceView.PanY = -e.NewValue - (bounds.Y * GraphSurfaceView.Zoom);
        UpdateGraphScrollBarsAfterLayoutPass();
    }

    private void FitGraphAfterLayoutPass(bool force)
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                if (!force && viewModel.IsFreeNavigationEnabled)
                {
                    UpdateGraphScrollBars();
                    return;
                }

                Size? viewportSize = GetGraphViewportSizeOrNull();
                if (viewModel.VisibleLayout is not { Succeeded: true } || viewportSize is null)
                {
                    UpdateGraphScrollBars();
                    return;
                }

                GraphSurfaceView.FitToGraph(viewportSize.Value, GetGraphFitPadding(viewportSize.Value));
                UpdateGraphScrollBars();
            },
            DispatcherPriority.Loaded);
    }

    private static double GetGraphFitPadding(Size viewportSize)
    {
        double shortestSide = Math.Min(viewportSize.Width, viewportSize.Height);
        double adaptivePadding = shortestSide * FitPaddingViewportRatio;
        return Math.Clamp(adaptivePadding, MinimumFitPadding, MaximumFitPadding);
    }

    private void CenterSelectedGraphNodeAfterLayoutPass()
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                Size? viewportSize = GetGraphViewportSizeOrNull();
                if (viewModel.VisibleLayout is not { Succeeded: true } layout || viewportSize is null)
                {
                    UpdateGraphScrollBars();
                    return;
                }

                string? selectedNodeId = viewModel.SelectedNodeIds.FirstOrDefault() ?? viewModel.SelectedNodeId;
                GraphLayoutNode? selectedLayoutNode = selectedNodeId is null
                    ? null
                    : layout.Nodes.FirstOrDefault(node => StringComparer.Ordinal.Equals(node.NodeId, selectedNodeId));

                GraphRect targetBounds = selectedLayoutNode?.Bounds ?? GraphSurfaceView.LayoutBounds;
                if (targetBounds.Width <= 0d || targetBounds.Height <= 0d)
                {
                    UpdateGraphScrollBars();
                    return;
                }

                double contentCenterX = targetBounds.X + (targetBounds.Width / 2d);
                double contentCenterY = targetBounds.Y + (targetBounds.Height / 2d);
                GraphSurfaceView.PanX = (viewportSize.Value.Width / 2d) - (contentCenterX * GraphSurfaceView.Zoom);
                GraphSurfaceView.PanY = (viewportSize.Value.Height / 2d) - (contentCenterY * GraphSurfaceView.Zoom);
                UpdateGraphScrollBars();
            },
            DispatcherPriority.Loaded);
    }

    private void SetActualSizeAfterLayoutPass()
    {
        Dispatcher.BeginInvoke(
            () =>
            {
                GraphSurfaceView.Zoom = 1d;
                CenterSelectedGraphNodeAfterLayoutPass();
                UpdateGraphScrollBars();
            },
            DispatcherPriority.Loaded);
    }

    private void UpdateGraphScrollBarsAfterLayoutPass()
    {
        Dispatcher.BeginInvoke(UpdateGraphScrollBars, DispatcherPriority.Loaded);
    }

    private void UpdateGraphScrollBars()
    {
        if (isUpdatingGraphScrollBars)
        {
            return;
        }

        try
        {
            isUpdatingGraphScrollBars = true;
            Size? viewportSize = GetGraphViewportSizeOrNull();
            GraphRect bounds = GraphSurfaceView.LayoutBounds;
            if (viewModel.VisibleLayout is not { Succeeded: true }
                || viewportSize is null
                || bounds.Width <= 0d
                || bounds.Height <= 0d)
            {
                SetGraphScrollBarsVisible(horizontalVisible: false, verticalVisible: false);
                return;
            }

            double zoom = GraphSurfaceView.Zoom;
            double scaledWidth = bounds.Width * zoom;
            double scaledHeight = bounds.Height * zoom;
            bool horizontalVisible = scaledWidth > viewportSize.Value.Width + 1d;
            bool verticalVisible = scaledHeight > viewportSize.Value.Height + 1d;

            SetGraphScrollBarsVisible(horizontalVisible, verticalVisible);

            if (horizontalVisible)
            {
                double max = Math.Max(0d, scaledWidth - viewportSize.Value.Width);
                double value = Math.Clamp(-(bounds.X * zoom + GraphSurfaceView.PanX), 0d, max);
                GraphHorizontalScrollBar.Minimum = 0d;
                GraphHorizontalScrollBar.Maximum = max;
                GraphHorizontalScrollBar.ViewportSize = viewportSize.Value.Width;
                GraphHorizontalScrollBar.SmallChange = Math.Max(12d, viewportSize.Value.Width * 0.08d);
                GraphHorizontalScrollBar.LargeChange = Math.Max(24d, viewportSize.Value.Width * 0.72d);
                GraphHorizontalScrollBar.Value = value;
            }

            if (verticalVisible)
            {
                double max = Math.Max(0d, scaledHeight - viewportSize.Value.Height);
                double value = Math.Clamp(-(bounds.Y * zoom + GraphSurfaceView.PanY), 0d, max);
                GraphVerticalScrollBar.Minimum = 0d;
                GraphVerticalScrollBar.Maximum = max;
                GraphVerticalScrollBar.ViewportSize = viewportSize.Value.Height;
                GraphVerticalScrollBar.SmallChange = Math.Max(12d, viewportSize.Value.Height * 0.08d);
                GraphVerticalScrollBar.LargeChange = Math.Max(24d, viewportSize.Value.Height * 0.72d);
                GraphVerticalScrollBar.Value = value;
            }
        }
        finally
        {
            isUpdatingGraphScrollBars = false;
        }
    }

    private void SetGraphScrollBarsVisible(bool horizontalVisible, bool verticalVisible)
    {
        GraphHorizontalScrollBar.Visibility = horizontalVisible ? Visibility.Visible : Visibility.Collapsed;
        GraphVerticalScrollBar.Visibility = verticalVisible ? Visibility.Visible : Visibility.Collapsed;
        GraphScrollCorner.Visibility = horizontalVisible && verticalVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private Size? GetGraphViewportSizeOrNull()
    {
        double viewportWidth = GraphSurfaceView.ActualWidth;
        double viewportHeight = GraphSurfaceView.ActualHeight;

        if (viewportWidth <= 1d || viewportHeight <= 1d)
        {
            viewportWidth = GraphSurfaceHost.ActualWidth - GraphSurfaceHost.BorderThickness.Left - GraphSurfaceHost.BorderThickness.Right;
            viewportHeight = GraphSurfaceHost.ActualHeight - GraphSurfaceHost.BorderThickness.Top - GraphSurfaceHost.BorderThickness.Bottom;
        }

        return viewportWidth <= 1d || viewportHeight <= 1d
            ? null
            : new Size(viewportWidth, viewportHeight);
    }
}
