using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Core.Requests;

namespace VIA.WPF.Graph.Wpf.Controls;

/// <summary>
/// Minimal WPF graph canvas that renders a neutral layout result with separate group, edge and node layers.
/// </summary>
public sealed class GraphCanvas : FrameworkElement
{
    public static readonly DependencyProperty LayoutResultProperty = DependencyProperty.Register(
        nameof(LayoutResult),
        typeof(GraphLayoutResult),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure,
            OnLayoutResultChanged));

    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document),
        typeof(GraphDocument),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsRender,
            OnDocumentChanged));

    private static readonly DependencyPropertyKey LayoutBoundsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(LayoutBounds),
        typeof(GraphRect),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(new GraphRect(0d, 0d, 0d, 0d)));

    public static readonly DependencyProperty LayoutBoundsProperty = LayoutBoundsPropertyKey.DependencyProperty;

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom),
        typeof(double),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            1d,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsMeasure,
            OnViewportPropertyChanged,
            CoerceZoom),
        IsFinitePositiveDouble);

    public static readonly DependencyProperty MinZoomProperty = DependencyProperty.Register(
        nameof(MinZoom),
        typeof(double),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(0.05d, OnZoomLimitChanged),
        IsFinitePositiveDouble);

    public static readonly DependencyProperty MaxZoomProperty = DependencyProperty.Register(
        nameof(MaxZoom),
        typeof(double),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(8d, OnZoomLimitChanged),
        IsFinitePositiveDouble);

    public static readonly DependencyProperty PanXProperty = DependencyProperty.Register(
        nameof(PanX),
        typeof(double),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            0d,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnViewportPropertyChanged),
        IsFiniteDouble);

    public static readonly DependencyProperty PanYProperty = DependencyProperty.Register(
        nameof(PanY),
        typeof(double),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            0d,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnViewportPropertyChanged),
        IsFiniteDouble);

    public static readonly DependencyProperty SelectedNodeIdsProperty = DependencyProperty.Register(
        nameof(SelectedNodeIds),
        typeof(IReadOnlyList<string>),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            Array.Empty<string>(),
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnSelectionPropertyChanged));

    public static readonly DependencyProperty SelectedLinkIdsProperty = DependencyProperty.Register(
        nameof(SelectedLinkIds),
        typeof(IReadOnlyList<string>),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            Array.Empty<string>(),
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnSelectionPropertyChanged));

    public static readonly DependencyProperty SelectedGroupIdsProperty = DependencyProperty.Register(
        nameof(SelectedGroupIds),
        typeof(IReadOnlyList<string>),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            Array.Empty<string>(),
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnSelectionPropertyChanged));

    public static readonly DependencyProperty CollapsedGroupIdsProperty = DependencyProperty.Register(
        nameof(CollapsedGroupIds),
        typeof(IReadOnlyList<string>),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            Array.Empty<string>(),
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnCollapsedGroupIdsChanged));

    public static readonly DependencyProperty IsMarkerGroupFilterEnabledProperty = DependencyProperty.Register(
        nameof(IsMarkerGroupFilterEnabled),
        typeof(bool),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnMarkerGroupFilterChanged));

    public static readonly DependencyProperty ActiveViewModeProperty = DependencyProperty.Register(
        nameof(ActiveViewMode),
        typeof(GraphViewMode),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            GraphViewMode.Overview,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnVisualModePropertyChanged));

    public static readonly DependencyProperty VisualDensityProperty = DependencyProperty.Register(
        nameof(VisualDensity),
        typeof(double),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            1d,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnVisualModePropertyChanged,
            CoerceVisualDensity),
        IsFinitePositiveDouble);

    public static readonly DependencyProperty FocusedNodeIdProperty = DependencyProperty.Register(
        nameof(FocusedNodeId),
        typeof(string),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnFocusPropertyChanged));

    public static readonly DependencyProperty FocusedLinkIdProperty = DependencyProperty.Register(
        nameof(FocusedLinkId),
        typeof(string),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnFocusPropertyChanged));

    public static readonly DependencyProperty FocusedGroupIdProperty = DependencyProperty.Register(
        nameof(FocusedGroupId),
        typeof(string),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnFocusPropertyChanged));

    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
        nameof(SearchText),
        typeof(string),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnFocusPropertyChanged));


    public static readonly DependencyProperty GraphRequestCommandProperty = DependencyProperty.Register(
        nameof(GraphRequestCommand),
        typeof(ICommand),
        typeof(GraphCanvas),
        new FrameworkPropertyMetadata(null));

    private const double DefaultTextSize = 12d;
    private const double NodeCornerRadius = 5d;
    private const double ArrowLength = 10d;
    private const double ArrowHalfWidth = 4.5d;
    private const double ZoomWheelFactor = 1.1d;
    private const double DefaultFitPadding = 32d;
    private const double EdgeHitTolerance = 6d;
    private const double CollapseToggleSize = 20d;
    private const double FocusDimOpacity = 0.22d;
    private const double MinVisualDensity = 0.55d;
    private const double MaxVisualDensity = 1.35d;
    private const double LowDensityLabelZoomThreshold = 0.72d;
    private const double AreaOverviewBadgeHeight = 18d;

    private static readonly Brush BackgroundBrush = CreateFrozenBrush(Color.FromRgb(248, 248, 248));
    private static readonly Brush GroupFillBrush = CreateFrozenBrush(Color.FromArgb(20, 96, 125, 139));
    private static readonly Brush SelectedGroupFillBrush = CreateFrozenBrush(Color.FromArgb(32, 30, 115, 190));
    private static readonly Brush MarkerGroupFillBrush = CreateFrozenBrush(Color.FromArgb(28, 255, 193, 7));
    private static readonly Brush CollapsedGroupFillBrush = CreateFrozenBrush(Color.FromRgb(236, 242, 246));
    private static readonly Brush CollapsedBadgeFillBrush = CreateFrozenBrush(Color.FromRgb(30, 115, 190));
    private static readonly Brush CollapsedBadgeTextBrush = CreateFrozenBrush(Color.FromRgb(255, 255, 255));
    private static readonly Brush AreaOverviewBundleFillBrush = CreateFrozenBrush(Color.FromRgb(76, 104, 122));
    private static readonly Brush AreaOverviewBundleTextBrush = CreateFrozenBrush(Color.FromRgb(255, 255, 255));
    private static readonly Brush NodeFillBrush = Brushes.White;
    private static readonly Brush TextBrush = Brushes.Black;
    private static readonly Brush MutedTextBrush = CreateFrozenBrush(Color.FromRgb(96, 96, 96));
    private static readonly Brush SelectionBrush = CreateFrozenBrush(Color.FromRgb(30, 115, 190));
    private static readonly Pen GroupPen = CreateFrozenPen(Color.FromRgb(96, 125, 139), 1.25d, DashStyles.Dash);
    private static readonly Pen SelectedGroupPen = CreateFrozenPen(Color.FromRgb(30, 115, 190), 2.25d, DashStyles.Dash);
    private static readonly Pen MarkerGroupPen = CreateFrozenPen(Color.FromRgb(255, 152, 0), 2d, DashStyles.Dash);
    private static readonly Pen CollapsedGroupPen = CreateFrozenPen(Color.FromRgb(76, 104, 122), 2d, DashStyles.Solid);
    private static readonly Pen AreaOverviewGroupPen = CreateFrozenPen(Color.FromRgb(55, 82, 100), 2d, DashStyles.Solid);
    private static readonly Pen AreaOverviewEdgePen = CreateFrozenPen(Color.FromRgb(76, 104, 122), 2d, DashStyles.Solid);
    private static readonly Pen EdgePen = CreateFrozenPen(Color.FromRgb(84, 96, 108), 1.5d, DashStyles.Solid);
    private static readonly Pen SelectedEdgePen = CreateFrozenPen(Color.FromRgb(30, 115, 190), 2.5d, DashStyles.Solid);
    private static readonly Pen FallbackEdgePen = CreateFrozenPen(Color.FromRgb(120, 120, 120), 1.5d, DashStyles.Dash);
    private static readonly Pen NodePen = CreateFrozenPen(Color.FromRgb(84, 96, 108), 1.25d, DashStyles.Solid);
    private static readonly Pen SelectedNodePen = CreateFrozenPen(Color.FromRgb(30, 115, 190), 2.5d, DashStyles.Solid);
    private static readonly Typeface TextTypeface = new("Segoe UI");

    private readonly GraphCanvasLayer groupLayer = new();
    private readonly GraphCanvasLayer edgeLayer = new();
    private readonly GraphCanvasLayer nodeLayer = new();
    private bool isPanning;
    private Point lastPanPoint;
    private Cursor? previousCursor;

    public GraphCanvas()
    {
        ClipToBounds = true;
        Focusable = true;

        AddVisualChild(groupLayer.Visual);
        AddVisualChild(edgeLayer.Visual);
        AddVisualChild(nodeLayer.Visual);
        UpdateLayerTransform();
    }

    public GraphLayoutResult? LayoutResult
    {
        get => (GraphLayoutResult?)GetValue(LayoutResultProperty);
        set => SetValue(LayoutResultProperty, value);
    }

    public GraphDocument? Document
    {
        get => (GraphDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public GraphRect LayoutBounds => (GraphRect)GetValue(LayoutBoundsProperty);

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double MinZoom
    {
        get => (double)GetValue(MinZoomProperty);
        set => SetValue(MinZoomProperty, value);
    }

    public double MaxZoom
    {
        get => (double)GetValue(MaxZoomProperty);
        set => SetValue(MaxZoomProperty, value);
    }

    public double PanX
    {
        get => (double)GetValue(PanXProperty);
        set => SetValue(PanXProperty, value);
    }

    public double PanY
    {
        get => (double)GetValue(PanYProperty);
        set => SetValue(PanYProperty, value);
    }

    public IReadOnlyList<string> SelectedNodeIds
    {
        get => (IReadOnlyList<string>?)GetValue(SelectedNodeIdsProperty) ?? Array.Empty<string>();
        set => SetValue(SelectedNodeIdsProperty, CopySelection(value));
    }

    public IReadOnlyList<string> SelectedLinkIds
    {
        get => (IReadOnlyList<string>?)GetValue(SelectedLinkIdsProperty) ?? Array.Empty<string>();
        set => SetValue(SelectedLinkIdsProperty, CopySelection(value));
    }

    public IReadOnlyList<string> SelectedGroupIds
    {
        get => (IReadOnlyList<string>?)GetValue(SelectedGroupIdsProperty) ?? Array.Empty<string>();
        set => SetValue(SelectedGroupIdsProperty, CopySelection(value));
    }

    public IReadOnlyList<string> CollapsedGroupIds
    {
        get => (IReadOnlyList<string>?)GetValue(CollapsedGroupIdsProperty) ?? Array.Empty<string>();
        set => SetValue(CollapsedGroupIdsProperty, CopySelection(value));
    }

    public bool IsMarkerGroupFilterEnabled
    {
        get => (bool)GetValue(IsMarkerGroupFilterEnabledProperty);
        set => SetValue(IsMarkerGroupFilterEnabledProperty, value);
    }

    public GraphViewMode ActiveViewMode
    {
        get => (GraphViewMode)GetValue(ActiveViewModeProperty);
        set => SetValue(ActiveViewModeProperty, value);
    }

    public double VisualDensity
    {
        get => (double)GetValue(VisualDensityProperty);
        set => SetValue(VisualDensityProperty, value);
    }

    public bool IsAreaOverviewActive => IsAreaOverviewMode(ActiveViewMode);

    public bool IsFocusModeActive => ActiveViewMode == GraphViewMode.Focus
        || FocusedNodeId is not null
        || FocusedLinkId is not null
        || FocusedGroupId is not null;

    public string? FocusedNodeId
    {
        get => (string?)GetValue(FocusedNodeIdProperty);
        set => SetValue(FocusedNodeIdProperty, NormalizeOptionalText(value));
    }

    public string? FocusedLinkId
    {
        get => (string?)GetValue(FocusedLinkIdProperty);
        set => SetValue(FocusedLinkIdProperty, NormalizeOptionalText(value));
    }

    public string? FocusedGroupId
    {
        get => (string?)GetValue(FocusedGroupIdProperty);
        set => SetValue(FocusedGroupIdProperty, NormalizeOptionalText(value));
    }

    public string SearchText
    {
        get => (string?)GetValue(SearchTextProperty) ?? string.Empty;
        set => SetValue(SearchTextProperty, value ?? string.Empty);
    }


    public ICommand? GraphRequestCommand
    {
        get => (ICommand?)GetValue(GraphRequestCommandProperty);
        set => SetValue(GraphRequestCommandProperty, value);
    }

    protected override int VisualChildrenCount => 3;

    protected override Visual GetVisualChild(int index)
    {
        return index switch
        {
            0 => groupLayer.Visual,
            1 => edgeLayer.Visual,
            2 => nodeLayer.Visual,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Layer index is outside the graph canvas visual range.")
        };
    }

    public void FitToGraph()
    {
        FitToGraph(RenderSize, DefaultFitPadding);
    }

    public void FitToGraph(Size viewportSize, double padding = DefaultFitPadding)
    {
        GraphRect bounds = LayoutBounds;
        if (bounds.Width <= 0d || bounds.Height <= 0d || viewportSize.Width <= 0d || viewportSize.Height <= 0d)
        {
            SetCurrentValue(ZoomProperty, 1d);
            SetCurrentValue(PanXProperty, 0d);
            SetCurrentValue(PanYProperty, 0d);
            return;
        }

        double safePadding = Math.Max(0d, padding);
        double availableWidth = Math.Max(1d, viewportSize.Width - (safePadding * 2d));
        double availableHeight = Math.Max(1d, viewportSize.Height - (safePadding * 2d));
        double fittedZoom = CoerceZoomValue(Math.Min(availableWidth / bounds.Width, availableHeight / bounds.Height));

        double scaledWidth = bounds.Width * fittedZoom;
        double scaledHeight = bounds.Height * fittedZoom;
        double panX = ((viewportSize.Width - scaledWidth) / 2d) - (bounds.X * fittedZoom);
        double panY = ((viewportSize.Height - scaledHeight) / 2d) - (bounds.Y * fittedZoom);

        SetCurrentValue(ZoomProperty, fittedZoom);
        SetCurrentValue(PanXProperty, panX);
        SetCurrentValue(PanYProperty, panY);
    }

    public bool FocusNode(string nodeId)
    {
        string? normalizedNodeId = NormalizeOptionalText(nodeId);
        if (normalizedNodeId is null || !TryGetNodeBounds(normalizedNodeId, out GraphRect bounds))
        {
            return false;
        }

        SetCurrentValue(ActiveViewModeProperty, GraphViewMode.Focus);
        SetCurrentValue(FocusedNodeIdProperty, normalizedNodeId);
        SetCurrentValue(FocusedLinkIdProperty, null);
        SetCurrentValue(FocusedGroupIdProperty, null);
        CenterOnBounds(bounds);
        return true;
    }

    public bool FocusLink(string linkId)
    {
        string? normalizedLinkId = NormalizeOptionalText(linkId);
        if (normalizedLinkId is null || !TryGetLinkBounds(normalizedLinkId, out GraphRect bounds))
        {
            return false;
        }

        SetCurrentValue(ActiveViewModeProperty, GraphViewMode.Focus);
        SetCurrentValue(FocusedNodeIdProperty, null);
        SetCurrentValue(FocusedLinkIdProperty, normalizedLinkId);
        SetCurrentValue(FocusedGroupIdProperty, null);
        CenterOnBounds(bounds);
        return true;
    }

    public bool FocusGroup(string groupId)
    {
        string? normalizedGroupId = NormalizeOptionalText(groupId);
        if (normalizedGroupId is null || !TryGetGroupFocusBounds(normalizedGroupId, out GraphRect bounds))
        {
            return false;
        }

        SetCurrentValue(ActiveViewModeProperty, GraphViewMode.Focus);
        SetCurrentValue(FocusedNodeIdProperty, null);
        SetCurrentValue(FocusedLinkIdProperty, null);
        SetCurrentValue(FocusedGroupIdProperty, normalizedGroupId);
        FitToBounds(bounds, RenderSize, DefaultFitPadding);
        return true;
    }

    public bool DrillDownToGroup(string groupId)
    {
        return FocusGroup(groupId);
    }

    public void ShowAreaOverview()
    {
        SetCurrentValue(ActiveViewModeProperty, GraphViewMode.GroupOverview);
        SetCurrentValue(FocusedNodeIdProperty, null);
        SetCurrentValue(FocusedLinkIdProperty, null);
        SetCurrentValue(FocusedGroupIdProperty, null);
        SetCurrentValue(SearchTextProperty, string.Empty);

        if (RenderSize.Width > 0d && RenderSize.Height > 0d)
        {
            FitToGraph();
        }
    }

    public bool SetGroupCollapsed(string groupId, bool isCollapsed)
    {
        string? normalizedGroupId = NormalizeOptionalText(groupId);
        if (normalizedGroupId is null || !TryGetContainerGroupBounds(normalizedGroupId, out _))
        {
            return false;
        }

        SetCurrentValue(
            CollapsedGroupIdsProperty,
            UpdateCollapsedGroups(CollapsedGroupIds, normalizedGroupId, isCollapsed));
        ExecuteGraphRequest(GraphRequest.SetGroupCollapsed(normalizedGroupId, isCollapsed));
        return true;
    }

    public bool ToggleGroupCollapsed(string groupId)
    {
        string? normalizedGroupId = NormalizeOptionalText(groupId);
        if (normalizedGroupId is null)
        {
            return false;
        }

        bool isCollapsed = CollapsedGroupIds.Contains(normalizedGroupId, StringComparer.Ordinal);
        return SetGroupCollapsed(normalizedGroupId, !isCollapsed);
    }

    public bool SelectMarkerGroup(string groupId, bool isMultiSelection = false)
    {
        string? normalizedGroupId = NormalizeOptionalText(groupId);
        if (normalizedGroupId is null || !IsMarkerGroup(normalizedGroupId))
        {
            return false;
        }

        SetCurrentValue(SelectedGroupIdsProperty, UpdateSelection(SelectedGroupIds, normalizedGroupId, isMultiSelection));
        if (!isMultiSelection)
        {
            SetCurrentValue(SelectedNodeIdsProperty, Array.Empty<string>());
            SetCurrentValue(SelectedLinkIdsProperty, Array.Empty<string>());
        }

        ExecuteGraphRequest(GraphRequest.SelectGroup(normalizedGroupId, isMultiSelection));
        return true;
    }

    public bool FocusMarkerGroup(string groupId)
    {
        string? normalizedGroupId = NormalizeOptionalText(groupId);
        return normalizedGroupId is not null
            && IsMarkerGroup(normalizedGroupId)
            && FocusGroup(normalizedGroupId);
    }

    public void ClearMarkerGroupFilter()
    {
        SetCurrentValue(IsMarkerGroupFilterEnabledProperty, false);
        SetCurrentValue(SelectedGroupIdsProperty, SelectedGroupIds.Where(groupId => !IsMarkerGroup(groupId)).ToArray());
    }

    public bool FocusFirstMatch(string searchText)
    {
        string normalizedSearchText = searchText ?? string.Empty;
        SetCurrentValue(SearchTextProperty, normalizedSearchText);

        if (string.IsNullOrWhiteSpace(normalizedSearchText) || LayoutResult is not { Succeeded: true } layoutResult)
        {
            return false;
        }

        GraphLayoutNode? node = layoutResult.Nodes.FirstOrDefault(item => ContainsText(item.NodeId, normalizedSearchText));
        if (node is not null)
        {
            return FocusNode(node.NodeId);
        }

        GraphLayoutGroup? group = layoutResult.Groups.FirstOrDefault(item => ContainsText(item.GroupId, normalizedSearchText));
        if (group is not null)
        {
            return FocusGroup(group.GroupId);
        }

        GraphGroup? markerGroup = GetMarkerGroups()
            .FirstOrDefault(item => ContainsText(item.Id, normalizedSearchText) || ContainsText(item.Title, normalizedSearchText));
        if (markerGroup is not null)
        {
            return FocusMarkerGroup(markerGroup.Id);
        }

        GraphLayoutEdge? edge = layoutResult.Edges.FirstOrDefault(item => ContainsText(item.LinkId, normalizedSearchText));
        return edge is not null && FocusLink(edge.LinkId);
    }

    public void ReturnToOverview()
    {
        SetCurrentValue(ActiveViewModeProperty, GraphViewMode.Overview);
        SetCurrentValue(FocusedNodeIdProperty, null);
        SetCurrentValue(FocusedLinkIdProperty, null);
        SetCurrentValue(FocusedGroupIdProperty, null);
        SetCurrentValue(SearchTextProperty, string.Empty);

        if (RenderSize.Width > 0d && RenderSize.Height > 0d)
        {
            FitToGraph();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        GraphRect bounds = LayoutBounds;
        return new Size(bounds.Width * Zoom, bounds.Height * Zoom);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        drawingContext.DrawRectangle(BackgroundBrush, null, new Rect(new Point(0d, 0d), RenderSize));
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (LayoutResult is null || !LayoutResult.Succeeded)
        {
            return;
        }

        double oldZoom = Zoom;
        double requestedZoom = e.Delta > 0
            ? oldZoom * ZoomWheelFactor
            : oldZoom / ZoomWheelFactor;
        double newZoom = CoerceZoomValue(requestedZoom);
        if (Math.Abs(newZoom - oldZoom) < 0.000001d)
        {
            return;
        }

        Point mousePosition = e.GetPosition(this);
        double contentX = (mousePosition.X - PanX) / oldZoom;
        double contentY = (mousePosition.Y - PanY) / oldZoom;

        SetCurrentValue(ZoomProperty, newZoom);
        SetCurrentValue(PanXProperty, mousePosition.X - (contentX * newZoom));
        SetCurrentValue(PanYProperty, mousePosition.Y - (contentY * newZoom));
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Right)
        {
            Focus();
            CaptureMouse();
            isPanning = true;
            lastPanPoint = e.GetPosition(this);
            previousCursor = Cursor;
            Cursor = Cursors.SizeAll;
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        Focus();
        Point mousePosition = e.GetPosition(this);
        GraphCanvasHit? collapseToggleHit = e.ClickCount == 1 ? HitTestGroupCollapseToggle(mousePosition) : null;
        if (collapseToggleHit is not null)
        {
            _ = ToggleGroupCollapsed(collapseToggleHit.Id);
            e.Handled = true;
            return;
        }

        GraphCanvasHit? hit = HitTestGraph(mousePosition);
        if (e.ClickCount > 1)
        {
            ApplyHitFocus(hit);
            e.Handled = true;
            return;
        }

        bool isMultiSelection = Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        ApplyHitSelection(hit, isMultiSelection);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!isPanning)
        {
            return;
        }

        Point currentPoint = e.GetPosition(this);
        Vector delta = currentPoint - lastPanPoint;
        lastPanPoint = currentPoint;

        SetCurrentValue(PanXProperty, PanX + delta.X);
        SetCurrentValue(PanYProperty, PanY + delta.Y);
        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (!isPanning || (e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Right))
        {
            return;
        }

        EndPan();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        EndPan();
    }


    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key != Key.Escape)
        {
            return;
        }

        ReturnToOverview();
        ExecuteGraphRequest(GraphRequest.ReturnToOverview());
        e.Handled = true;
    }

    private static void OnLayoutResultChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        GraphLayoutResult? layoutResult = (GraphLayoutResult?)eventArgs.NewValue;

        canvas.UpdateLayoutBounds(layoutResult);
        canvas.RenderLayers(layoutResult);
        canvas.UpdateLayerTransform();
        canvas.ApplyCurrentFocusToViewport();
        canvas.InvalidateMeasure();
        canvas.InvalidateVisual();
    }

    private static void OnDocumentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        canvas.RenderLayers(canvas.LayoutResult);
        canvas.ApplyCurrentFocusToViewport();
        canvas.InvalidateVisual();
    }

    private static void OnViewportPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        canvas.UpdateLayerTransform();
        canvas.InvalidateMeasure();
        canvas.InvalidateVisual();
    }

    private static void OnZoomLimitChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        canvas.CoerceValue(ZoomProperty);
    }

    private static void OnSelectionPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        canvas.RenderLayers(canvas.LayoutResult);
        canvas.InvalidateVisual();
    }

    private static void OnCollapsedGroupIdsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        canvas.RenderLayers(canvas.LayoutResult);
        canvas.InvalidateVisual();
    }

    private static void OnMarkerGroupFilterChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        canvas.RenderLayers(canvas.LayoutResult);
        canvas.InvalidateVisual();
    }


    private static void OnVisualModePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        canvas.RenderLayers(canvas.LayoutResult);
        canvas.InvalidateMeasure();
        canvas.InvalidateVisual();
    }

    private static void OnFocusPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        canvas.RenderLayers(canvas.LayoutResult);
        canvas.ApplyFocusChangeToViewport(eventArgs.Property, eventArgs.NewValue);
        canvas.InvalidateVisual();
    }

    private static object CoerceZoom(DependencyObject dependencyObject, object baseValue)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        double value = (double)baseValue;
        double min = Math.Min(canvas.MinZoom, canvas.MaxZoom);
        double max = Math.Max(canvas.MinZoom, canvas.MaxZoom);
        return Math.Clamp(value, min, max);
    }

    private static object CoerceVisualDensity(DependencyObject dependencyObject, object baseValue)
    {
        double value = (double)baseValue;
        return Math.Clamp(value, MinVisualDensity, MaxVisualDensity);
    }

    private static bool IsFinitePositiveDouble(object value)
    {
        return value is double doubleValue && double.IsFinite(doubleValue) && doubleValue > 0d;
    }

    private static bool IsFiniteDouble(object value)
    {
        return value is double doubleValue && double.IsFinite(doubleValue);
    }

    private void UpdateLayoutBounds(GraphLayoutResult? layoutResult)
    {
        SetValue(LayoutBoundsPropertyKey, GetContentBounds(layoutResult));
    }

    private void RenderLayers(GraphLayoutResult? layoutResult)
    {
        if (layoutResult is null || !layoutResult.Succeeded)
        {
            groupLayer.Clear();
            edgeLayer.Clear();
            nodeLayer.Clear();
            return;
        }

        HashSet<string> selectedGroupIds = ToSelectionSet(SelectedGroupIds);
        HashSet<string> selectedLinkIds = ToSelectionSet(SelectedLinkIds);
        HashSet<string> selectedNodeIds = ToSelectionSet(SelectedNodeIds);
        GraphCanvasVisualContext visualContext = new(ActiveViewMode, VisualDensity, Zoom);
        GraphCanvasFocusContext focusContext = new(FocusedNodeId, FocusedLinkId, FocusedGroupId, SearchText, visualContext.IsFocusMode);
        GraphCanvasCollapseContext collapseContext = GraphCanvasCollapseContext.Create(layoutResult, CollapsedGroupIds);
        GraphCanvasMarkerGroupContext markerGroupContext = GraphCanvasMarkerGroupContext.Create(
            Document,
            layoutResult,
            SelectedGroupIds,
            FocusedGroupId,
            SearchText,
            IsMarkerGroupFilterEnabled);

        if (visualContext.IsAreaOverview)
        {
            IReadOnlyList<GraphCanvasAreaTransition> areaTransitions = CreateAreaOverviewTransitions(layoutResult);
            groupLayer.Render(drawingContext => DrawGroups(drawingContext, layoutResult.Groups, selectedGroupIds, focusContext, collapseContext, visualContext));
            edgeLayer.Render(drawingContext => DrawAreaOverviewTransitions(drawingContext, areaTransitions, focusContext, visualContext));
            nodeLayer.Clear();
            return;
        }

        groupLayer.Render(drawingContext =>
        {
            DrawMarkerGroupHighlights(drawingContext, markerGroupContext, visualContext);
            DrawGroups(drawingContext, layoutResult.Groups, selectedGroupIds, focusContext, collapseContext, visualContext);
        });
        edgeLayer.Render(drawingContext => DrawEdges(drawingContext, layoutResult.Edges, selectedLinkIds, focusContext, collapseContext, markerGroupContext, visualContext));
        nodeLayer.Render(drawingContext => DrawNodes(drawingContext, layoutResult.Nodes, selectedNodeIds, focusContext, collapseContext, markerGroupContext, visualContext));
    }

    private void DrawMarkerGroupHighlights(
        DrawingContext drawingContext,
        GraphCanvasMarkerGroupContext markerGroupContext,
        GraphCanvasVisualContext visualContext)
    {
        foreach (GraphCanvasMarkerGroupHighlight highlight in markerGroupContext.Highlights)
        {
            Rect bounds = ToRect(Expand(highlight.Bounds, 10d));
            drawingContext.DrawRoundedRectangle(MarkerGroupFillBrush, MarkerGroupPen, bounds, NodeCornerRadius, NodeCornerRadius);
            DrawTextIfVisible(drawingContext, highlight.Title, bounds, SelectionBrush, TextAlignment.Left, 8d, 2d, visualContext);
        }
    }

    private void DrawGroups(
        DrawingContext drawingContext,
        IReadOnlyList<GraphLayoutGroup> groups,
        IReadOnlySet<string> selectedGroupIds,
        GraphCanvasFocusContext focusContext,
        GraphCanvasCollapseContext collapseContext,
        GraphCanvasVisualContext visualContext)
    {
        foreach (GraphLayoutGroup group in groups)
        {
            bool isSelected = selectedGroupIds.Contains(group.GroupId);
            bool isFocused = StringComparer.Ordinal.Equals(focusContext.GroupId, group.GroupId);
            bool isSearchMatch = focusContext.MatchesSearch(group.GroupId);
            bool isHighlighted = isSelected || isFocused || isSearchMatch;
            bool isDimmed = focusContext.IsActive && !isHighlighted;
            bool isCollapsed = !visualContext.IsAreaOverview && collapseContext.IsCollapsed(group.GroupId);
            Rect rect = ToRect(group.Bounds);

            DrawWithOptionalOpacity(drawingContext, isDimmed, () =>
            {
                if (isCollapsed)
                {
                    DrawCollapsedGroup(drawingContext, group, rect, isHighlighted, collapseContext.GetBundledTransitionCount(group.GroupId), visualContext);
                    return;
                }

                Brush fill = visualContext.IsAreaOverview ? CollapsedGroupFillBrush : isHighlighted ? SelectedGroupFillBrush : GroupFillBrush;
                Pen pen = visualContext.IsAreaOverview ? AreaOverviewGroupPen : isHighlighted ? SelectedGroupPen : GroupPen;
                drawingContext.DrawRectangle(fill, pen, rect);
                DrawTextIfVisible(drawingContext, group.GroupId, rect, isHighlighted ? SelectionBrush : TextBrush, TextAlignment.Left, 8d, 4d, visualContext);

                if (!visualContext.IsAreaOverview)
                {
                    DrawCollapseToggle(drawingContext, group.Bounds, isCollapsed: false);
                }
            });
        }
    }

    private void DrawEdges(
        DrawingContext drawingContext,
        IReadOnlyList<GraphLayoutEdge> edges,
        IReadOnlySet<string> selectedLinkIds,
        GraphCanvasFocusContext focusContext,
        GraphCanvasCollapseContext collapseContext,
        GraphCanvasMarkerGroupContext markerGroupContext,
        GraphCanvasVisualContext visualContext)
    {
        foreach (GraphLayoutEdge edge in edges)
        {
            if (edge.Points.Count < 2 || collapseContext.IsEdgeBundled(edge) || !markerGroupContext.IsEdgeVisible(edge))
            {
                continue;
            }

            bool isSelected = selectedLinkIds.Contains(edge.LinkId);
            bool isFocused = StringComparer.Ordinal.Equals(focusContext.LinkId, edge.LinkId);
            bool isSearchMatch = focusContext.MatchesSearch(edge.LinkId);
            bool isMarkerGroupMatch = markerGroupContext.IsEdgeHighlighted(edge);
            bool isHighlighted = isSelected || isFocused || isSearchMatch || isMarkerGroupMatch;
            bool isDimmed = (focusContext.IsActive && !isHighlighted) || markerGroupContext.IsEdgeDimmed(edge);
            Pen pen = isHighlighted ? SelectedEdgePen : edge.UsesFallbackGeometry ? FallbackEdgePen : EdgePen;

            DrawWithOptionalOpacity(drawingContext, isDimmed, () =>
            {
                StreamGeometry lineGeometry = CreatePolylineGeometry(edge.Points);
                drawingContext.DrawGeometry(null, pen, lineGeometry);

                StreamGeometry? arrowGeometry = CreateArrowHeadGeometry(edge.Points);
                if (arrowGeometry is not null)
                {
                    drawingContext.DrawGeometry(pen.Brush, null, arrowGeometry);
                }
            });
        }
    }

    private void DrawNodes(
        DrawingContext drawingContext,
        IReadOnlyList<GraphLayoutNode> nodes,
        IReadOnlySet<string> selectedNodeIds,
        GraphCanvasFocusContext focusContext,
        GraphCanvasCollapseContext collapseContext,
        GraphCanvasMarkerGroupContext markerGroupContext,
        GraphCanvasVisualContext visualContext)
    {
        foreach (GraphLayoutNode node in nodes)
        {
            if (collapseContext.IsNodeHidden(node) || !markerGroupContext.IsNodeVisible(node))
            {
                continue;
            }

            bool isSelected = selectedNodeIds.Contains(node.NodeId);
            bool isFocused = StringComparer.Ordinal.Equals(focusContext.NodeId, node.NodeId);
            bool isSearchMatch = focusContext.MatchesSearch(node.NodeId);
            bool isMarkerGroupMatch = markerGroupContext.IsNodeHighlighted(node);
            bool isHighlighted = isSelected || isFocused || isSearchMatch || isMarkerGroupMatch;
            bool isDimmed = (focusContext.IsActive && !isHighlighted) || markerGroupContext.IsNodeDimmed(node);
            Rect rect = ToRect(node.Bounds);

            DrawWithOptionalOpacity(drawingContext, isDimmed, () =>
            {
                drawingContext.DrawRoundedRectangle(NodeFillBrush, isHighlighted ? SelectedNodePen : NodePen, rect, NodeCornerRadius, NodeCornerRadius);
                DrawTextIfVisible(drawingContext, node.NodeId, rect, isHighlighted ? SelectionBrush : TextBrush, TextAlignment.Center, 6d, 0d, visualContext);
            });
        }
    }

    private void DrawCollapsedGroup(
        DrawingContext drawingContext,
        GraphLayoutGroup group,
        Rect bounds,
        bool isHighlighted,
        int bundledTransitionCount,
        GraphCanvasVisualContext visualContext)
    {
        Pen border = isHighlighted ? SelectedGroupPen : CollapsedGroupPen;
        drawingContext.DrawRoundedRectangle(CollapsedGroupFillBrush, border, bounds, NodeCornerRadius, NodeCornerRadius);
        DrawTextIfVisible(drawingContext, group.GroupId, bounds, isHighlighted ? SelectionBrush : TextBrush, TextAlignment.Left, 10d, 6d, visualContext);
        DrawCollapseToggle(drawingContext, group.Bounds, isCollapsed: true);

        string subtitle = bundledTransitionCount == 1
            ? "Collapsed · 1 transition"
            : $"Collapsed · {bundledTransitionCount} transitions";
        Rect subtitleBounds = new(bounds.X, bounds.Y + 18d, bounds.Width, Math.Max(0d, bounds.Height - 18d));
        DrawTextIfVisible(drawingContext, subtitle, subtitleBounds, MutedTextBrush, TextAlignment.Left, 10d, 4d, visualContext);

        if (bundledTransitionCount <= 0)
        {
            return;
        }

        string badgeText = bundledTransitionCount > 99 ? "99+" : bundledTransitionCount.ToString(CultureInfo.InvariantCulture);
        double badgeWidth = badgeText.Length > 2 ? 32d : 26d;
        Rect badgeBounds = new(
            Math.Max(bounds.Left + 8d, bounds.Right - badgeWidth - 10d),
            bounds.Top + 8d,
            badgeWidth,
            18d);

        drawingContext.DrawRoundedRectangle(CollapsedBadgeFillBrush, null, badgeBounds, 9d, 9d);
        DrawText(drawingContext, badgeText, badgeBounds, CollapsedBadgeTextBrush, TextAlignment.Center, 3d, 0d, textScale: 1d);
    }

    private void DrawCollapseToggle(DrawingContext drawingContext, GraphRect groupBounds, bool isCollapsed)
    {
        Rect toggleBounds = GetCollapseToggleBounds(groupBounds);
        drawingContext.DrawRoundedRectangle(BackgroundBrush, CollapsedGroupPen, toggleBounds, 4d, 4d);

        Point left = new(toggleBounds.Left + 5d, toggleBounds.Top + (toggleBounds.Height / 2d));
        Point right = new(toggleBounds.Right - 5d, toggleBounds.Top + (toggleBounds.Height / 2d));
        drawingContext.DrawLine(CollapsedGroupPen, left, right);

        if (!isCollapsed)
        {
            return;
        }

        Point top = new(toggleBounds.Left + (toggleBounds.Width / 2d), toggleBounds.Top + 5d);
        Point bottom = new(toggleBounds.Left + (toggleBounds.Width / 2d), toggleBounds.Bottom - 5d);
        drawingContext.DrawLine(CollapsedGroupPen, top, bottom);
    }

    private void DrawTextIfVisible(
        DrawingContext drawingContext,
        string text,
        Rect bounds,
        Brush brush,
        TextAlignment textAlignment,
        double horizontalPadding,
        double verticalPadding,
        GraphCanvasVisualContext visualContext)
    {
        if (!visualContext.ShouldShowLabels)
        {
            return;
        }

        DrawText(
            drawingContext,
            text,
            bounds,
            brush,
            textAlignment,
            horizontalPadding,
            verticalPadding,
            visualContext.EffectiveDensity);
    }

    private void DrawAreaOverviewTransitions(
        DrawingContext drawingContext,
        IReadOnlyList<GraphCanvasAreaTransition> transitions,
        GraphCanvasFocusContext focusContext,
        GraphCanvasVisualContext visualContext)
    {
        foreach (GraphCanvasAreaTransition transition in transitions)
        {
            bool isHighlighted = StringComparer.Ordinal.Equals(focusContext.GroupId, transition.SourceGroupId)
                || StringComparer.Ordinal.Equals(focusContext.GroupId, transition.TargetGroupId);
            DrawWithOptionalOpacity(drawingContext, focusContext.IsActive && !isHighlighted, () =>
            {
                GraphPoint start = new(transition.Start.X, transition.Start.Y);
                GraphPoint end = new(transition.End.X, transition.End.Y);
                StreamGeometry geometry = CreatePolylineGeometry([start, end]);
                drawingContext.DrawGeometry(null, isHighlighted ? SelectedEdgePen : AreaOverviewEdgePen, geometry);

                StreamGeometry? arrowGeometry = CreateArrowHeadGeometry([start, end]);
                if (arrowGeometry is not null)
                {
                    drawingContext.DrawGeometry((isHighlighted ? SelectedEdgePen : AreaOverviewEdgePen).Brush, null, arrowGeometry);
                }

                string countText = transition.LinkCount > 99 ? "99+" : transition.LinkCount.ToString(CultureInfo.InvariantCulture);
                Rect badgeBounds = new(
                    transition.MidPoint.X - 13d,
                    transition.MidPoint.Y - (AreaOverviewBadgeHeight / 2d),
                    26d,
                    AreaOverviewBadgeHeight);
                drawingContext.DrawRoundedRectangle(AreaOverviewBundleFillBrush, null, badgeBounds, 9d, 9d);
                DrawText(drawingContext, countText, badgeBounds, AreaOverviewBundleTextBrush, TextAlignment.Center, 3d, 0d, textScale: Math.Max(visualContext.EffectiveDensity, 0.9d));
            });
        }
    }

    private void DrawText(
        DrawingContext drawingContext,
        string text,
        Rect bounds,
        Brush brush,
        TextAlignment textAlignment,
        double horizontalPadding,
        double verticalPadding,
        double textScale = 1d)
    {
        double availableWidth = Math.Max(0d, bounds.Width - (horizontalPadding * 2d));
        double availableHeight = Math.Max(0d, bounds.Height - (verticalPadding * 2d));
        if (availableWidth <= 0d || availableHeight <= 0d)
        {
            return;
        }

        FormattedText formattedText = new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            TextTypeface,
            DefaultTextSize * Math.Clamp(textScale, MinVisualDensity, MaxVisualDensity),
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = availableWidth,
            MaxTextHeight = availableHeight,
            TextAlignment = textAlignment,
            Trimming = TextTrimming.CharacterEllipsis
        };

        double y = bounds.Y + verticalPadding + Math.Max(0d, (availableHeight - formattedText.Height) / 2d);
        drawingContext.DrawText(formattedText, new Point(bounds.X + horizontalPadding, y));
    }

    private GraphCanvasHit? HitTestGraph(Point viewPoint)
    {
        GraphLayoutResult? layoutResult = LayoutResult;
        if (layoutResult is null || !layoutResult.Succeeded)
        {
            return null;
        }

        Point contentPoint = ViewToContent(viewPoint);
        GraphCanvasVisualContext visualContext = new(ActiveViewMode, VisualDensity, Zoom);
        GraphCanvasCollapseContext collapseContext = GraphCanvasCollapseContext.Create(layoutResult, CollapsedGroupIds);
        GraphCanvasMarkerGroupContext markerGroupContext = GraphCanvasMarkerGroupContext.Create(
            Document,
            layoutResult,
            SelectedGroupIds,
            FocusedGroupId,
            SearchText,
            IsMarkerGroupFilterEnabled);

        for (int index = layoutResult.Groups.Count - 1; index >= 0; index--)
        {
            GraphLayoutGroup group = layoutResult.Groups[index];
            if (collapseContext.IsCollapsed(group.GroupId) && ToRect(group.Bounds).Contains(contentPoint))
            {
                return new GraphCanvasHit(GraphCanvasHitKind.Group, group.GroupId);
            }
        }

        if (!visualContext.IsAreaOverview)
        {
            for (int index = layoutResult.Nodes.Count - 1; index >= 0; index--)
            {
                GraphLayoutNode node = layoutResult.Nodes[index];
                if (!collapseContext.IsNodeHidden(node) && markerGroupContext.IsNodeVisible(node) && ToRect(node.Bounds).Contains(contentPoint))
                {
                    return new GraphCanvasHit(GraphCanvasHitKind.Node, node.NodeId);
                }
            }

            double edgeTolerance = EdgeHitTolerance / Math.Max(Zoom, 0.000001d);
            foreach (GraphLayoutEdge edge in layoutResult.Edges)
            {
                if (edge.Points.Count < 2 || collapseContext.IsEdgeBundled(edge) || !markerGroupContext.IsEdgeVisible(edge))
                {
                    continue;
                }

                if (IsPointNearPolyline(contentPoint, edge.Points, edgeTolerance))
                {
                    return new GraphCanvasHit(GraphCanvasHitKind.Link, edge.LinkId);
                }
            }
        }

        for (int index = layoutResult.Groups.Count - 1; index >= 0; index--)
        {
            GraphLayoutGroup group = layoutResult.Groups[index];
            if (ToRect(group.Bounds).Contains(contentPoint))
            {
                return new GraphCanvasHit(GraphCanvasHitKind.Group, group.GroupId);
            }
        }

        return null;
    }

    private GraphCanvasHit? HitTestGroupCollapseToggle(Point viewPoint)
    {
        GraphLayoutResult? layoutResult = LayoutResult;
        if (IsAreaOverviewActive || layoutResult is null || !layoutResult.Succeeded)
        {
            return null;
        }

        Point contentPoint = ViewToContent(viewPoint);
        for (int index = layoutResult.Groups.Count - 1; index >= 0; index--)
        {
            GraphLayoutGroup group = layoutResult.Groups[index];
            if (GetCollapseToggleBounds(group.Bounds).Contains(contentPoint))
            {
                return new GraphCanvasHit(GraphCanvasHitKind.Group, group.GroupId);
            }
        }

        return null;
    }

    private void ApplyHitSelection(GraphCanvasHit? hit, bool isMultiSelection)
    {
        if (hit is null)
        {
            if (!isMultiSelection)
            {
                ClearSelection();
                ExecuteGraphRequest(GraphRequest.ClearSelection());
            }

            return;
        }

        switch (hit.Kind)
        {
            case GraphCanvasHitKind.Node:
                SetCurrentValue(SelectedNodeIdsProperty, UpdateSelection(SelectedNodeIds, hit.Id, isMultiSelection));
                ExecuteGraphRequest(GraphRequest.SelectNode(hit.Id, isMultiSelection));
                if (!isMultiSelection)
                {
                    SetCurrentValue(SelectedLinkIdsProperty, Array.Empty<string>());
                    SetCurrentValue(SelectedGroupIdsProperty, Array.Empty<string>());
                }
                break;
            case GraphCanvasHitKind.Link:
                SetCurrentValue(SelectedLinkIdsProperty, UpdateSelection(SelectedLinkIds, hit.Id, isMultiSelection));
                ExecuteGraphRequest(GraphRequest.SelectLink(hit.Id, isMultiSelection));
                if (!isMultiSelection)
                {
                    SetCurrentValue(SelectedNodeIdsProperty, Array.Empty<string>());
                    SetCurrentValue(SelectedGroupIdsProperty, Array.Empty<string>());
                }
                break;
            case GraphCanvasHitKind.Group:
                SetCurrentValue(SelectedGroupIdsProperty, UpdateSelection(SelectedGroupIds, hit.Id, isMultiSelection));
                ExecuteGraphRequest(GraphRequest.SelectGroup(hit.Id, isMultiSelection));
                if (!isMultiSelection)
                {
                    SetCurrentValue(SelectedNodeIdsProperty, Array.Empty<string>());
                    SetCurrentValue(SelectedLinkIdsProperty, Array.Empty<string>());
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported graph canvas hit kind '{hit.Kind}'.");
        }
    }

    private void ApplyHitFocus(GraphCanvasHit? hit)
    {
        if (hit is null)
        {
            ReturnToOverview();
            ExecuteGraphRequest(GraphRequest.ReturnToOverview());
            return;
        }

        switch (hit.Kind)
        {
            case GraphCanvasHitKind.Node:
                _ = FocusNode(hit.Id);
                ExecuteGraphRequest(GraphRequest.OpenNode(hit.Id));
                break;
            case GraphCanvasHitKind.Link:
                _ = FocusLink(hit.Id);
                ExecuteGraphRequest(GraphRequest.OpenLink(hit.Id));
                break;
            case GraphCanvasHitKind.Group:
                _ = DrillDownToGroup(hit.Id);
                ExecuteGraphRequest(GraphRequest.OpenGroup(hit.Id));
                break;
            default:
                throw new InvalidOperationException($"Unsupported graph canvas hit kind '{hit.Kind}'.");
        }
    }

    private void ClearSelection()
    {
        SetCurrentValue(SelectedNodeIdsProperty, Array.Empty<string>());
        SetCurrentValue(SelectedLinkIdsProperty, Array.Empty<string>());
        SetCurrentValue(SelectedGroupIdsProperty, Array.Empty<string>());
    }

    private void ApplyFocusChangeToViewport(DependencyProperty property, object? newValue)
    {
        string? id = NormalizeOptionalText(newValue as string);
        if (id is null || LayoutResult is not { Succeeded: true } || RenderSize.Width <= 0d || RenderSize.Height <= 0d)
        {
            return;
        }

        if (property == FocusedNodeIdProperty && TryGetNodeBounds(id, out GraphRect nodeBounds))
        {
            CenterOnBounds(nodeBounds);
            return;
        }

        if (property == FocusedLinkIdProperty && TryGetLinkBounds(id, out GraphRect linkBounds))
        {
            CenterOnBounds(linkBounds);
            return;
        }

        if (property == FocusedGroupIdProperty && TryGetGroupFocusBounds(id, out GraphRect groupBounds))
        {
            FitToBounds(groupBounds, RenderSize, DefaultFitPadding);
        }
    }

    private void ApplyCurrentFocusToViewport()
    {
        if (RenderSize.Width <= 0d || RenderSize.Height <= 0d || LayoutResult is not { Succeeded: true })
        {
            return;
        }

        if (FocusedNodeId is not null && TryGetNodeBounds(FocusedNodeId, out GraphRect nodeBounds))
        {
            CenterOnBounds(nodeBounds);
            return;
        }

        if (FocusedLinkId is not null && TryGetLinkBounds(FocusedLinkId, out GraphRect linkBounds))
        {
            CenterOnBounds(linkBounds);
            return;
        }

        if (FocusedGroupId is not null && TryGetGroupFocusBounds(FocusedGroupId, out GraphRect groupBounds))
        {
            FitToBounds(groupBounds, RenderSize, DefaultFitPadding);
        }
    }

    private void ExecuteGraphRequest(GraphRequest request)
    {
        ICommand? command = GraphRequestCommand;
        if (command is null || !command.CanExecute(request))
        {
            return;
        }

        command.Execute(request);
    }

    private Point ViewToContent(Point viewPoint)
    {
        return new Point(
            (viewPoint.X - PanX) / Zoom,
            (viewPoint.Y - PanY) / Zoom);
    }


    private IReadOnlyList<GraphCanvasAreaTransition> CreateAreaOverviewTransitions(GraphLayoutResult layoutResult)
    {
        GraphDocument? document = Document;
        if (document is null || layoutResult.Groups.Count == 0 || document.Links.Count == 0)
        {
            return Array.Empty<GraphCanvasAreaTransition>();
        }

        Dictionary<string, GraphLayoutGroup> groupById = layoutResult.Groups.ToDictionary(group => group.GroupId, StringComparer.Ordinal);
        HashSet<string> containerGroupIds = document.Groups
            .Where(group => group.Kind == GraphGroupKind.Container && groupById.ContainsKey(group.Id))
            .Select(group => group.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (containerGroupIds.Count == 0)
        {
            return Array.Empty<GraphCanvasAreaTransition>();
        }

        Dictionary<string, string> groupIdByNodeId = document.Nodes
            .Select(node => new
            {
                node.Id,
                GroupId = node.GroupMemberships.FirstOrDefault(containerGroupIds.Contains)
            })
            .Where(item => item.GroupId is not null)
            .ToDictionary(item => item.Id, item => item.GroupId!, StringComparer.Ordinal);

        Dictionary<(string SourceGroupId, string TargetGroupId), int> transitionCounts = [];
        foreach (GraphLink link in document.Links)
        {
            if (!groupIdByNodeId.TryGetValue(link.SourceNodeId, out string? sourceGroupId)
                || !groupIdByNodeId.TryGetValue(link.TargetNodeId, out string? targetGroupId)
                || StringComparer.Ordinal.Equals(sourceGroupId, targetGroupId))
            {
                continue;
            }

            (string SourceGroupId, string TargetGroupId) key = (sourceGroupId, targetGroupId);
            transitionCounts[key] = transitionCounts.TryGetValue(key, out int count) ? count + 1 : 1;
        }

        return transitionCounts
            .Select(pair =>
            {
                GraphLayoutGroup source = groupById[pair.Key.SourceGroupId];
                GraphLayoutGroup target = groupById[pair.Key.TargetGroupId];
                Point start = GetCenter(source.Bounds);
                Point end = GetCenter(target.Bounds);
                Point midPoint = new((start.X + end.X) / 2d, (start.Y + end.Y) / 2d);
                return new GraphCanvasAreaTransition(
                    pair.Key.SourceGroupId,
                    pair.Key.TargetGroupId,
                    pair.Value,
                    start,
                    end,
                    midPoint);
            })
            .ToArray();
    }

    private bool TryGetNodeBounds(string nodeId, out GraphRect bounds)
    {
        GraphLayoutNode? node = LayoutResult?.Nodes.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.NodeId, nodeId));
        bounds = node?.Bounds ?? default;
        return node is not null;
    }

    private bool TryGetGroupBounds(string groupId, out GraphRect bounds)
    {
        return TryGetContainerGroupBounds(groupId, out bounds);
    }

    private bool TryGetContainerGroupBounds(string groupId, out GraphRect bounds)
    {
        GraphLayoutGroup? group = LayoutResult?.Groups.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.GroupId, groupId));
        bounds = group?.Bounds ?? default;
        return group is not null;
    }

    private bool TryGetGroupFocusBounds(string groupId, out GraphRect bounds)
    {
        if (TryGetContainerGroupBounds(groupId, out bounds))
        {
            return true;
        }

        return TryGetMarkerGroupBounds(groupId, out bounds);
    }

    private bool TryGetMarkerGroupBounds(string groupId, out GraphRect bounds)
    {
        if (!IsMarkerGroup(groupId) || LayoutResult is not { Succeeded: true } layoutResult)
        {
            bounds = default;
            return false;
        }

        HashSet<string> memberNodeIds = GetMarkerGroupMemberNodeIds(groupId);
        GraphRect[] memberBounds = layoutResult.Nodes
            .Where(node => memberNodeIds.Contains(node.NodeId))
            .Select(node => node.Bounds)
            .ToArray();

        if (memberBounds.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = Union(memberBounds);
        return true;
    }

    private bool IsMarkerGroup(string groupId)
    {
        return Document?.Groups.Any(group =>
            group.Kind == GraphGroupKind.Marker
            && StringComparer.Ordinal.Equals(group.Id, groupId)) == true;
    }

    private IReadOnlyList<GraphGroup> GetMarkerGroups()
    {
        return Document?.Groups
            .Where(group => group.Kind == GraphGroupKind.Marker)
            .ToArray()
            ?? Array.Empty<GraphGroup>();
    }

    private HashSet<string> GetMarkerGroupMemberNodeIds(string groupId)
    {
        return Document?.Nodes
            .Where(node => node.GroupMemberships.Contains(groupId, StringComparer.Ordinal))
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
    }

    private bool TryGetLinkBounds(string linkId, out GraphRect bounds)
    {
        GraphLayoutEdge? edge = LayoutResult?.Edges.FirstOrDefault(item => StringComparer.Ordinal.Equals(item.LinkId, linkId));
        if (edge is null || edge.Points.Count == 0)
        {
            bounds = default;
            return false;
        }

        double minX = edge.Points.Min(point => point.X);
        double minY = edge.Points.Min(point => point.Y);
        double maxX = edge.Points.Max(point => point.X);
        double maxY = edge.Points.Max(point => point.Y);
        bounds = new GraphRect(minX, minY, maxX - minX, maxY - minY);
        return true;
    }

    private void CenterOnBounds(GraphRect bounds)
    {
        if (RenderSize.Width <= 0d || RenderSize.Height <= 0d)
        {
            return;
        }

        double contentCenterX = bounds.X + (bounds.Width / 2d);
        double contentCenterY = bounds.Y + (bounds.Height / 2d);
        SetCurrentValue(PanXProperty, (RenderSize.Width / 2d) - (contentCenterX * Zoom));
        SetCurrentValue(PanYProperty, (RenderSize.Height / 2d) - (contentCenterY * Zoom));
    }

    private void FitToBounds(GraphRect bounds, Size viewportSize, double padding)
    {
        if (bounds.Width <= 0d || bounds.Height <= 0d || viewportSize.Width <= 0d || viewportSize.Height <= 0d)
        {
            CenterOnBounds(bounds);
            return;
        }

        double safePadding = Math.Max(0d, padding);
        double availableWidth = Math.Max(1d, viewportSize.Width - (safePadding * 2d));
        double availableHeight = Math.Max(1d, viewportSize.Height - (safePadding * 2d));
        double fittedZoom = CoerceZoomValue(Math.Min(availableWidth / bounds.Width, availableHeight / bounds.Height));
        double scaledWidth = bounds.Width * fittedZoom;
        double scaledHeight = bounds.Height * fittedZoom;

        SetCurrentValue(ZoomProperty, fittedZoom);
        SetCurrentValue(PanXProperty, ((viewportSize.Width - scaledWidth) / 2d) - (bounds.X * fittedZoom));
        SetCurrentValue(PanYProperty, ((viewportSize.Height - scaledHeight) / 2d) - (bounds.Y * fittedZoom));
    }

    private static IReadOnlyList<string> UpdateSelection(
        IReadOnlyList<string> currentSelection,
        string id,
        bool isMultiSelection)
    {
        if (!isMultiSelection)
        {
            return [id];
        }

        List<string> values = currentSelection
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        int existingIndex = values.FindIndex(value => StringComparer.Ordinal.Equals(value, id));
        if (existingIndex >= 0)
        {
            values.RemoveAt(existingIndex);
        }
        else
        {
            values.Add(id);
        }

        return values.Count == 0 ? Array.Empty<string>() : values.ToArray();
    }

    private static IReadOnlyList<string> UpdateCollapsedGroups(
        IReadOnlyList<string> currentCollapsedGroupIds,
        string groupId,
        bool isCollapsed)
    {
        List<string> values = currentCollapsedGroupIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        int existingIndex = values.FindIndex(value => StringComparer.Ordinal.Equals(value, groupId));
        if (isCollapsed)
        {
            if (existingIndex < 0)
            {
                values.Add(groupId);
            }
        }
        else if (existingIndex >= 0)
        {
            values.RemoveAt(existingIndex);
        }

        return values.Count == 0 ? Array.Empty<string>() : values.ToArray();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IReadOnlyList<string> CopySelection(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static HashSet<string> ToSelectionSet(IEnumerable<string> values)
    {
        return new HashSet<string>(values, StringComparer.Ordinal);
    }

    private static bool IsPointNearPolyline(Point point, IReadOnlyList<GraphPoint> polylinePoints, double tolerance)
    {
        double toleranceSquared = tolerance * tolerance;

        for (int index = 0; index < polylinePoints.Count - 1; index++)
        {
            if (GetSquaredDistanceToSegment(point, ToPoint(polylinePoints[index]), ToPoint(polylinePoints[index + 1])) <= toleranceSquared)
            {
                return true;
            }
        }

        return false;
    }

    private static double GetSquaredDistanceToSegment(Point point, Point start, Point end)
    {
        double segmentX = end.X - start.X;
        double segmentY = end.Y - start.Y;
        double segmentLengthSquared = (segmentX * segmentX) + (segmentY * segmentY);
        if (segmentLengthSquared <= 0.000001d)
        {
            double pointDeltaX = point.X - start.X;
            double pointDeltaY = point.Y - start.Y;
            return (pointDeltaX * pointDeltaX) + (pointDeltaY * pointDeltaY);
        }

        double projection = (((point.X - start.X) * segmentX) + ((point.Y - start.Y) * segmentY)) / segmentLengthSquared;
        double clampedProjection = Math.Clamp(projection, 0d, 1d);
        double closestX = start.X + (clampedProjection * segmentX);
        double closestY = start.Y + (clampedProjection * segmentY);
        double deltaX = point.X - closestX;
        double deltaY = point.Y - closestY;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private static void DrawWithOptionalOpacity(DrawingContext drawingContext, bool isDimmed, Action draw)
    {
        if (!isDimmed)
        {
            draw();
            return;
        }

        drawingContext.PushOpacity(FocusDimOpacity);
        draw();
        drawingContext.Pop();
    }

    private static bool IsAreaOverviewMode(GraphViewMode activeViewMode)
    {
        return activeViewMode == GraphViewMode.GroupOverview;
    }

    private static bool ContainsText(string id, string searchText)
    {
        return !string.IsNullOrWhiteSpace(searchText)
            && id.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private static StreamGeometry CreatePolylineGeometry(IReadOnlyList<GraphPoint> points)
    {
        StreamGeometry geometry = new();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(ToPoint(points[0]), false, false);
            context.PolyLineTo(points.Skip(1).Select(ToPoint).ToArray(), true, true);
        }

        geometry.Freeze();
        return geometry;
    }

    private static StreamGeometry? CreateArrowHeadGeometry(IReadOnlyList<GraphPoint> points)
    {
        Point tip = ToPoint(points[^1]);
        Point previous = ToPoint(points[^2]);

        double deltaX = tip.X - previous.X;
        double deltaY = tip.Y - previous.Y;
        double length = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (length < 0.001d)
        {
            return null;
        }

        double unitX = deltaX / length;
        double unitY = deltaY / length;
        Point baseCenter = new(tip.X - (unitX * ArrowLength), tip.Y - (unitY * ArrowLength));
        Point left = new(baseCenter.X - (unitY * ArrowHalfWidth), baseCenter.Y + (unitX * ArrowHalfWidth));
        Point right = new(baseCenter.X + (unitY * ArrowHalfWidth), baseCenter.Y - (unitX * ArrowHalfWidth));

        StreamGeometry geometry = new();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(tip, true, true);
            context.LineTo(left, true, true);
            context.LineTo(right, true, true);
        }

        geometry.Freeze();
        return geometry;
    }

    private static GraphRect GetContentBounds(GraphLayoutResult? layoutResult)
    {
        if (layoutResult?.GraphBounds is { } graphBounds)
        {
            return graphBounds;
        }

        return new GraphRect(0d, 0d, 0d, 0d);
    }

    private static GraphRect Union(IReadOnlyList<GraphRect> rects)
    {
        if (rects.Count == 0)
        {
            return new GraphRect(0d, 0d, 0d, 0d);
        }

        double minX = rects.Min(rect => rect.X);
        double minY = rects.Min(rect => rect.Y);
        double maxX = rects.Max(rect => rect.X + rect.Width);
        double maxY = rects.Max(rect => rect.Y + rect.Height);
        return new GraphRect(minX, minY, maxX - minX, maxY - minY);
    }

    private static GraphRect Expand(GraphRect rect, double padding)
    {
        double safePadding = Math.Max(0d, padding);
        return new GraphRect(
            rect.X - safePadding,
            rect.Y - safePadding,
            rect.Width + (safePadding * 2d),
            rect.Height + (safePadding * 2d));
    }

    private static Rect GetCollapseToggleBounds(GraphRect groupBounds)
    {
        double x = groupBounds.X + Math.Max(4d, groupBounds.Width - CollapseToggleSize - 6d);
        double y = groupBounds.Y + 6d;
        return new Rect(x, y, CollapseToggleSize, CollapseToggleSize);
    }

    private static Rect ToRect(GraphRect rect)
    {
        return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
    }

    private static Point GetCenter(GraphRect rect)
    {
        return new Point(rect.X + (rect.Width / 2d), rect.Y + (rect.Height / 2d));
    }

    private static bool ContainsPoint(GraphRect rect, GraphPoint point)
    {
        return ToRect(rect).Contains(ToPoint(point));
    }

    private static bool ContainsPoint(GraphRect rect, Point point)
    {
        return ToRect(rect).Contains(point);
    }

    private static bool IntersectsSegment(GraphRect rect, GraphPoint start, GraphPoint end)
    {
        Rect bounds = ToRect(rect);
        Point startPoint = ToPoint(start);
        Point endPoint = ToPoint(end);
        if (bounds.Contains(startPoint) || bounds.Contains(endPoint))
        {
            return true;
        }

        Rect segmentBounds = new(
            new Point(Math.Min(startPoint.X, endPoint.X), Math.Min(startPoint.Y, endPoint.Y)),
            new Point(Math.Max(startPoint.X, endPoint.X), Math.Max(startPoint.Y, endPoint.Y)));
        return bounds.IntersectsWith(segmentBounds);
    }

    private static Point ToPoint(GraphPoint point)
    {
        return new Point(point.X, point.Y);
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    private static Pen CreateFrozenPen(Color color, double thickness, DashStyle dashStyle)
    {
        Pen pen = new(CreateFrozenBrush(color), thickness)
        {
            DashStyle = dashStyle
        };
        pen.Freeze();
        return pen;
    }

    private static MatrixTransform CreateViewportTransform(double zoom, double panX, double panY)
    {
        Matrix matrix = new(zoom, 0d, 0d, zoom, panX, panY);
        MatrixTransform transform = new(matrix);
        transform.Freeze();
        return transform;
    }

    private double CoerceZoomValue(double zoom)
    {
        double min = Math.Min(MinZoom, MaxZoom);
        double max = Math.Max(MinZoom, MaxZoom);
        return Math.Clamp(zoom, min, max);
    }

    private void UpdateLayerTransform()
    {
        Transform transform = CreateViewportTransform(Zoom, PanX, PanY);
        groupLayer.Visual.Transform = transform;
        edgeLayer.Visual.Transform = transform;
        nodeLayer.Visual.Transform = transform;
    }

    private void EndPan()
    {
        if (!isPanning)
        {
            return;
        }

        isPanning = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        Cursor = previousCursor;
        previousCursor = null;
    }

    private sealed record GraphCanvasAreaTransition(
        string SourceGroupId,
        string TargetGroupId,
        int LinkCount,
        Point Start,
        Point End,
        Point MidPoint);

    private sealed record GraphCanvasVisualContext(GraphViewMode ActiveViewMode, double VisualDensity, double Zoom)
    {
        public double EffectiveDensity => Math.Clamp(VisualDensity, MinVisualDensity, MaxVisualDensity);

        public bool IsAreaOverview => IsAreaOverviewMode(ActiveViewMode);

        public bool IsFocusMode => ActiveViewMode == GraphViewMode.Focus;

        public bool ShouldShowLabels => EffectiveDensity > MinVisualDensity
            && ((Zoom * EffectiveDensity) >= LowDensityLabelZoomThreshold || IsAreaOverview);
    }

    private sealed class GraphCanvasMarkerGroupContext
    {
        private readonly IReadOnlyDictionary<string, (string SourceNodeId, string TargetNodeId)> linkEndpointsById;
        private readonly HashSet<string> highlightedNodeIds;
        private readonly HashSet<string> filterNodeIds;
        private readonly bool isMarkerGroupActive;
        private readonly bool isFilterEnabled;

        private GraphCanvasMarkerGroupContext(
            IReadOnlyList<GraphCanvasMarkerGroupHighlight> highlights,
            IReadOnlyDictionary<string, (string SourceNodeId, string TargetNodeId)> linkEndpointsById,
            HashSet<string> highlightedNodeIds,
            HashSet<string> filterNodeIds,
            bool isMarkerGroupActive,
            bool isFilterEnabled)
        {
            Highlights = highlights;
            this.linkEndpointsById = linkEndpointsById;
            this.highlightedNodeIds = highlightedNodeIds;
            this.filterNodeIds = filterNodeIds;
            this.isMarkerGroupActive = isMarkerGroupActive;
            this.isFilterEnabled = isFilterEnabled;
        }

        public IReadOnlyList<GraphCanvasMarkerGroupHighlight> Highlights { get; }

        public static GraphCanvasMarkerGroupContext Create(
            GraphDocument? document,
            GraphLayoutResult layoutResult,
            IReadOnlyList<string> selectedGroupIds,
            string? focusedGroupId,
            string searchText,
            bool isFilterEnabled)
        {
            if (document is null)
            {
                return Empty;
            }

            GraphGroup[] markerGroups = document.Groups
                .Where(group => group.Kind == GraphGroupKind.Marker)
                .ToArray();
            if (markerGroups.Length == 0)
            {
                return Empty;
            }

            HashSet<string> markerGroupIds = markerGroups
                .Select(group => group.Id)
                .ToHashSet(StringComparer.Ordinal);

            HashSet<string> selectedMarkerGroupIds = selectedGroupIds
                .Where(markerGroupIds.Contains)
                .ToHashSet(StringComparer.Ordinal);

            HashSet<string> activeMarkerGroupIds = new(selectedMarkerGroupIds, StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(focusedGroupId) && markerGroupIds.Contains(focusedGroupId))
            {
                activeMarkerGroupIds.Add(focusedGroupId);
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                foreach (GraphGroup markerGroup in markerGroups)
                {
                    if (ContainsText(markerGroup.Id, searchText) || ContainsText(markerGroup.Title, searchText))
                    {
                        activeMarkerGroupIds.Add(markerGroup.Id);
                    }
                }
            }

            Dictionary<string, HashSet<string>> memberNodeIdsByGroupId = markerGroups.ToDictionary(
                group => group.Id,
                group => document.Nodes
                    .Where(node => node.GroupMemberships.Contains(group.Id, StringComparer.Ordinal))
                    .Select(node => node.Id)
                    .ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);

            HashSet<string> highlightedNodeIds = activeMarkerGroupIds
                .SelectMany(groupId => memberNodeIdsByGroupId.TryGetValue(groupId, out HashSet<string>? nodeIds) ? nodeIds.AsEnumerable() : Enumerable.Empty<string>())
                .ToHashSet(StringComparer.Ordinal);

            HashSet<string> filterNodeIds = isFilterEnabled
                ? selectedMarkerGroupIds
                    .SelectMany(groupId => memberNodeIdsByGroupId.TryGetValue(groupId, out HashSet<string>? nodeIds) ? nodeIds.AsEnumerable() : Enumerable.Empty<string>())
                    .ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            Dictionary<string, GraphLayoutNode> layoutNodeById = layoutResult.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
            List<GraphCanvasMarkerGroupHighlight> highlights = [];
            foreach (string activeMarkerGroupId in activeMarkerGroupIds)
            {
                if (!memberNodeIdsByGroupId.TryGetValue(activeMarkerGroupId, out HashSet<string>? memberNodeIds))
                {
                    continue;
                }

                GraphRect[] bounds = memberNodeIds
                    .Where(layoutNodeById.ContainsKey)
                    .Select(nodeId => layoutNodeById[nodeId].Bounds)
                    .ToArray();
                if (bounds.Length == 0)
                {
                    continue;
                }

                GraphGroup markerGroup = markerGroups.First(group => StringComparer.Ordinal.Equals(group.Id, activeMarkerGroupId));
                highlights.Add(new GraphCanvasMarkerGroupHighlight(markerGroup.Id, markerGroup.Title, Union(bounds)));
            }

            Dictionary<string, (string SourceNodeId, string TargetNodeId)> linkEndpointsById = document.Links.ToDictionary(
                link => link.Id,
                link => (SourceNodeId: link.SourceNodeId, TargetNodeId: link.TargetNodeId),
                StringComparer.Ordinal);

            return new GraphCanvasMarkerGroupContext(
                highlights,
                linkEndpointsById,
                highlightedNodeIds,
                filterNodeIds,
                activeMarkerGroupIds.Count > 0,
                isFilterEnabled && selectedMarkerGroupIds.Count > 0);
        }

        public bool IsNodeVisible(GraphLayoutNode node)
        {
            return !isFilterEnabled || filterNodeIds.Contains(node.NodeId);
        }

        public bool IsNodeHighlighted(GraphLayoutNode node)
        {
            return highlightedNodeIds.Contains(node.NodeId);
        }

        public bool IsNodeDimmed(GraphLayoutNode node)
        {
            return isMarkerGroupActive && !IsNodeHighlighted(node);
        }

        public bool IsEdgeVisible(GraphLayoutEdge edge)
        {
            if (!isFilterEnabled || !linkEndpointsById.TryGetValue(edge.LinkId, out (string SourceNodeId, string TargetNodeId) endpoints))
            {
                return true;
            }

            return filterNodeIds.Contains(endpoints.SourceNodeId)
                || filterNodeIds.Contains(endpoints.TargetNodeId);
        }

        public bool IsEdgeHighlighted(GraphLayoutEdge edge)
        {
            return linkEndpointsById.TryGetValue(edge.LinkId, out (string SourceNodeId, string TargetNodeId) endpoints)
                && (highlightedNodeIds.Contains(endpoints.SourceNodeId) || highlightedNodeIds.Contains(endpoints.TargetNodeId));
        }

        public bool IsEdgeDimmed(GraphLayoutEdge edge)
        {
            return isMarkerGroupActive && !IsEdgeHighlighted(edge);
        }

        private static GraphCanvasMarkerGroupContext Empty { get; } = new(
            Array.Empty<GraphCanvasMarkerGroupHighlight>(),
            new Dictionary<string, (string SourceNodeId, string TargetNodeId)>(),
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            isMarkerGroupActive: false,
            isFilterEnabled: false);
    }

    private sealed record GraphCanvasMarkerGroupHighlight(string GroupId, string Title, GraphRect Bounds);

    private sealed class GraphCanvasCollapseContext
    {
        private readonly IReadOnlyList<GraphLayoutGroup> collapsedGroups;
        private readonly IReadOnlyDictionary<string, int> bundledTransitionCountByGroupId;

        private GraphCanvasCollapseContext(
            IReadOnlyList<GraphLayoutGroup> collapsedGroups,
            IReadOnlyDictionary<string, int> bundledTransitionCountByGroupId)
        {
            this.collapsedGroups = collapsedGroups;
            this.bundledTransitionCountByGroupId = bundledTransitionCountByGroupId;
        }

        public static GraphCanvasCollapseContext Create(
            GraphLayoutResult layoutResult,
            IReadOnlyList<string> collapsedGroupIds)
        {
            HashSet<string> collapsedGroupIdSet = new(collapsedGroupIds, StringComparer.Ordinal);
            GraphLayoutGroup[] groups = layoutResult.Groups
                .Where(group => collapsedGroupIdSet.Contains(group.GroupId))
                .ToArray();

            Dictionary<string, int> bundledCounts = groups.ToDictionary(
                group => group.GroupId,
                group => layoutResult.Edges.Count(edge => IsBundledTransitionForGroup(edge, group.Bounds)),
                StringComparer.Ordinal);

            return new GraphCanvasCollapseContext(groups, bundledCounts);
        }

        public bool IsCollapsed(string groupId)
        {
            return collapsedGroups.Any(group => StringComparer.Ordinal.Equals(group.GroupId, groupId));
        }

        public int GetBundledTransitionCount(string groupId)
        {
            return bundledTransitionCountByGroupId.TryGetValue(groupId, out int count) ? count : 0;
        }

        public bool IsNodeHidden(GraphLayoutNode node)
        {
            Point center = GetCenter(node.Bounds);
            return collapsedGroups.Any(group => ContainsPoint(group.Bounds, center));
        }

        public bool IsEdgeBundled(GraphLayoutEdge edge)
        {
            return edge.Points.Count >= 2 && collapsedGroups.Any(group => EdgeTouchesGroup(edge, group.Bounds));
        }

        private static bool IsBundledTransitionForGroup(GraphLayoutEdge edge, GraphRect groupBounds)
        {
            if (edge.Points.Count < 2 || !EdgeTouchesGroup(edge, groupBounds))
            {
                return false;
            }

            return edge.Points.Any(point => !ContainsPoint(groupBounds, point));
        }

        private static bool EdgeTouchesGroup(GraphLayoutEdge edge, GraphRect groupBounds)
        {
            if (edge.Points.Any(point => ContainsPoint(groupBounds, point)))
            {
                return true;
            }

            for (int index = 0; index < edge.Points.Count - 1; index++)
            {
                if (IntersectsSegment(groupBounds, edge.Points[index], edge.Points[index + 1]))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private sealed record GraphCanvasFocusContext(string? NodeId, string? LinkId, string? GroupId, string SearchText, bool IsFocusMode)
    {
        public bool IsActive => NodeId is not null
            || LinkId is not null
            || GroupId is not null
            || !string.IsNullOrWhiteSpace(SearchText);

        public bool MatchesSearch(string id)
        {
            return ContainsText(id, SearchText);
        }
    }

    private enum GraphCanvasHitKind
    {
        Node,
        Link,
        Group,
    }

    private sealed record GraphCanvasHit(GraphCanvasHitKind Kind, string Id);
}
