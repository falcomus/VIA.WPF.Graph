using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
public sealed partial class GraphWorkspace : Grid
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
    private const double DefaultFitPadding = 72d;

    private GridLength shownNavigationColumnWidth;
    private GridLength shownSplitterColumnWidth;
    private readonly GraphWorkspaceRequestCommand navigationTreeRequestCommand;
    private readonly GraphWorkspaceRequestCommand graphSurfaceRequestCommand;

    private bool isUpdatingFromChild;
    private bool isApplyingViewStateToChildren;
    private bool isFitRequested;
    private bool isUpdatingGraphScrollBars;
    private bool isUpdatingToolbarSelections;

    public GraphWorkspace()
    {
        InitializeComponent();

        shownNavigationColumnWidth = navigationColumn.Width;
        shownSplitterColumnWidth = splitterColumn.Width;

        navigationTreeRequestCommand = new GraphWorkspaceRequestCommand(this, GraphWorkspaceRequestSource.NavigationTree);
        graphSurfaceRequestCommand = new GraphWorkspaceRequestCommand(this, GraphWorkspaceRequestSource.GraphSurface);

        NavigationTree.GraphRequestCommand = navigationTreeRequestCommand;
        GraphSurface.GraphRequestCommand = graphSurfaceRequestCommand;

        layoutDirectionComboBox.ItemsSource = Enum.GetValues(typeof(GraphLayoutDirection));
        edgeRoutingStyleComboBox.ItemsSource = Enum.GetValues(typeof(GraphEdgeRoutingStyle));

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

    private void OnFocusButtonClick(object sender, RoutedEventArgs e)
    {
        SetActiveViewMode(GraphViewMode.Focus);
    }

    private void OnBranchButtonClick(object sender, RoutedEventArgs e)
    {
        SetActiveViewMode(GraphViewMode.Tree);
    }

    private void OnGroupButtonClick(object sender, RoutedEventArgs e)
    {
        SetActiveViewMode(GraphViewMode.Group);
    }

    private void OnOverviewButtonClick(object sender, RoutedEventArgs e)
    {
        SetActiveViewMode(GraphViewMode.Overview);
    }

    private void OnFitButtonClick(object sender, RoutedEventArgs e)
    {
        FitToGraph();
    }

    private void OnActualSizeButtonClick(object sender, RoutedEventArgs e)
    {
        SetActualSize();
    }

    private void OnCenterButtonClick(object sender, RoutedEventArgs e)
    {
        _ = CenterSelection();
    }

    private void OnNavigationToggleButtonClick(object sender, RoutedEventArgs e)
    {
        ShowNavigationTree = !ShowNavigationTree;
    }

    private void OnDensityButtonClick(object sender, RoutedEventArgs e)
    {
        ToggleVisualDensity();
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

    public GraphNavigationPathTree NavigationTree => navigationTreeControl;

    public SkiaGraphSurface GraphSurface => graphSurfaceControl;

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
            ScrollSelectedTreeNodeIntoViewAfterLayoutPass();
        }
        finally
        {
            isApplyingViewStateToChildren = false;
        }
    }

    private void ScrollSelectedTreeNodeIntoViewAfterLayoutPass()
    {
        if (!ShowNavigationTree)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                if (!ShowNavigationTree)
                {
                    return;
                }

                NavigationTree.UpdateLayout();
                _ = NavigationTree.BringSelectedTreeNodeIntoView();
            },
            DispatcherPriority.Loaded);
    }

    private void HandleChildGraphRequest(GraphRequest request, GraphWorkspaceRequestSource requestSource)
    {
        ArgumentNullException.ThrowIfNull(request);

        GraphViewState nextViewState = CreateViewStateAfterRequest(EffectiveViewState, request, requestSource);
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

    private static GraphViewState CreateViewStateAfterRequest(
        GraphViewState current,
        GraphRequest request,
        GraphWorkspaceRequestSource requestSource)
    {
        GraphViewMode nodeSelectionMode = requestSource == GraphWorkspaceRequestSource.NavigationTree
            ? GraphViewMode.Tree
            : GraphViewMode.Focus;

        return request.Kind switch
        {
            GraphRequestKind.SelectNode or GraphRequestKind.OpenNode => new GraphViewState(
                nodeSelectionMode,
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
        navigationColumn.Width = showTree ? shownNavigationColumnWidth : new GridLength(0d);
        splitterColumn.Width = showTree ? shownSplitterColumnWidth : new GridLength(0d);
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

        Dictionary<string, GraphNode> nodesById = document.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        GraphNode[] groupNodes = document.Nodes
            .Where(node => node.GroupMemberships.Contains(groupId, StringComparer.Ordinal))
            .ToArray();
        if (groupNodes.Length == 0)
        {
            return document.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        }

        HashSet<string> groupNodeIds = groupNodes
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> visibleNodeIds = new(StringComparer.Ordinal);

        AddSelectedGroupNodes(visibleNodeIds, nodesById, groupNodeIds, viewState);
        AddGroupEntryNodes(visibleNodeIds, groupNodes, document.Links, groupNodeIds);
        AddDirectContextNodes(visibleNodeIds, nodesById, document.Links);
        AddGroupNodesUpToLimit(visibleNodeIds, groupNodes);
        AddDirectContextNodes(visibleNodeIds, nodesById, document.Links);

        return visibleNodeIds.Count == 0
            ? document.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal)
            : visibleNodeIds;
    }

    private static void AddSelectedGroupNodes(
        HashSet<string> visibleNodeIds,
        IReadOnlyDictionary<string, GraphNode> nodesById,
        IReadOnlySet<string> groupNodeIds,
        GraphViewState viewState)
    {
        IEnumerable<string?> selectedNodeIds =
        [
            viewState.ActiveNodeId,
            ..viewState.Selection.SelectedNodeIds
        ];

        foreach (string? nodeId in selectedNodeIds)
        {
            if (visibleNodeIds.Count >= GroupCompactMaxVisibleNodes)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(nodeId)
                && groupNodeIds.Contains(nodeId)
                && nodesById.ContainsKey(nodeId))
            {
                visibleNodeIds.Add(nodeId);
            }
        }
    }

    private static void AddGroupEntryNodes(
        HashSet<string> visibleNodeIds,
        IReadOnlyList<GraphNode> groupNodes,
        IReadOnlyList<GraphLink> links,
        IReadOnlySet<string> groupNodeIds)
    {
        foreach (GraphNode node in groupNodes)
        {
            if (visibleNodeIds.Count >= GroupCompactMaxVisibleNodes)
            {
                return;
            }

            bool hasIncomingFromSameGroup = links.Any(link =>
                StringComparer.Ordinal.Equals(link.TargetNodeId, node.Id)
                && groupNodeIds.Contains(link.SourceNodeId)
                && IsQuietNavigationLink(link));

            if (!hasIncomingFromSameGroup)
            {
                visibleNodeIds.Add(node.Id);
            }
        }
    }

    private static void AddGroupNodesUpToLimit(HashSet<string> visibleNodeIds, IReadOnlyList<GraphNode> groupNodes)
    {
        foreach (GraphNode node in groupNodes)
        {
            if (visibleNodeIds.Count >= GroupCompactMaxVisibleNodes)
            {
                return;
            }

            visibleNodeIds.Add(node.Id);
        }
    }

    private static void AddDirectContextNodes(
        HashSet<string> visibleNodeIds,
        IReadOnlyDictionary<string, GraphNode> nodesById,
        IReadOnlyList<GraphLink> links)
    {
        if (visibleNodeIds.Count == 0)
        {
            return;
        }

        string[] currentNodeIds = visibleNodeIds.ToArray();
        foreach (string nodeId in currentNodeIds)
        {
            foreach (GraphLink link in links.Where(link => IsDirectLink(link, nodeId) && IsQuietNavigationLink(link)))
            {
                if (visibleNodeIds.Count >= GroupCompactMaxVisibleNodes)
                {
                    return;
                }

                if (nodesById.ContainsKey(link.SourceNodeId))
                {
                    visibleNodeIds.Add(link.SourceNodeId);
                }

                if (visibleNodeIds.Count >= GroupCompactMaxVisibleNodes)
                {
                    return;
                }

                if (nodesById.ContainsKey(link.TargetNodeId))
                {
                    visibleNodeIds.Add(link.TargetNodeId);
                }
            }
        }
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

    private enum GraphWorkspaceRequestSource
    {
        NavigationTree,
        GraphSurface,
    }

    private sealed class GraphWorkspaceRequestCommand : ICommand
    {
        private readonly GraphWorkspace owner;
        private readonly GraphWorkspaceRequestSource requestSource;

        public GraphWorkspaceRequestCommand(GraphWorkspace owner, GraphWorkspaceRequestSource requestSource)
        {
            this.owner = owner;
            this.requestSource = requestSource;
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
                owner.HandleChildGraphRequest(request, requestSource);
            }
        }
    }
}
