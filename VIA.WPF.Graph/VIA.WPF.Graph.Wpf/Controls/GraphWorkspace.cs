using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Core.Projections;
using VIA.WPF.Graph.Core.Requests;

namespace VIA.WPF.Graph.Wpf.Controls;

/// <summary>
/// Host-neutral WPF workspace that coordinates a navigation tree and a Skia graph surface.
/// The host supplies neutral graph data, view state, capabilities and a layout engine boundary;
/// the workspace owns no host model and emits only neutral graph requests.
/// </summary>
public sealed class GraphWorkspace : Grid
{
    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document),
        typeof(GraphDocument),
        typeof(GraphWorkspace),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure,
            OnWorkspaceInputChanged));

    public static readonly DependencyProperty ViewStateProperty = DependencyProperty.Register(
        nameof(ViewState),
        typeof(GraphViewState),
        typeof(GraphWorkspace),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnViewStateChanged));

    public static readonly DependencyProperty LayoutEngineProperty = DependencyProperty.Register(
        nameof(LayoutEngine),
        typeof(IGraphLayoutEngine),
        typeof(GraphWorkspace),
        new FrameworkPropertyMetadata(
            null,
            OnWorkspaceInputChanged));

    public static readonly DependencyProperty HostCapabilitiesProperty = DependencyProperty.Register(
        nameof(HostCapabilities),
        typeof(GraphHostCapabilities),
        typeof(GraphWorkspace),
        new FrameworkPropertyMetadata(GraphHostCapabilities.ReadOnly()));

    public static readonly DependencyProperty GraphRequestCommandProperty = DependencyProperty.Register(
        nameof(GraphRequestCommand),
        typeof(ICommand),
        typeof(GraphWorkspace),
        new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty ShowNavigationTreeProperty = DependencyProperty.Register(
        nameof(ShowNavigationTree),
        typeof(bool),
        typeof(GraphWorkspace),
        new FrameworkPropertyMetadata(
            true,
            FrameworkPropertyMetadataOptions.AffectsMeasure,
            OnShowNavigationTreeChanged));

    public static readonly DependencyProperty LayoutDirectionProperty = DependencyProperty.Register(
        nameof(LayoutDirection),
        typeof(GraphLayoutDirection),
        typeof(GraphWorkspace),
        new FrameworkPropertyMetadata(
            GraphLayoutDirection.LeftToRight,
            OnWorkspaceInputChanged));

    public static readonly DependencyProperty EdgeRoutingStyleProperty = DependencyProperty.Register(
        nameof(EdgeRoutingStyle),
        typeof(GraphEdgeRoutingStyle),
        typeof(GraphWorkspace),
        new FrameworkPropertyMetadata(
            GraphEdgeRoutingStyle.Spline,
            OnWorkspaceInputChanged));

    public static readonly DependencyProperty VisualDensityProperty = DependencyProperty.Register(
        nameof(VisualDensity),
        typeof(double),
        typeof(GraphWorkspace),
        new FrameworkPropertyMetadata(
            1d,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnVisualDensityChanged,
            CoerceVisualDensity),
        IsFinitePositiveDouble);

    private static readonly DependencyPropertyKey TreeProjectionPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(TreeProjection),
        typeof(GraphTreeProjection),
        typeof(GraphWorkspace),
        new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty TreeProjectionProperty = TreeProjectionPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey VisibleDocumentPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(VisibleDocument),
        typeof(GraphDocument),
        typeof(GraphWorkspace),
        new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty VisibleDocumentProperty = VisibleDocumentPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey VisibleLayoutPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(VisibleLayout),
        typeof(GraphLayoutResult),
        typeof(GraphWorkspace),
        new FrameworkPropertyMetadata(null));

    public static readonly DependencyProperty VisibleLayoutProperty = VisibleLayoutPropertyKey.DependencyProperty;

    private const int GroupCompactMaxVisibleNodes = 20;
    private const int BranchMaxVisibleNodes = 22;
    private const double NavigationTreeWidth = 260d;
    private const double SplitterWidth = 5d;
    private const double DefaultFitPadding = 72d;

    private readonly ColumnDefinition navigationColumn = new() { Width = new GridLength(NavigationTreeWidth) };
    private readonly ColumnDefinition splitterColumn = new() { Width = new GridLength(SplitterWidth) };
    private readonly ColumnDefinition graphColumn = new() { Width = new GridLength(1d, GridUnitType.Star) };
    private readonly ScrollViewer navigationScrollViewer;
    private readonly GridSplitter splitter;
    private readonly Grid graphSurfaceHost;
    private readonly ScrollBar horizontalScrollBar;
    private readonly ScrollBar verticalScrollBar;
    private readonly Border scrollCorner;
    private Button navigationToggleButton = null!;
    private Button densityButton = null!;
    private ComboBox layoutDirectionComboBox = null!;
    private ComboBox edgeRoutingStyleComboBox = null!;
    private readonly GraphWorkspaceRequestCommand workspaceRequestCommand;

    private bool isUpdatingFromChild;
    private bool isApplyingViewStateToChildren;
    private bool isFitRequested;
    private bool isUpdatingGraphScrollBars;
    private bool isUpdatingToolbarSelections;

    public GraphWorkspace()
    {
        ClipToBounds = true;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        ColumnDefinitions.Add(navigationColumn);
        ColumnDefinitions.Add(splitterColumn);
        ColumnDefinitions.Add(graphColumn);

        workspaceRequestCommand = new GraphWorkspaceRequestCommand(this);

        Border toolbar = CreateToolbar();
        SetRow(toolbar, 0);
        SetColumn(toolbar, 0);
        SetColumnSpan(toolbar, 3);
        Children.Add(toolbar);

        NavigationTree = new GraphNavigationPathTree
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            GraphRequestCommand = workspaceRequestCommand
        };

        navigationScrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            CanContentScroll = false,
            Content = NavigationTree
        };
        SetRow(navigationScrollViewer, 1);
        SetColumn(navigationScrollViewer, 0);
        Children.Add(navigationScrollViewer);

        splitter = new GridSplitter
        {
            Width = SplitterWidth,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = new SolidColorBrush(Color.FromRgb(216, 226, 236)),
            ShowsPreview = true
        };
        SetRow(splitter, 1);
        SetColumn(splitter, 1);
        Children.Add(splitter);

        GraphSurface = new SkiaGraphSurface
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            GraphRequestCommand = workspaceRequestCommand
        };

        graphSurfaceHost = new Grid
        {
            ClipToBounds = true,
            Background = new SolidColorBrush(Color.FromRgb(244, 246, 248))
        };
        graphSurfaceHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1d, GridUnitType.Star) });
        graphSurfaceHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        graphSurfaceHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        graphSurfaceHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        SetRow(GraphSurface, 0);
        SetColumn(GraphSurface, 0);
        graphSurfaceHost.Children.Add(GraphSurface);

        horizontalScrollBar = new ScrollBar
        {
            Height = 13d,
            Orientation = Orientation.Horizontal,
            Visibility = Visibility.Collapsed,
            Opacity = 0.82d,
            Margin = new Thickness(2d, 0d, 2d, 2d)
        };
        horizontalScrollBar.ValueChanged += OnHorizontalScrollBarValueChanged;
        SetRow(horizontalScrollBar, 1);
        SetColumn(horizontalScrollBar, 0);
        graphSurfaceHost.Children.Add(horizontalScrollBar);

        verticalScrollBar = new ScrollBar
        {
            Width = 13d,
            Orientation = Orientation.Vertical,
            Visibility = Visibility.Collapsed,
            Opacity = 0.82d,
            Margin = new Thickness(0d, 2d, 2d, 2d)
        };
        verticalScrollBar.ValueChanged += OnVerticalScrollBarValueChanged;
        SetRow(verticalScrollBar, 0);
        SetColumn(verticalScrollBar, 1);
        graphSurfaceHost.Children.Add(verticalScrollBar);

        scrollCorner = new Border
        {
            Width = 13d,
            Height = 13d,
            Margin = new Thickness(0d, 0d, 2d, 2d),
            Background = new SolidColorBrush(Color.FromRgb(231, 236, 241)),
            Visibility = Visibility.Collapsed
        };
        SetRow(scrollCorner, 1);
        SetColumn(scrollCorner, 1);
        graphSurfaceHost.Children.Add(scrollCorner);

        SetRow(graphSurfaceHost, 1);
        SetColumn(graphSurfaceHost, 2);
        Children.Add(graphSurfaceHost);

        SizeChanged += OnWorkspaceSizeChanged;
        GraphSurface.SizeChanged += OnGraphSurfaceSizeChanged;
        DependencyPropertyDescriptor.FromProperty(SkiaGraphSurface.ZoomProperty, typeof(SkiaGraphSurface))
            .AddValueChanged(GraphSurface, OnGraphSurfaceViewportChanged);
        DependencyPropertyDescriptor.FromProperty(SkiaGraphSurface.PanXProperty, typeof(SkiaGraphSurface))
            .AddValueChanged(GraphSurface, OnGraphSurfaceViewportChanged);
        DependencyPropertyDescriptor.FromProperty(SkiaGraphSurface.PanYProperty, typeof(SkiaGraphSurface))
            .AddValueChanged(GraphSurface, OnGraphSurfaceViewportChanged);

        UpdateNavigationTreeVisibility();
        UpdateToolbarSelections();
        RebuildWorkspace();
    }

    private Border CreateToolbar()
    {
        StackPanel panel = new()
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(CreateToolbarButton("Focus", () => SetActiveViewMode(GraphViewMode.Focus)));
        panel.Children.Add(CreateToolbarButton("Branch", () => SetActiveViewMode(GraphViewMode.Tree)));
        panel.Children.Add(CreateToolbarButton("Group", () => SetActiveViewMode(GraphViewMode.Group)));
        panel.Children.Add(CreateToolbarButton("Overview", () => SetActiveViewMode(GraphViewMode.Overview)));
        panel.Children.Add(CreateToolbarSeparator());
        panel.Children.Add(CreateToolbarButton("Fit", FitToGraph));
        panel.Children.Add(CreateToolbarButton("100 %", SetActualSize));
        panel.Children.Add(CreateToolbarButton("Center", () => _ = CenterSelection()));
        panel.Children.Add(CreateToolbarSeparator());

        navigationToggleButton = CreateToolbarButton("Tree", () => ShowNavigationTree = !ShowNavigationTree);
        panel.Children.Add(navigationToggleButton);

        densityButton = CreateToolbarButton("Compact", ToggleVisualDensity);
        panel.Children.Add(densityButton);
        panel.Children.Add(CreateToolbarSeparator());
        panel.Children.Add(CreateToolbarLabel("Layout"));

        layoutDirectionComboBox = new ComboBox
        {
            Width = 118d,
            MinHeight = 26d,
            Margin = new Thickness(4d, 0d, 8d, 0d),
            ItemsSource = Enum.GetValues(typeof(GraphLayoutDirection))
        };
        layoutDirectionComboBox.SelectionChanged += OnLayoutDirectionComboBoxSelectionChanged;
        panel.Children.Add(layoutDirectionComboBox);

        panel.Children.Add(CreateToolbarLabel("Routing"));
        edgeRoutingStyleComboBox = new ComboBox
        {
            Width = 110d,
            MinHeight = 26d,
            Margin = new Thickness(4d, 0d, 0d, 0d),
            ItemsSource = Enum.GetValues(typeof(GraphEdgeRoutingStyle))
        };
        edgeRoutingStyleComboBox.SelectionChanged += OnEdgeRoutingStyleComboBoxSelectionChanged;
        panel.Children.Add(edgeRoutingStyleComboBox);

        return new Border
        {
            Padding = new Thickness(10d, 8d, 10d, 8d),
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(218, 228, 238)),
            BorderThickness = new Thickness(0d, 0d, 0d, 1d),
            Child = panel
        };
    }

    private static Button CreateToolbarButton(string content, Action action)
    {
        Button button = new()
        {
            Content = content,
            MinHeight = 28d,
            Padding = new Thickness(10d, 4d, 10d, 4d),
            Margin = new Thickness(0d, 0d, 6d, 0d),
            FontWeight = FontWeights.SemiBold,
            Cursor = Cursors.Hand
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static TextBlock CreateToolbarLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(76, 92, 108))
        };
    }

    private static Border CreateToolbarSeparator()
    {
        return new Border
        {
            Width = 1d,
            Height = 22d,
            Margin = new Thickness(4d, 0d, 10d, 0d),
            Background = new SolidColorBrush(Color.FromRgb(216, 226, 236))
        };
    }

    public GraphDocument? Document
    {
        get => (GraphDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public GraphViewState? ViewState
    {
        get => (GraphViewState?)GetValue(ViewStateProperty);
        set => SetValue(ViewStateProperty, value);
    }

    public IGraphLayoutEngine? LayoutEngine
    {
        get => (IGraphLayoutEngine?)GetValue(LayoutEngineProperty);
        set => SetValue(LayoutEngineProperty, value);
    }

    public GraphHostCapabilities HostCapabilities
    {
        get => (GraphHostCapabilities?)GetValue(HostCapabilitiesProperty) ?? GraphHostCapabilities.ReadOnly();
        set => SetValue(HostCapabilitiesProperty, value);
    }

    public ICommand? GraphRequestCommand
    {
        get => (ICommand?)GetValue(GraphRequestCommandProperty);
        set => SetValue(GraphRequestCommandProperty, value);
    }

    public bool ShowNavigationTree
    {
        get => (bool)GetValue(ShowNavigationTreeProperty);
        set => SetValue(ShowNavigationTreeProperty, value);
    }

    public GraphLayoutDirection LayoutDirection
    {
        get => (GraphLayoutDirection)GetValue(LayoutDirectionProperty);
        set => SetValue(LayoutDirectionProperty, value);
    }

    public GraphEdgeRoutingStyle EdgeRoutingStyle
    {
        get => (GraphEdgeRoutingStyle)GetValue(EdgeRoutingStyleProperty);
        set => SetValue(EdgeRoutingStyleProperty, value);
    }

    public double VisualDensity
    {
        get => (double)GetValue(VisualDensityProperty);
        set => SetValue(VisualDensityProperty, value);
    }

    public GraphTreeProjection? TreeProjection => (GraphTreeProjection?)GetValue(TreeProjectionProperty);

    public GraphDocument? VisibleDocument => (GraphDocument?)GetValue(VisibleDocumentProperty);

    public GraphLayoutResult? VisibleLayout => (GraphLayoutResult?)GetValue(VisibleLayoutProperty);

    public GraphNavigationPathTree NavigationTree { get; }

    public SkiaGraphSurface GraphSurface { get; }

    public void FitToGraph()
    {
        Size viewportSize = new(Math.Max(0d, GraphSurface.ActualWidth), Math.Max(0d, GraphSurface.ActualHeight));
        if (viewportSize.Width <= 0d || viewportSize.Height <= 0d)
        {
            GraphSurface.FitToGraph();
            UpdateGraphScrollBarsAfterLayoutPass();
            return;
        }

        GraphSurface.FitToGraph(viewportSize, DefaultFitPadding);
        UpdateGraphScrollBarsAfterLayoutPass();
    }

    public void SetActualSize()
    {
        GraphSurface.Zoom = 1d;
        if (!CenterSelection())
        {
            GraphSurface.PanX = 0d;
            GraphSurface.PanY = 0d;
        }

        UpdateGraphScrollBarsAfterLayoutPass();
    }

    public bool CenterSelection()
    {
        GraphLayoutResult? layout = VisibleLayout;
        string? selectedNodeId = EffectiveViewState.Selection.SelectedNodeIds.FirstOrDefault() ?? EffectiveViewState.ActiveNodeId;
        if (layout is not { Succeeded: true } || string.IsNullOrWhiteSpace(selectedNodeId))
        {
            return false;
        }

        GraphLayoutNode? node = layout.Nodes.FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.NodeId, selectedNodeId));
        if (node is null || GraphSurface.ActualWidth <= 0d || GraphSurface.ActualHeight <= 0d)
        {
            return false;
        }

        double centerX = node.Bounds.X + (node.Bounds.Width / 2d);
        double centerY = node.Bounds.Y + (node.Bounds.Height / 2d);
        GraphSurface.PanX = (GraphSurface.ActualWidth / 2d) - (centerX * GraphSurface.Zoom);
        GraphSurface.PanY = (GraphSurface.ActualHeight / 2d) - (centerY * GraphSurface.Zoom);
        UpdateGraphScrollBarsAfterLayoutPass();
        return true;
    }

    private void SetActiveViewMode(GraphViewMode viewMode)
    {
        GraphViewState current = EffectiveViewState;
        string? activeNodeId = current.ActiveNodeId
            ?? current.Selection.SelectedNodeIds.FirstOrDefault()
            ?? Document?.Nodes.FirstOrDefault()?.Id;
        string? activeGroupId = current.ActiveGroupId ?? current.Selection.SelectedGroupIds.FirstOrDefault();

        if ((viewMode is GraphViewMode.Group or GraphViewMode.GroupOverview) && string.IsNullOrWhiteSpace(activeGroupId))
        {
            activeGroupId = ResolveFirstContainerGroupId(Document, activeNodeId);
        }

        GraphSelectionState selection = current.Selection;
        if (viewMode == GraphViewMode.Focus && !string.IsNullOrWhiteSpace(activeNodeId))
        {
            selection = new GraphSelectionState(selectedNodeIds: [activeNodeId]);
        }
        else if (viewMode is GraphViewMode.Group or GraphViewMode.GroupOverview && !string.IsNullOrWhiteSpace(activeGroupId))
        {
            selection = new GraphSelectionState(
                selectedNodeIds: current.Selection.SelectedNodeIds,
                selectedGroupIds: [activeGroupId]);
        }

        SetCurrentValue(ViewStateProperty, new GraphViewState(
            viewMode,
            viewMode is GraphViewMode.Overview or GraphViewMode.Diagnostic ? current.ActiveNodeId : activeNodeId,
            viewMode is GraphViewMode.Group or GraphViewMode.GroupOverview ? activeGroupId : null,
            selection,
            current.Viewport,
            current.CollapsedContainerGroupIds,
            current.ExpandedTreeItemIds));
        RebuildWorkspace();
        RequestFitAfterLayoutPass();
    }

    private void ToggleVisualDensity()
    {
        VisualDensity = VisualDensity < 1d ? 1.08d : 0.82d;
    }

    private GraphViewState EffectiveViewState => ViewState ?? GraphViewState.Default;

    private static void OnWorkspaceInputChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        GraphWorkspace workspace = (GraphWorkspace)dependencyObject;
        workspace.UpdateToolbarSelections();
        workspace.RebuildWorkspace();
    }

    private static void OnViewStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        GraphWorkspace workspace = (GraphWorkspace)dependencyObject;
        GraphViewState oldState = (GraphViewState?)e.OldValue ?? GraphViewState.Default;
        GraphViewState newState = (GraphViewState?)e.NewValue ?? GraphViewState.Default;

        if (RequiresVisibleDocumentRebuild(oldState, newState))
        {
            workspace.RebuildWorkspace();
            return;
        }

        workspace.ApplyViewStateToChildren(newState);
    }

    private static bool RequiresVisibleDocumentRebuild(GraphViewState oldState, GraphViewState newState)
    {
        return oldState.ActiveViewMode != newState.ActiveViewMode
            || !StringComparer.Ordinal.Equals(oldState.ActiveNodeId, newState.ActiveNodeId)
            || !StringComparer.Ordinal.Equals(oldState.ActiveGroupId, newState.ActiveGroupId)
            || !oldState.Selection.SelectedNodeIds.SequenceEqual(newState.Selection.SelectedNodeIds, StringComparer.Ordinal)
            || !oldState.Selection.SelectedLinkIds.SequenceEqual(newState.Selection.SelectedLinkIds, StringComparer.Ordinal)
            || !oldState.Selection.SelectedGroupIds.SequenceEqual(newState.Selection.SelectedGroupIds, StringComparer.Ordinal)
            || !oldState.CollapsedContainerGroupIds.SequenceEqual(newState.CollapsedContainerGroupIds, StringComparer.Ordinal)
            || !oldState.ExpandedTreeItemIds.SequenceEqual(newState.ExpandedTreeItemIds, StringComparer.Ordinal);
    }

    private static void OnShowNavigationTreeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        GraphWorkspace workspace = (GraphWorkspace)dependencyObject;
        workspace.UpdateNavigationTreeVisibility();
        workspace.RebuildWorkspace();
    }

    private static void OnVisualDensityChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        GraphWorkspace workspace = (GraphWorkspace)dependencyObject;
        workspace.GraphSurface.VisualDensity = workspace.VisualDensity;
        workspace.UpdateToolbarSelections();
    }

    private static object CoerceVisualDensity(DependencyObject dependencyObject, object baseValue)
    {
        double value = (double)baseValue;
        if (!double.IsFinite(value))
        {
            return 1d;
        }

        return Math.Clamp(value, 0.55d, 1.35d);
    }

    private static bool IsFinitePositiveDouble(object value)
    {
        return value is double doubleValue && double.IsFinite(doubleValue) && doubleValue > 0d;
    }

    private void RebuildWorkspace()
    {
        if (isUpdatingFromChild)
        {
            return;
        }

        GraphDocument? document = Document;
        if (document is null)
        {
            SetValue(TreeProjectionPropertyKey, null);
            SetValue(VisibleDocumentPropertyKey, null);
            SetValue(VisibleLayoutPropertyKey, null);
            NavigationTree.Projection = null;
            GraphSurface.Document = null;
            GraphSurface.LayoutResult = null;
            ApplyViewStateToChildren(GraphViewState.Default);
            UpdateGraphScrollBarsAfterLayoutPass();
            return;
        }

        GraphViewState viewState = EffectiveViewState;
        GraphTreeProjection projection = GraphTreeProjectionBuilder.Build(document);
        GraphDocument visibleDocument = CreateVisibleDocument(document, viewState);
        GraphLayoutOptions layoutOptions = new(LayoutDirection, EdgeRoutingStyle);
        GraphLayoutResult visibleLayout = CreateVisibleLayout(visibleDocument, layoutOptions);

        SetValue(TreeProjectionPropertyKey, projection);
        SetValue(VisibleDocumentPropertyKey, visibleDocument);
        SetValue(VisibleLayoutPropertyKey, visibleLayout);

        NavigationTree.Projection = projection;
        GraphSurface.Document = visibleDocument;
        GraphSurface.LayoutResult = visibleLayout;
        GraphSurface.VisualDensity = VisualDensity;
        ApplyViewStateToChildren(viewState);

        if (IsLoaded && visibleLayout.Succeeded)
        {
            RequestFitAfterLayoutPass();
        }
        else
        {
            UpdateGraphScrollBarsAfterLayoutPass();
        }
    }

    private GraphLayoutResult CreateVisibleLayout(GraphDocument visibleDocument, GraphLayoutOptions layoutOptions)
    {
        IGraphLayoutEngine? layoutEngine = LayoutEngine;
        if (layoutEngine is null)
        {
            return new GraphLayoutResult(
                visibleDocument.Id,
                layoutOptions,
                error: new GraphLayoutError("No graph layout engine is configured for the workspace."));
        }

        try
        {
            return layoutEngine.Layout(visibleDocument, layoutOptions);
        }
        catch (Exception exception)
        {
            return new GraphLayoutResult(
                visibleDocument.Id,
                layoutOptions,
                error: new GraphLayoutError(
                    "The graph layout engine failed while laying out the visible workspace document.",
                    exception.Message,
                    exception.GetType().FullName));
        }
    }

    private void ApplyViewStateToChildren(GraphViewState viewState)
    {
        try
        {
            isApplyingViewStateToChildren = true;
            NavigationTree.GraphSelectedNodeIds = viewState.Selection.SelectedNodeIds;
            NavigationTree.GraphSelectedLinkIds = viewState.Selection.SelectedLinkIds;
            GraphSurface.SelectedNodeIds = viewState.Selection.SelectedNodeIds;
            GraphSurface.SelectedLinkIds = viewState.Selection.SelectedLinkIds;
            GraphSurface.SelectedGroupIds = viewState.Selection.SelectedGroupIds;
            GraphSurface.ActiveViewMode = viewState.ActiveViewMode;
            GraphSurface.FocusedNodeId = viewState.ActiveNodeId;
            GraphSurface.FocusedGroupId = viewState.ActiveGroupId;
            GraphSurface.Zoom = viewState.Viewport.Zoom;
            GraphSurface.PanX = viewState.Viewport.PanX;
            GraphSurface.PanY = viewState.Viewport.PanY;
        }
        finally
        {
            isApplyingViewStateToChildren = false;
        }
    }

    private void HandleChildGraphRequest(GraphRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        GraphViewState nextViewState = CreateViewStateAfterRequest(EffectiveViewState, request);
        isUpdatingFromChild = true;
        try
        {
            SetCurrentValue(ViewStateProperty, nextViewState);
        }
        finally
        {
            isUpdatingFromChild = false;
        }

        RebuildWorkspace();
        ForwardGraphRequest(request);
    }

    private void ForwardGraphRequest(GraphRequest request)
    {
        ICommand? command = GraphRequestCommand;
        if (command is null || !command.CanExecute(request))
        {
            return;
        }

        command.Execute(request);
    }

    private static GraphViewState CreateViewStateAfterRequest(GraphViewState current, GraphRequest request)
    {
        return request.Kind switch
        {
            GraphRequestKind.SelectNode or GraphRequestKind.OpenNode => new GraphViewState(
                GraphViewMode.Focus,
                request.NodeId,
                null,
                new GraphSelectionState(selectedNodeIds: request.NodeId is null ? null : [request.NodeId]),
                current.Viewport,
                current.CollapsedContainerGroupIds,
                current.ExpandedTreeItemIds),

            GraphRequestKind.SelectLink or GraphRequestKind.OpenLink => new GraphViewState(
                GraphViewMode.Focus,
                current.ActiveNodeId,
                null,
                new GraphSelectionState(selectedLinkIds: request.LinkId is null ? null : [request.LinkId]),
                current.Viewport,
                current.CollapsedContainerGroupIds,
                current.ExpandedTreeItemIds),

            GraphRequestKind.SelectGroup or GraphRequestKind.OpenGroup => new GraphViewState(
                GraphViewMode.Group,
                null,
                request.GroupId,
                new GraphSelectionState(selectedGroupIds: request.GroupId is null ? null : [request.GroupId]),
                current.Viewport,
                current.CollapsedContainerGroupIds,
                current.ExpandedTreeItemIds),

            GraphRequestKind.ClearSelection or GraphRequestKind.ReturnToOverview => new GraphViewState(
                GraphViewMode.Overview,
                viewport: current.Viewport,
                collapsedContainerGroupIds: current.CollapsedContainerGroupIds,
                expandedTreeItemIds: current.ExpandedTreeItemIds),

            GraphRequestKind.SetGroupCollapsed => new GraphViewState(
                current.ActiveViewMode,
                current.ActiveNodeId,
                current.ActiveGroupId,
                current.Selection,
                current.Viewport,
                UpdateCollapsedGroups(current.CollapsedContainerGroupIds, request.GroupId, request.IsGroupCollapsed),
                current.ExpandedTreeItemIds),

            _ => current,
        };
    }

    private static IReadOnlyList<string> UpdateCollapsedGroups(
        IReadOnlyList<string> currentCollapsedGroupIds,
        string? groupId,
        bool? isCollapsed)
    {
        if (string.IsNullOrWhiteSpace(groupId) || isCollapsed is null)
        {
            return currentCollapsedGroupIds;
        }

        HashSet<string> next = new(currentCollapsedGroupIds, StringComparer.Ordinal);
        if (isCollapsed.Value)
        {
            next.Add(groupId);
        }
        else
        {
            next.Remove(groupId);
        }

        return next.ToArray();
    }

    private void OnWorkspaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (isFitRequested)
        {
            RequestFitAfterLayoutPass();
        }

        UpdateGraphScrollBarsAfterLayoutPass();
    }

    private void OnGraphSurfaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGraphScrollBarsAfterLayoutPass();
    }

    private void OnGraphSurfaceViewportChanged(object? sender, EventArgs e)
    {
        if (isApplyingViewStateToChildren)
        {
            return;
        }

        GraphViewState current = EffectiveViewState;
        GraphViewportState viewport = new(GraphSurface.Zoom, GraphSurface.PanX, GraphSurface.PanY);
        SetCurrentValue(ViewStateProperty, new GraphViewState(
            current.ActiveViewMode,
            current.ActiveNodeId,
            current.ActiveGroupId,
            current.Selection,
            viewport,
            current.CollapsedContainerGroupIds,
            current.ExpandedTreeItemIds));
        UpdateGraphScrollBarsAfterLayoutPass();
    }

    private void RequestFitAfterLayoutPass()
    {
        isFitRequested = true;
        Dispatcher.BeginInvoke(
            () =>
            {
                if (!isFitRequested)
                {
                    return;
                }

                isFitRequested = false;
                FitToGraph();
                UpdateGraphScrollBars();
            },
            DispatcherPriority.Loaded);
    }

    private void UpdateNavigationTreeVisibility()
    {
        bool showTree = ShowNavigationTree;
        navigationColumn.Width = showTree ? new GridLength(NavigationTreeWidth) : new GridLength(0d);
        splitterColumn.Width = showTree ? new GridLength(SplitterWidth) : new GridLength(0d);
        NavigationTree.Visibility = showTree ? Visibility.Visible : Visibility.Collapsed;
        navigationScrollViewer.Visibility = showTree ? Visibility.Visible : Visibility.Collapsed;
        splitter.Visibility = showTree ? Visibility.Visible : Visibility.Collapsed;
        navigationToggleButton.Content = showTree ? "Hide tree" : "Show tree";
    }

    private static GraphDocument CreateVisibleDocument(GraphDocument document, GraphViewState viewState)
    {
        HashSet<string> visibleNodeIds = ResolveVisibleNodeIds(document, viewState);
        if (visibleNodeIds.Count == 0 || visibleNodeIds.Count == document.Nodes.Count)
        {
            return document;
        }

        GraphNode[] visibleNodes = document.Nodes
            .Where(node => visibleNodeIds.Contains(node.Id))
            .ToArray();
        HashSet<string> existingVisibleNodeIds = visibleNodes
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        GraphLink[] visibleLinks = document.Links
            .Where(link => existingVisibleNodeIds.Contains(link.SourceNodeId) && existingVisibleNodeIds.Contains(link.TargetNodeId))
            .ToArray();
        GraphGroup[] visibleGroups = ResolveVisibleGroups(document, visibleNodes, viewState.ActiveGroupId);

        return new GraphDocument(
            $"{document.Id}:visible:{viewState.ActiveViewMode}",
            visibleNodes,
            visibleLinks,
            document.Metadata,
            visibleGroups);
    }

    private static HashSet<string> ResolveVisibleNodeIds(GraphDocument document, GraphViewState viewState)
    {
        return viewState.ActiveViewMode switch
        {
            GraphViewMode.Focus => ResolveFocusNodeIds(document, viewState),
            GraphViewMode.Tree => ResolveBranchNodeIds(document, viewState),
            GraphViewMode.Group or GraphViewMode.GroupOverview => ResolveGroupCompactNodeIds(document, viewState),
            _ => document.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal),
        };
    }

    private static HashSet<string> ResolveFocusNodeIds(GraphDocument document, GraphViewState viewState)
    {
        string? focusNodeId = viewState.ActiveNodeId ?? viewState.Selection.SelectedNodeIds.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(focusNodeId)
            || !document.Nodes.Any(node => StringComparer.Ordinal.Equals(node.Id, focusNodeId)))
        {
            return document.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        }

        HashSet<string> visibleNodeIds = new(StringComparer.Ordinal) { focusNodeId };
        foreach (GraphLink link in document.Links.Where(link => IsDirectLink(link, focusNodeId) && IsQuietNavigationLink(link)))
        {
            visibleNodeIds.Add(link.SourceNodeId);
            visibleNodeIds.Add(link.TargetNodeId);
        }

        return visibleNodeIds;
    }

    private static HashSet<string> ResolveBranchNodeIds(GraphDocument document, GraphViewState viewState)
    {
        string? focusNodeId = viewState.ActiveNodeId ?? viewState.Selection.SelectedNodeIds.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(focusNodeId)
            || !document.Nodes.Any(node => StringComparer.Ordinal.Equals(node.Id, focusNodeId)))
        {
            return document.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        }

        HashSet<string> visibleNodeIds = ResolveFocusNodeIds(document, viewState);
        foreach (string nodeId in visibleNodeIds.ToArray())
        {
            foreach (GraphLink link in document.Links.Where(link => StringComparer.Ordinal.Equals(link.SourceNodeId, nodeId) && IsQuietNavigationLink(link)))
            {
                visibleNodeIds.Add(link.SourceNodeId);
                visibleNodeIds.Add(link.TargetNodeId);
                if (visibleNodeIds.Count >= BranchMaxVisibleNodes)
                {
                    return visibleNodeIds;
                }
            }
        }

        return visibleNodeIds;
    }

    private static HashSet<string> ResolveGroupCompactNodeIds(GraphDocument document, GraphViewState viewState)
    {
        string? groupId = viewState.ActiveGroupId ?? viewState.Selection.SelectedGroupIds.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return document.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        }

        HashSet<string> visibleNodeIds = new(StringComparer.Ordinal);
        foreach (GraphNode node in document.Nodes.Where(node => node.GroupMemberships.Contains(groupId, StringComparer.Ordinal)))
        {
            visibleNodeIds.Add(node.Id);
            if (visibleNodeIds.Count >= GroupCompactMaxVisibleNodes)
            {
                return visibleNodeIds;
            }
        }

        foreach (GraphLink link in document.Links)
        {
            if (visibleNodeIds.Count >= GroupCompactMaxVisibleNodes)
            {
                break;
            }

            bool sourceVisible = visibleNodeIds.Contains(link.SourceNodeId);
            bool targetVisible = visibleNodeIds.Contains(link.TargetNodeId);
            if (sourceVisible && !targetVisible)
            {
                visibleNodeIds.Add(link.TargetNodeId);
            }
            else if (targetVisible && !sourceVisible)
            {
                visibleNodeIds.Add(link.SourceNodeId);
            }
        }

        return visibleNodeIds.Count == 0
            ? document.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal)
            : visibleNodeIds;
    }

    private static bool IsDirectLink(GraphLink link, string nodeId)
    {
        return StringComparer.Ordinal.Equals(link.SourceNodeId, nodeId)
            || StringComparer.Ordinal.Equals(link.TargetNodeId, nodeId);
    }

    private static bool IsQuietNavigationLink(GraphLink link)
    {
        return link.Kind is GraphLinkKind.Primary
            or GraphLinkKind.Secondary
            or GraphLinkKind.PopupOpen;
    }

    private static string? ResolveFirstContainerGroupId(GraphDocument? document, string? nodeId)
    {
        if (document is null || string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        GraphNode? node = document.Nodes.FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Id, nodeId));
        return node?.GroupMemberships.FirstOrDefault(groupId => document.Groups.Any(group => group.Kind == GraphGroupKind.Container && StringComparer.Ordinal.Equals(group.Id, groupId)));
    }

    private static GraphGroup[] ResolveVisibleGroups(GraphDocument document, IReadOnlyList<GraphNode> visibleNodes, string? activeGroupId)
    {
        HashSet<string> visibleGroupIds = visibleNodes
            .SelectMany(node => node.GroupMemberships)
            .ToHashSet(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(activeGroupId))
        {
            visibleGroupIds.Add(activeGroupId);
        }

        Dictionary<string, GraphGroup> groupsById = document.Groups.ToDictionary(group => group.Id, StringComparer.Ordinal);
        foreach (string groupId in visibleGroupIds.ToArray())
        {
            string? parentGroupId = groupsById.TryGetValue(groupId, out GraphGroup? group) ? group.ParentGroupId : null;
            while (!string.IsNullOrWhiteSpace(parentGroupId) && visibleGroupIds.Add(parentGroupId))
            {
                parentGroupId = groupsById.TryGetValue(parentGroupId, out GraphGroup? parentGroup) ? parentGroup.ParentGroupId : null;
            }
        }

        return document.Groups
            .Where(group => visibleGroupIds.Contains(group.Id))
            .ToArray();
    }

    private void OnLayoutDirectionComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingToolbarSelections || layoutDirectionComboBox.SelectedItem is not GraphLayoutDirection direction)
        {
            return;
        }

        LayoutDirection = direction;
    }

    private void OnEdgeRoutingStyleComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isUpdatingToolbarSelections || edgeRoutingStyleComboBox.SelectedItem is not GraphEdgeRoutingStyle routingStyle)
        {
            return;
        }

        EdgeRoutingStyle = routingStyle;
    }

    private void UpdateToolbarSelections()
    {
        if (layoutDirectionComboBox is null || edgeRoutingStyleComboBox is null || densityButton is null)
        {
            return;
        }

        try
        {
            isUpdatingToolbarSelections = true;
            layoutDirectionComboBox.SelectedItem = LayoutDirection;
            edgeRoutingStyleComboBox.SelectedItem = EdgeRoutingStyle;
            densityButton.Content = VisualDensity < 1d ? "Comfort" : "Compact";
        }
        finally
        {
            isUpdatingToolbarSelections = false;
        }
    }

    private void OnHorizontalScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isUpdatingGraphScrollBars)
        {
            return;
        }

        GraphRect bounds = GraphSurface.LayoutBounds;
        if (bounds.Width <= 0d)
        {
            return;
        }

        GraphSurface.PanX = -e.NewValue - (bounds.X * GraphSurface.Zoom);
        UpdateGraphScrollBarsAfterLayoutPass();
    }

    private void OnVerticalScrollBarValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isUpdatingGraphScrollBars)
        {
            return;
        }

        GraphRect bounds = GraphSurface.LayoutBounds;
        if (bounds.Height <= 0d)
        {
            return;
        }

        GraphSurface.PanY = -e.NewValue - (bounds.Y * GraphSurface.Zoom);
        UpdateGraphScrollBarsAfterLayoutPass();
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
            GraphRect bounds = GraphSurface.LayoutBounds;
            if (VisibleLayout is not { Succeeded: true }
                || viewportSize is null
                || bounds.Width <= 0d
                || bounds.Height <= 0d)
            {
                SetGraphScrollBarsVisible(horizontalVisible: false, verticalVisible: false);
                return;
            }

            double zoom = GraphSurface.Zoom;
            double scaledWidth = bounds.Width * zoom;
            double scaledHeight = bounds.Height * zoom;
            bool horizontalVisible = scaledWidth > viewportSize.Value.Width + 1d;
            bool verticalVisible = scaledHeight > viewportSize.Value.Height + 1d;

            SetGraphScrollBarsVisible(horizontalVisible, verticalVisible);

            if (horizontalVisible)
            {
                double max = Math.Max(0d, scaledWidth - viewportSize.Value.Width);
                double value = Math.Clamp(-(bounds.X * zoom + GraphSurface.PanX), 0d, max);
                horizontalScrollBar.Minimum = 0d;
                horizontalScrollBar.Maximum = max;
                horizontalScrollBar.ViewportSize = viewportSize.Value.Width;
                horizontalScrollBar.SmallChange = Math.Max(12d, viewportSize.Value.Width * 0.08d);
                horizontalScrollBar.LargeChange = Math.Max(24d, viewportSize.Value.Width * 0.72d);
                horizontalScrollBar.Value = value;
            }

            if (verticalVisible)
            {
                double max = Math.Max(0d, scaledHeight - viewportSize.Value.Height);
                double value = Math.Clamp(-(bounds.Y * zoom + GraphSurface.PanY), 0d, max);
                verticalScrollBar.Minimum = 0d;
                verticalScrollBar.Maximum = max;
                verticalScrollBar.ViewportSize = viewportSize.Value.Height;
                verticalScrollBar.SmallChange = Math.Max(12d, viewportSize.Value.Height * 0.08d);
                verticalScrollBar.LargeChange = Math.Max(24d, viewportSize.Value.Height * 0.72d);
                verticalScrollBar.Value = value;
            }
        }
        finally
        {
            isUpdatingGraphScrollBars = false;
        }
    }

    private void SetGraphScrollBarsVisible(bool horizontalVisible, bool verticalVisible)
    {
        horizontalScrollBar.Visibility = horizontalVisible ? Visibility.Visible : Visibility.Collapsed;
        verticalScrollBar.Visibility = verticalVisible ? Visibility.Visible : Visibility.Collapsed;
        scrollCorner.Visibility = horizontalVisible && verticalVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private Size? GetGraphViewportSizeOrNull()
    {
        double viewportWidth = GraphSurface.ActualWidth;
        double viewportHeight = GraphSurface.ActualHeight;
        return viewportWidth <= 1d || viewportHeight <= 1d
            ? null
            : new Size(viewportWidth, viewportHeight);
    }

    private sealed class GraphWorkspaceRequestCommand : ICommand
    {
        private readonly GraphWorkspace owner;

        public GraphWorkspaceRequestCommand(GraphWorkspace owner)
        {
            this.owner = owner;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter)
        {
            return parameter is GraphRequest request && owner.HostCapabilities.Supports(request);
        }

        public void Execute(object? parameter)
        {
            if (parameter is GraphRequest request)
            {
                owner.HandleChildGraphRequest(request);
            }
        }
    }
}
