using System.Windows;
using System.Windows.Input;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Core.Requests;

namespace VIA.WPF.Graph.Wpf.Controls;

/// <summary>
/// Skia-based graph surface for rendering neutral graph layout data in a WPF host.
/// </summary>
public sealed class SkiaGraphSurface : SKElement
{
    public static readonly DependencyProperty LayoutResultProperty = DependencyProperty.Register(
        nameof(LayoutResult),
        typeof(GraphLayoutResult),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsRender,
            OnLayoutResultChanged));

    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document),
        typeof(GraphDocument),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsRender,
            OnDocumentChanged));

    private static readonly DependencyPropertyKey LayoutBoundsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(LayoutBounds),
        typeof(GraphRect),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(new GraphRect(0d, 0d, 0d, 0d)));

    public static readonly DependencyProperty LayoutBoundsProperty = LayoutBoundsPropertyKey.DependencyProperty;

    public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
        nameof(Zoom),
        typeof(double),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(
            1d,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnViewportPropertyChanged,
            CoerceZoom),
        IsFinitePositiveDouble);

    public static readonly DependencyProperty MinZoomProperty = DependencyProperty.Register(
        nameof(MinZoom),
        typeof(double),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(0.04d, OnZoomLimitChanged),
        IsFinitePositiveDouble);

    public static readonly DependencyProperty MaxZoomProperty = DependencyProperty.Register(
        nameof(MaxZoom),
        typeof(double),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(10d, OnZoomLimitChanged),
        IsFinitePositiveDouble);

    public static readonly DependencyProperty PanXProperty = DependencyProperty.Register(
        nameof(PanX),
        typeof(double),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(
            0d,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnViewportPropertyChanged),
        IsFiniteDouble);

    public static readonly DependencyProperty PanYProperty = DependencyProperty.Register(
        nameof(PanY),
        typeof(double),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(
            0d,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnViewportPropertyChanged),
        IsFiniteDouble);

    public static readonly DependencyProperty SelectedNodeIdsProperty = DependencyProperty.Register(
        nameof(SelectedNodeIds),
        typeof(IReadOnlyList<string>),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(
            Array.Empty<string>(),
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnSelectionPropertyChanged));

    public static readonly DependencyProperty SelectedLinkIdsProperty = DependencyProperty.Register(
        nameof(SelectedLinkIds),
        typeof(IReadOnlyList<string>),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(
            Array.Empty<string>(),
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnSelectionPropertyChanged));

    public static readonly DependencyProperty SelectedGroupIdsProperty = DependencyProperty.Register(
        nameof(SelectedGroupIds),
        typeof(IReadOnlyList<string>),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(
            Array.Empty<string>(),
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnSelectionPropertyChanged));

    public static readonly DependencyProperty ActiveViewModeProperty = DependencyProperty.Register(
        nameof(ActiveViewMode),
        typeof(GraphViewMode),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(
            GraphViewMode.Overview,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnVisualStateChanged));

    public static readonly DependencyProperty VisualDensityProperty = DependencyProperty.Register(
        nameof(VisualDensity),
        typeof(double),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(
            1d,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnVisualStateChanged,
            CoerceVisualDensity),
        IsFinitePositiveDouble);

    public static readonly DependencyProperty FocusedNodeIdProperty = DependencyProperty.Register(
        nameof(FocusedNodeId),
        typeof(string),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnFocusPropertyChanged));

    public static readonly DependencyProperty FocusedLinkIdProperty = DependencyProperty.Register(
        nameof(FocusedLinkId),
        typeof(string),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnFocusPropertyChanged));

    public static readonly DependencyProperty FocusedGroupIdProperty = DependencyProperty.Register(
        nameof(FocusedGroupId),
        typeof(string),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender,
            OnFocusPropertyChanged));

    public static readonly DependencyProperty GraphRequestCommandProperty = DependencyProperty.Register(
        nameof(GraphRequestCommand),
        typeof(ICommand),
        typeof(SkiaGraphSurface),
        new FrameworkPropertyMetadata(null));

    private const double DefaultFitPadding = 42d;
    private const double ZoomWheelFactor = 1.12d;
    private const double MinVisualDensity = 0.55d;
    private const double MaxVisualDensity = 1.35d;
    private const double EdgeHitTolerance = 7d;
    private const float NodeCornerRadius = 7f;
    private const float NormalNodeShadowOffset = 2.2f;
    private const float NormalNodeShadowBlur = 5.4f;
    private const float NormalNodeShadowExpansion = 0.65f;
    private const float SelectedNodeShadowOffset = 6.4f;
    private const float SelectedNodeShadowBlur = 12.2f;
    private const float SelectedNodeShadowExpansion = 1.8f;
    private const float SelectedNodeAmbientShadowOffset = 2.8f;
    private const float SelectedNodeAmbientShadowBlur = 6.4f;
    private const float SelectedNodeAmbientShadowExpansion = 0.9f;

    private static readonly SKTypeface TextTypeface = SKTypeface.FromFamilyName("Segoe UI");
    private static readonly SKColor SurfaceBackgroundColor = new(244, 246, 248);
    private static readonly SKColor SurfaceGridColor = new(225, 234, 242, 155);
    private static readonly SKColor SurfaceVignetteColor = new(214, 226, 238, 70);
    private static readonly SKColor GroupFillColor = new(226, 237, 247, 72);
    private static readonly SKColor GroupStrokeColor = new(126, 154, 179, 120);
    private static readonly SKColor SelectedGroupFillColor = new(209, 230, 251, 96);
    private static readonly SKColor SelectedGroupStrokeColor = new(31, 121, 208, 185);
    private static readonly SKColor NodeFillTopColor = new(255, 255, 255);
    private static readonly SKColor NodeFillBottomColor = new(244, 249, 253);
    private static readonly SKColor PopupFillTopColor = new(255, 252, 244);
    private static readonly SKColor PopupFillBottomColor = new(255, 246, 225);
    private static readonly SKColor ExternalFillTopColor = new(252, 247, 255);
    private static readonly SKColor ExternalFillBottomColor = new(246, 237, 251);
    private static readonly SKColor TextColor = new(23, 36, 53);
    private static readonly SKColor MutedTextColor = new(92, 112, 130);
    private static readonly SKColor EdgeColor = new(78, 103, 126);
    private static readonly SKColor PrimaryEdgeColor = new(27, 96, 166);
    private static readonly SKColor BackEdgeColor = new(171, 89, 68);
    private static readonly SKColor PopupEdgeColor = new(153, 111, 37);
    private static readonly SKColor ExternalEdgeColor = new(125, 76, 138);
    private static readonly SKColor DiagnosticEdgeColor = new(126, 86, 86);
    private static readonly SKColor SelectionColor = new(31, 121, 208);
    private static readonly SKColor NodeBorderColor = new(197, 214, 229);
    private static readonly SKColor SelectedNodeBorderColor = new(77, 144, 206);
    private static readonly SKColor SelectedNodeGlowColor = new(58, 122, 181, 58);
    private static readonly SKColor NodeShadowColor = new(34, 42, 50);

    private bool isPanning;
    private Point lastPanPoint;
    private Cursor? previousCursor;

    public SkiaGraphSurface()
    {
        ClipToBounds = true;
        Focusable = true;
        PaintSurface += OnPaintSurface;
        SizeChanged += OnSurfaceSizeChanged;
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

    public ICommand? GraphRequestCommand
    {
        get => (ICommand?)GetValue(GraphRequestCommandProperty);
        set => SetValue(GraphRequestCommandProperty, value);
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

        SetCurrentValue(ZoomProperty, fittedZoom);
        SetCurrentValue(PanXProperty, ((viewportSize.Width - scaledWidth) / 2d) - (bounds.X * fittedZoom));
        SetCurrentValue(PanYProperty, ((viewportSize.Height - scaledHeight) / 2d) - (bounds.Y * fittedZoom));
        InvalidateGraph();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (LayoutBounds.Width <= 0d || LayoutBounds.Height <= 0d)
        {
            return;
        }

        Focus();
        Point mousePosition = e.GetPosition(this);
        GraphPoint worldPoint = ScreenToWorld(mousePosition);
        double requestedZoom = Zoom * (e.Delta > 0 ? ZoomWheelFactor : 1d / ZoomWheelFactor);
        double nextZoom = CoerceZoomValue(requestedZoom);

        SetCurrentValue(ZoomProperty, nextZoom);
        SetCurrentValue(PanXProperty, mousePosition.X - (worldPoint.X * nextZoom));
        SetCurrentValue(PanYProperty, mousePosition.Y - (worldPoint.Y * nextZoom));
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.ChangedButton is MouseButton.Middle or MouseButton.Right)
        {
            BeginPan(e.GetPosition(this));
            e.Handled = true;
            return;
        }

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        Focus();
        Point mousePosition = e.GetPosition(this);
        bool isMultiSelection = Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        GraphSurfaceHit? hit = HitTestGraph(mousePosition);
        ApplySelection(hit, isMultiSelection, e.ClickCount > 1);
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

        SetCurrentValue(SelectedNodeIdsProperty, Array.Empty<string>());
        SetCurrentValue(SelectedLinkIdsProperty, Array.Empty<string>());
        SetCurrentValue(SelectedGroupIdsProperty, Array.Empty<string>());
        SetCurrentValue(FocusedNodeIdProperty, null);
        SetCurrentValue(FocusedLinkIdProperty, null);
        SetCurrentValue(FocusedGroupIdProperty, null);
        ExecuteGraphRequest(GraphRequest.ClearSelection());
        e.Handled = true;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        SKCanvas canvas = e.Surface.Canvas;
        canvas.Clear(SurfaceBackgroundColor);

        double actualWidth = Math.Max(1d, ActualWidth);
        double actualHeight = Math.Max(1d, ActualHeight);
        float scaleX = e.Info.Width / (float)actualWidth;
        float scaleY = e.Info.Height / (float)actualHeight;

        canvas.Save();
        canvas.Scale(scaleX, scaleY);
        DrawSurfaceBackground(canvas, (float)actualWidth, (float)actualHeight);

        GraphLayoutResult? layout = LayoutResult;
        GraphDocument? document = Document;
        if (layout is null || !layout.Succeeded || document is null)
        {
            DrawEmptyState(canvas, (float)actualWidth, (float)actualHeight, layout?.Error?.Message ?? "No layout data.");
            canvas.Restore();
            return;
        }

        canvas.Save();
        canvas.Translate((float)PanX, (float)PanY);
        canvas.Scale((float)Zoom);

        GraphSurfaceModel model = GraphSurfaceModel.Create(document, layout);
        GraphFocusSet focusSet = GraphFocusSet.Create(document, SelectedNodeIds, SelectedLinkIds, FocusedNodeId, FocusedLinkId, FocusedGroupId, ActiveViewMode);

        DrawGroups(canvas, model, focusSet);
        DrawEdges(canvas, model, focusSet, drawStrokes: true, drawDecorations: false);
        DrawNodes(canvas, model, focusSet);
        DrawEdges(canvas, model, focusSet, drawStrokes: false, drawDecorations: true);

        canvas.Restore();
        canvas.Restore();
    }

    private void DrawSurfaceBackground(SKCanvas canvas, float width, float height)
    {
        // Product presentation mode intentionally avoids a technical editor grid or an inner canvas fill.
        // The WPF host already supplies the graph area's outer surface.
    }

    private void DrawEmptyState(SKCanvas canvas, float width, float height, string message)
    {
        using SKPaint cardPaint = new()
        {
            IsAntialias = true,
            Color = new SKColor(255, 255, 255, 230),
            Style = SKPaintStyle.Fill
        };
        using SKPaint strokePaint = new()
        {
            IsAntialias = true,
            Color = new SKColor(200, 216, 229),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.4f
        };
        using SkiaTextStyle textPaint = CreateTextPaint(TextColor, 16f, isBold: true);
        using SkiaTextStyle subTextPaint = CreateTextPaint(MutedTextColor, 12.5f, isBold: false);

        SKRect card = new((width / 2f) - 190f, (height / 2f) - 58f, (width / 2f) + 190f, (height / 2f) + 58f);
        canvas.DrawRoundRect(card, 14f, 14f, cardPaint);
        canvas.DrawRoundRect(card, 14f, 14f, strokePaint);
        DrawTextClipped(canvas, "Skia graph surface", card.Left + 22f, card.Top + 38f, card.Width - 44f, textPaint);
        DrawTextClipped(canvas, message, card.Left + 22f, card.Top + 68f, card.Width - 44f, subTextPaint);
    }

    private void DrawGroups(SKCanvas canvas, GraphSurfaceModel model, GraphFocusSet focusSet)
    {
        foreach (GraphLayoutGroup layoutGroup in model.Layout.Groups.OrderByDescending(group => group.Bounds.Width * group.Bounds.Height))
        {
            if (!model.GroupsById.TryGetValue(layoutGroup.GroupId, out GraphGroup? group) || group.Kind != GraphGroupKind.Container)
            {
                continue;
            }

            if (!ShouldDrawGroup(group, model))
            {
                continue;
            }

            bool isSelected = SelectedGroupIds.Contains(group.Id, StringComparer.Ordinal);
            bool isFocused = StringComparer.Ordinal.Equals(FocusedGroupId, group.Id);
            bool isEmphasized = focusSet.IsGroupRelevant(group.Id) || isFocused;
            float alpha = focusSet.GetAlpha(isEmphasized);
            SKRect rect = ToRect(layoutGroup.Bounds);

            using SKPaint fillPaint = new()
            {
                IsAntialias = true,
                Color = WithAlpha(isSelected ? SelectedGroupFillColor : GroupFillColor, alpha),
                Style = SKPaintStyle.Fill
            };
            using SKPaint strokePaint = new()
            {
                IsAntialias = true,
                Color = WithAlpha(isSelected ? SelectedGroupStrokeColor : GroupStrokeColor, alpha),
                StrokeWidth = (isSelected ? 1.8f : 1.05f) / (float)Math.Max(Zoom, 0.25d),
                Style = SKPaintStyle.Stroke,
                PathEffect = SKPathEffect.CreateDash([8f, 6f], 0f)
            };

            SKRect paddedRect = rect;
            paddedRect.Inflate(14f, 16f);
            canvas.DrawRoundRect(paddedRect, 14f, 14f, fillPaint);
            canvas.DrawRoundRect(paddedRect, 14f, 14f, strokePaint);

            using SkiaTextStyle titlePaint = CreateTextPaint(WithAlpha(isSelected ? SelectionColor : MutedTextColor, Math.Min(alpha, 0.9f)), 13f / (float)Math.Sqrt(Math.Max(Zoom, 0.55d)), isBold: true);
            DrawTextClipped(canvas, group.Title, paddedRect.Left + 16f, paddedRect.Top + 21f, Math.Max(60f, paddedRect.Width - 32f), titlePaint);
        }
    }

    private bool ShouldDrawGroup(GraphGroup group, GraphSurfaceModel model)
    {
        if (ActiveViewMode == GraphViewMode.GroupOverview)
        {
            return true;
        }

        if (SelectedGroupIds.Contains(group.Id, StringComparer.Ordinal) || StringComparer.Ordinal.Equals(FocusedGroupId, group.Id))
        {
            return true;
        }

        if (ActiveViewMode != GraphViewMode.Focus)
        {
            return false;
        }

        string? focusedNodeId = FocusedNodeId ?? SelectedNodeIds.FirstOrDefault();
        return !string.IsNullOrWhiteSpace(focusedNodeId)
            && model.NodesById.TryGetValue(focusedNodeId, out GraphNode? focusedNode)
            && focusedNode.GroupMemberships.Contains(group.Id, StringComparer.Ordinal);
    }

    private void DrawEdges(SKCanvas canvas, GraphSurfaceModel model, GraphFocusSet focusSet, bool drawStrokes, bool drawDecorations)
    {
        foreach (GraphLayoutEdge edge in model.Layout.Edges)
        {
            if (!model.LinksById.TryGetValue(edge.LinkId, out GraphLink? link))
            {
                continue;
            }

            IReadOnlyList<GraphPoint> points = ResolveEdgePoints(edge, link, model);
            if (points.Count < 2)
            {
                continue;
            }

            bool isSelected = SelectedLinkIds.Contains(link.Id, StringComparer.Ordinal) || StringComparer.Ordinal.Equals(FocusedLinkId, link.Id);
            bool isRelevant = focusSet.IsLinkRelevant(link.Id, link.SourceNodeId, link.TargetNodeId);
            float alpha = focusSet.GetAlpha(isRelevant);
            SKColor color = isSelected ? SelectionColor : GetEdgeColor(link);

            if (drawStrokes)
            {
                float strokeWidth = (isSelected ? 3.4f : 2.1f) / (float)Math.Sqrt(Math.Max(Zoom, 0.35d));
                using SKPaint paint = new()
                {
                    IsAntialias = true,
                    Color = WithAlpha(color, alpha),
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = strokeWidth,
                    StrokeCap = SKStrokeCap.Round,
                    StrokeJoin = SKStrokeJoin.Round,
                    PathEffect = CreatePathEffect(link, edge)
                };

                using SKPath path = CreateEdgePath(points);
                canvas.DrawPath(path, paint);
            }

            if (drawDecorations)
            {
                SKColor decorationColor = WithAlpha(color, alpha);
                DrawArrowHead(canvas, points, decorationColor, isSelected);
                DrawEdgeLabel(canvas, link, points, decorationColor);
            }
        }
    }

    private void DrawNodes(SKCanvas canvas, GraphSurfaceModel model, GraphFocusSet focusSet)
    {
        foreach (GraphLayoutNode layoutNode in model.Layout.Nodes)
        {
            if (!model.NodesById.TryGetValue(layoutNode.NodeId, out GraphNode? node))
            {
                continue;
            }

            bool isSelected = SelectedNodeIds.Contains(node.Id, StringComparer.Ordinal) || StringComparer.Ordinal.Equals(FocusedNodeId, node.Id);
            bool isRelevant = focusSet.IsNodeRelevant(node.Id);
            float alpha = focusSet.GetAlpha(isRelevant);
            DrawNode(canvas, node, layoutNode.Bounds, isSelected, alpha);
        }
    }

    private void DrawNode(SKCanvas canvas, GraphNode node, GraphRect bounds, bool isSelected, float alpha)
    {
        SKRect rect = ToRect(bounds);
        SKRect cardRect = rect;
        float density = (float)Math.Clamp(VisualDensity, MinVisualDensity, MaxVisualDensity);
        float insetX = Math.Max(0f, cardRect.Width * (1f - density) * 0.08f);
        float insetY = Math.Max(0f, cardRect.Height * (1f - density) * 0.07f);
        cardRect.Inflate(-insetX, -insetY);

        SKColor fillTop = node.Kind switch
        {
            GraphNodeKind.Popup => PopupFillTopColor,
            GraphNodeKind.External => ExternalFillTopColor,
            _ => NodeFillTopColor
        };
        SKColor fillBottom = node.Kind switch
        {
            GraphNodeKind.Popup => PopupFillBottomColor,
            GraphNodeKind.External => ExternalFillBottomColor,
            _ => NodeFillBottomColor
        };
        if (isSelected)
        {
            using SKPaint selectedLiftShadowPaint = new()
            {
                IsAntialias = true,
                Color = NodeShadowColor.WithAlpha((byte)Math.Clamp(46 * alpha, 0f, 255f)),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, SelectedNodeShadowBlur),
                Style = SKPaintStyle.Fill
            };
            SKRect selectedLiftShadowRect = cardRect;
            selectedLiftShadowRect.Offset(0f, SelectedNodeShadowOffset);
            selectedLiftShadowRect.Inflate(SelectedNodeShadowExpansion, SelectedNodeShadowExpansion);
            canvas.DrawRoundRect(selectedLiftShadowRect, NodeCornerRadius + 2f, NodeCornerRadius + 2f, selectedLiftShadowPaint);

            using SKPaint selectedAmbientShadowPaint = new()
            {
                IsAntialias = true,
                Color = NodeShadowColor.WithAlpha((byte)Math.Clamp(24 * alpha, 0f, 255f)),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, SelectedNodeAmbientShadowBlur),
                Style = SKPaintStyle.Fill
            };
            SKRect selectedAmbientShadowRect = cardRect;
            selectedAmbientShadowRect.Offset(0f, SelectedNodeAmbientShadowOffset);
            selectedAmbientShadowRect.Inflate(SelectedNodeAmbientShadowExpansion, SelectedNodeAmbientShadowExpansion);
            canvas.DrawRoundRect(selectedAmbientShadowRect, NodeCornerRadius + 1.5f, NodeCornerRadius + 1.5f, selectedAmbientShadowPaint);

            using SKPaint glowPaint = new()
            {
                IsAntialias = true,
                Color = WithAlpha(SelectedNodeGlowColor, alpha),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4.8f),
                StrokeWidth = 1.6f,
                Style = SKPaintStyle.Stroke
            };
            SKRect glowRect = cardRect;
            glowRect.Inflate(1.4f, 1.4f);
            canvas.DrawRoundRect(glowRect, NodeCornerRadius + 1.5f, NodeCornerRadius + 1.5f, glowPaint);
        }
        else
        {
            using SKPaint normalShadowPaint = new()
            {
                IsAntialias = true,
                Color = NodeShadowColor.WithAlpha((byte)Math.Clamp(16 * alpha, 0f, 255f)),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, NormalNodeShadowBlur),
                Style = SKPaintStyle.Fill
            };
            SKRect normalShadowRect = cardRect;
            normalShadowRect.Offset(0f, NormalNodeShadowOffset);
            normalShadowRect.Inflate(NormalNodeShadowExpansion, NormalNodeShadowExpansion);
            canvas.DrawRoundRect(normalShadowRect, NodeCornerRadius + 1.2f, NodeCornerRadius + 1.2f, normalShadowPaint);
        }

        using SKPaint fillPaint = new()
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(cardRect.Left, cardRect.Top),
                new SKPoint(cardRect.Left, cardRect.Bottom),
                [WithAlpha(fillTop, alpha), WithAlpha(fillBottom, alpha)],
                null,
                SKShaderTileMode.Clamp),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(cardRect, NodeCornerRadius, NodeCornerRadius, fillPaint);

        using SKPaint strokePaint = new()
        {
            IsAntialias = true,
            Color = WithAlpha(isSelected ? SelectedNodeBorderColor : NodeBorderColor, alpha),
            StrokeWidth = isSelected ? 1.65f : 1.05f,
            Style = SKPaintStyle.Stroke
        };
        SKRect strokeRect = cardRect;
        strokeRect.Inflate(-strokePaint.StrokeWidth / 2f, -strokePaint.StrokeWidth / 2f);
        canvas.DrawRoundRect(strokeRect, NodeCornerRadius, NodeCornerRadius, strokePaint);

        string? typeLabel = CreateNodeTypeLabel(node);
        bool hasTypeLabel = !string.IsNullOrWhiteSpace(typeLabel) && cardRect.Height >= 56f;
        float titleSize = Math.Clamp(cardRect.Height * 0.19f, 12f, 17f);
        float metaSize = Math.Clamp(cardRect.Height * 0.135f, 10f, 12.5f);
        float typeSize = Math.Clamp(cardRect.Height * 0.095f, 8.2f, 10.2f);
        float left = cardRect.Left + 22f;
        float rightPadding = 16f;
        float titleY = hasTypeLabel
            ? cardRect.Top + Math.Max(32f, cardRect.Height * 0.43f)
            : cardRect.Top + Math.Max(24f, cardRect.Height * 0.38f);
        float typeY = Math.Max(cardRect.Top + 17f, titleY - Math.Max(18f, cardRect.Height * 0.22f));
        float metaY = Math.Min(cardRect.Bottom - 13f, titleY + Math.Max(17f, cardRect.Height * 0.24f));
        float textWidth = Math.Max(36f, cardRect.Width - left + cardRect.Left - rightPadding);

        using SkiaTextStyle typePaint = CreateTextPaint(WithAlpha(GetNodeTypeLabelColor(node, isSelected), alpha), typeSize, isBold: true);
        using SkiaTextStyle titlePaint = CreateTextPaint(WithAlpha(TextColor, alpha), titleSize, isBold: true);
        using SkiaTextStyle metaPaint = CreateTextPaint(WithAlpha(MutedTextColor, alpha), metaSize, isBold: false);

        if (hasTypeLabel)
        {
            DrawTextClipped(canvas, typeLabel!, left, typeY, textWidth, typePaint);
        }

        DrawTextClipped(canvas, node.Title, left, titleY, textWidth, titlePaint);
        DrawTextClipped(canvas, CreateNodeSubtitle(node), left, metaY, textWidth, metaPaint);
    }

    private void DrawArrowHead(SKCanvas canvas, IReadOnlyList<GraphPoint> points, SKColor color, bool isSelected)
    {
        GraphPoint tipPoint = points[^1];
        GraphPoint previousPoint = points.Count > 1 ? points[^2] : new GraphPoint(tipPoint.X - 1d, tipPoint.Y);
        double deltaX = tipPoint.X - previousPoint.X;
        double deltaY = tipPoint.Y - previousPoint.Y;
        double length = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        if (length < 0.001d)
        {
            return;
        }

        double unitX = deltaX / length;
        double unitY = deltaY / length;
        float arrowLength = isSelected ? 14f : 12f;
        float arrowHalfWidth = isSelected ? 6f : 5f;
        SKPoint tip = ToPoint(tipPoint);
        SKPoint baseCenter = new(
            tip.X - ((float)unitX * arrowLength),
            tip.Y - ((float)unitY * arrowLength));
        SKPoint left = new(
            baseCenter.X - ((float)unitY * arrowHalfWidth),
            baseCenter.Y + ((float)unitX * arrowHalfWidth));
        SKPoint right = new(
            baseCenter.X + ((float)unitY * arrowHalfWidth),
            baseCenter.Y - ((float)unitX * arrowHalfWidth));

        using SKPath path = new();
        path.MoveTo(tip);
        path.LineTo(left);
        path.LineTo(right);
        path.Close();

        using SKPaint paint = new()
        {
            IsAntialias = true,
            Color = color,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawPath(path, paint);
    }

    private void DrawEdgeLabel(SKCanvas canvas, GraphLink link, IReadOnlyList<GraphPoint> points, SKColor color)
    {
        string? label = link.Label;
        if (string.IsNullOrWhiteSpace(label) || Zoom < 0.32d)
        {
            return;
        }

        GraphPoint middle = points[points.Count / 2];
        using SkiaTextStyle textPaint = CreateTextPaint(color, 10f / (float)Math.Sqrt(Math.Max(Zoom, 0.6d)), isBold: true);
        float textWidth = textPaint.MeasureText(label);
        const float horizontalPadding = 10f;
        SKRect badgeRect = new(
            (float)middle.X - (textWidth / 2f) - horizontalPadding,
            (float)middle.Y - 19f,
            (float)middle.X + (textWidth / 2f) + horizontalPadding,
            (float)middle.Y - 2f);

        using SKPaint fillPaint = new()
        {
            IsAntialias = true,
            Color = new SKColor(255, 255, 255, color.Alpha),
            Style = SKPaintStyle.Fill
        };
        using SKPaint strokePaint = new()
        {
            IsAntialias = true,
            Color = new SKColor(198, 213, 226, (byte)Math.Clamp(color.Alpha * 0.82f, 0f, 255f)),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0.9f / (float)Math.Sqrt(Math.Max(Zoom, 0.65d))
        };
        canvas.DrawRoundRect(badgeRect, 7f, 7f, fillPaint);
        canvas.DrawRoundRect(badgeRect, 7f, 7f, strokePaint);
        DrawTextVerticallyCentered(canvas, label, badgeRect.Left + horizontalPadding, badgeRect, textPaint);
    }

    private static string? CreateNodeTypeLabel(GraphNode node)
    {
        return node.Kind switch
        {
            GraphNodeKind.Popup => "POPUP",
            GraphNodeKind.External => "EXTERNAL",
            GraphNodeKind.Reference => "REFERENCE",
            GraphNodeKind.GroupProxy => "GROUP",
            _ => null
        };
    }

    private static SKColor GetNodeTypeLabelColor(GraphNode node, bool isSelected)
    {
        if (isSelected)
        {
            return SelectionColor;
        }

        return node.Kind switch
        {
            GraphNodeKind.Popup => PopupEdgeColor,
            GraphNodeKind.External => ExternalEdgeColor,
            GraphNodeKind.Reference => EdgeColor,
            GraphNodeKind.GroupProxy => MutedTextColor,
            _ => MutedTextColor
        };
    }

    private static SKPath CreateEdgePath(IReadOnlyList<GraphPoint> points)
    {
        SKPath path = new();
        path.MoveTo(ToPoint(points[0]));

        if (points.Count < 4)
        {
            for (int index = 1; index < points.Count; index++)
            {
                path.LineTo(ToPoint(points[index]));
            }

            return path;
        }

        for (int index = 1; index < points.Count; index++)
        {
            path.LineTo(ToPoint(points[index]));
        }

        return path;
    }

    private IReadOnlyList<GraphPoint> ResolveEdgePoints(GraphLayoutEdge edge, GraphLink link, GraphSurfaceModel model)
    {
        GraphPoint[] points;
        if (edge.Points.Count >= 2)
        {
            points = edge.Points.ToArray();
        }
        else
        {
            if (!model.NodeLayoutsById.TryGetValue(link.SourceNodeId, out GraphLayoutNode? source)
                || !model.NodeLayoutsById.TryGetValue(link.TargetNodeId, out GraphLayoutNode? target))
            {
                return Array.Empty<GraphPoint>();
            }

            points = [GetRectCenter(source.Bounds), GetRectCenter(target.Bounds)];
        }

        return ClipEdgePointsToNodeBounds(points, link, model);
    }

    private static IReadOnlyList<GraphPoint> ClipEdgePointsToNodeBounds(GraphPoint[] points, GraphLink link, GraphSurfaceModel model)
    {
        if (points.Length < 2 || StringComparer.Ordinal.Equals(link.SourceNodeId, link.TargetNodeId))
        {
            return points;
        }

        if (model.NodeLayoutsById.TryGetValue(link.SourceNodeId, out GraphLayoutNode? source))
        {
            points[0] = ClipCenterRayToRect(source.Bounds, points[1]);
        }

        if (model.NodeLayoutsById.TryGetValue(link.TargetNodeId, out GraphLayoutNode? target))
        {
            points[^1] = ClipCenterRayToRect(target.Bounds, points[^2]);
        }

        return points;
    }

    private GraphSurfaceHit? HitTestGraph(Point screenPoint)
    {
        GraphDocument? document = Document;
        GraphLayoutResult? layout = LayoutResult;
        if (document is null || layout is null || !layout.Succeeded)
        {
            return null;
        }

        GraphPoint worldPoint = ScreenToWorld(screenPoint);

        foreach (GraphLayoutNode node in layout.Nodes.Reverse())
        {
            if (Contains(node.Bounds, worldPoint))
            {
                return new GraphSurfaceHit(GraphSurfaceHitKind.Node, node.NodeId);
            }
        }

        Dictionary<string, GraphLink> linksById = document.Links.ToDictionary(link => link.Id, StringComparer.Ordinal);
        Dictionary<string, GraphLayoutNode> nodeLayoutsById = layout.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        double tolerance = EdgeHitTolerance / Math.Max(Zoom, 0.25d);
        foreach (GraphLayoutEdge edge in layout.Edges.Reverse())
        {
            if (!linksById.TryGetValue(edge.LinkId, out GraphLink? link))
            {
                continue;
            }

            IReadOnlyList<GraphPoint> points = edge.Points.Count >= 2
                ? edge.Points
                : ResolveFallbackHitPoints(link, nodeLayoutsById);
            if (points.Count < 2)
            {
                continue;
            }

            if (IsNearPolyline(worldPoint, points, tolerance))
            {
                return new GraphSurfaceHit(GraphSurfaceHitKind.Link, edge.LinkId);
            }
        }

        foreach (GraphLayoutGroup group in layout.Groups.Reverse())
        {
            if (Contains(group.Bounds, worldPoint))
            {
                return new GraphSurfaceHit(GraphSurfaceHitKind.Group, group.GroupId);
            }
        }

        return null;
    }

    private static IReadOnlyList<GraphPoint> ResolveFallbackHitPoints(GraphLink link, IReadOnlyDictionary<string, GraphLayoutNode> nodeLayoutsById)
    {
        if (!nodeLayoutsById.TryGetValue(link.SourceNodeId, out GraphLayoutNode? source)
            || !nodeLayoutsById.TryGetValue(link.TargetNodeId, out GraphLayoutNode? target))
        {
            return Array.Empty<GraphPoint>();
        }

        return [GetRectCenter(source.Bounds), GetRectCenter(target.Bounds)];
    }

    private void ApplySelection(GraphSurfaceHit? hit, bool isMultiSelection, bool open)
    {
        if (hit is null)
        {
            SetCurrentValue(SelectedNodeIdsProperty, Array.Empty<string>());
            SetCurrentValue(SelectedLinkIdsProperty, Array.Empty<string>());
            SetCurrentValue(SelectedGroupIdsProperty, Array.Empty<string>());
            ExecuteGraphRequest(GraphRequest.ClearSelection());
            return;
        }

        switch (hit.Kind)
        {
            case GraphSurfaceHitKind.Node:
                SetCurrentValue(SelectedNodeIdsProperty, UpdateSelection(SelectedNodeIds, hit.Id, isMultiSelection));
                SetCurrentValue(SelectedLinkIdsProperty, Array.Empty<string>());
                SetCurrentValue(SelectedGroupIdsProperty, Array.Empty<string>());
                ExecuteGraphRequest(open ? GraphRequest.OpenNode(hit.Id) : GraphRequest.SelectNode(hit.Id, isMultiSelection));
                break;
            case GraphSurfaceHitKind.Link:
                SetCurrentValue(SelectedNodeIdsProperty, Array.Empty<string>());
                SetCurrentValue(SelectedLinkIdsProperty, UpdateSelection(SelectedLinkIds, hit.Id, isMultiSelection));
                SetCurrentValue(SelectedGroupIdsProperty, Array.Empty<string>());
                ExecuteGraphRequest(open ? GraphRequest.OpenLink(hit.Id) : GraphRequest.SelectLink(hit.Id, isMultiSelection));
                break;
            case GraphSurfaceHitKind.Group:
                SetCurrentValue(SelectedNodeIdsProperty, Array.Empty<string>());
                SetCurrentValue(SelectedLinkIdsProperty, Array.Empty<string>());
                SetCurrentValue(SelectedGroupIdsProperty, UpdateSelection(SelectedGroupIds, hit.Id, isMultiSelection));
                ExecuteGraphRequest(open ? GraphRequest.OpenGroup(hit.Id) : GraphRequest.SelectGroup(hit.Id, isMultiSelection));
                break;
        }
    }

    private GraphPoint ScreenToWorld(Point screenPoint)
    {
        double safeZoom = Math.Max(Zoom, 0.0001d);
        return new GraphPoint(
            (screenPoint.X - PanX) / safeZoom,
            (screenPoint.Y - PanY) / safeZoom);
    }

    private void BeginPan(Point mousePoint)
    {
        Focus();
        isPanning = true;
        lastPanPoint = mousePoint;
        previousCursor = Cursor;
        Cursor = Cursors.SizeAll;
        CaptureMouse();
    }

    private void EndPan()
    {
        if (!isPanning)
        {
            return;
        }

        isPanning = false;
        Cursor = previousCursor;
        previousCursor = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void UpdateLayoutBounds(GraphLayoutResult? layoutResult)
    {
        GraphRect bounds = ResolveLayoutBounds(layoutResult);
        SetValue(LayoutBoundsPropertyKey, bounds);
    }

    private static GraphRect ResolveLayoutBounds(GraphLayoutResult? layoutResult)
    {
        if (layoutResult?.GraphBounds is { } graphBounds && graphBounds.Width > 0d && graphBounds.Height > 0d)
        {
            return graphBounds;
        }

        if (layoutResult is null || (layoutResult.Nodes.Count == 0 && layoutResult.Groups.Count == 0))
        {
            return new GraphRect(0d, 0d, 0d, 0d);
        }

        IEnumerable<GraphRect> rectangles = layoutResult.Nodes.Select(node => node.Bounds)
            .Concat(layoutResult.Groups.Select(group => group.Bounds));
        double left = rectangles.Min(rectangle => rectangle.X);
        double top = rectangles.Min(rectangle => rectangle.Y);
        double right = rectangles.Max(rectangle => rectangle.X + rectangle.Width);
        double bottom = rectangles.Max(rectangle => rectangle.Y + rectangle.Height);
        return new GraphRect(left, top, Math.Max(0d, right - left), Math.Max(0d, bottom - top));
    }

    private void ExecuteGraphRequest(GraphRequest request)
    {
        ICommand? command = GraphRequestCommand;
        if (command?.CanExecute(request) == true)
        {
            command.Execute(request);
        }
    }

    private void InvalidateGraph()
    {
        InvalidateVisual();
    }

    private static void OnLayoutResultChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        SkiaGraphSurface surface = (SkiaGraphSurface)dependencyObject;
        surface.UpdateLayoutBounds((GraphLayoutResult?)eventArgs.NewValue);
        surface.InvalidateGraph();
    }

    private static void OnDocumentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        ((SkiaGraphSurface)dependencyObject).InvalidateGraph();
    }

    private static void OnViewportPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        ((SkiaGraphSurface)dependencyObject).InvalidateGraph();
    }

    private static void OnZoomLimitChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        SkiaGraphSurface surface = (SkiaGraphSurface)dependencyObject;
        surface.CoerceValue(ZoomProperty);
        surface.InvalidateGraph();
    }

    private static void OnSelectionPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        ((SkiaGraphSurface)dependencyObject).InvalidateGraph();
    }

    private static void OnVisualStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        ((SkiaGraphSurface)dependencyObject).InvalidateGraph();
    }

    private static void OnFocusPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        ((SkiaGraphSurface)dependencyObject).InvalidateGraph();
    }

    private void OnSurfaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidateGraph();
    }

    private static object CoerceZoom(DependencyObject dependencyObject, object baseValue)
    {
        SkiaGraphSurface surface = (SkiaGraphSurface)dependencyObject;
        return surface.CoerceZoomValue((double)baseValue);
    }

    private double CoerceZoomValue(double zoom)
    {
        return Math.Clamp(zoom, MinZoom, MaxZoom);
    }

    private static object CoerceVisualDensity(DependencyObject dependencyObject, object baseValue)
    {
        return Math.Clamp((double)baseValue, MinVisualDensity, MaxVisualDensity);
    }

    private static bool IsFiniteDouble(object value)
    {
        return value is double number && double.IsFinite(number);
    }

    private static bool IsFinitePositiveDouble(object value)
    {
        return value is double number && double.IsFinite(number) && number > 0d;
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

    private static IReadOnlyList<string> UpdateSelection(IReadOnlyList<string> currentSelection, string id, bool isMultiSelection)
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

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static SkiaTextStyle CreateTextPaint(SKColor color, float size, bool isBold)
    {
        return new SkiaTextStyle(TextTypeface, size, color, isBold);
    }

    private static void DrawTextClipped(SKCanvas canvas, string text, float x, float baselineY, float maxWidth, SkiaTextStyle textStyle)
    {
        string clippedText = ClipTextToWidth(text, maxWidth, textStyle);
        textStyle.DrawText(canvas, clippedText, x, baselineY);
    }

    private static void DrawTextVerticallyCentered(SKCanvas canvas, string text, float x, SKRect bounds, SkiaTextStyle textStyle)
    {
        SKFontMetrics metrics = textStyle.Font.Metrics;
        float baselineY = bounds.MidY - ((metrics.Ascent + metrics.Descent) / 2f);
        textStyle.DrawText(canvas, text, x, baselineY);
    }

    private static string ClipTextToWidth(string text, float maxWidth, SkiaTextStyle textStyle)
    {
        if (string.IsNullOrEmpty(text) || textStyle.MeasureText(text) <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "…";
        int length = text.Length;
        while (length > 1)
        {
            string candidate = text[..length] + ellipsis;
            if (textStyle.MeasureText(candidate) <= maxWidth)
            {
                return candidate;
            }

            length--;
        }

        return ellipsis;
    }

    private static SKColor WithAlpha(SKColor color, float alpha)
    {
        byte scaledAlpha = (byte)Math.Clamp(color.Alpha * alpha, 0f, 255f);
        return color.WithAlpha(scaledAlpha);
    }

    private static SKRect ToRect(GraphRect rect)
    {
        return new SKRect(
            (float)rect.X,
            (float)rect.Y,
            (float)(rect.X + rect.Width),
            (float)(rect.Y + rect.Height));
    }

    private static SKPoint ToPoint(GraphPoint point)
    {
        return new SKPoint((float)point.X, (float)point.Y);
    }

    private static GraphPoint ClipCenterRayToRect(GraphRect bounds, GraphPoint towardPoint)
    {
        GraphPoint center = GetRectCenter(bounds);
        double deltaX = towardPoint.X - center.X;
        double deltaY = towardPoint.Y - center.Y;

        if (Math.Abs(deltaX) < 0.0001d && Math.Abs(deltaY) < 0.0001d)
        {
            return center;
        }

        double halfWidth = bounds.Width / 2d;
        double halfHeight = bounds.Height / 2d;
        double scaleX = Math.Abs(deltaX) < 0.0001d ? double.PositiveInfinity : halfWidth / Math.Abs(deltaX);
        double scaleY = Math.Abs(deltaY) < 0.0001d ? double.PositiveInfinity : halfHeight / Math.Abs(deltaY);
        double scale = Math.Min(scaleX, scaleY);

        if (!double.IsFinite(scale) || scale <= 0d)
        {
            return center;
        }

        return new GraphPoint(center.X + (deltaX * scale), center.Y + (deltaY * scale));
    }

    private static GraphPoint GetRectCenter(GraphRect bounds)
    {
        return new GraphPoint(bounds.X + (bounds.Width / 2d), bounds.Y + (bounds.Height / 2d));
    }

    private static bool Contains(GraphRect bounds, GraphPoint point)
    {
        return point.X >= bounds.X
            && point.X <= bounds.X + bounds.Width
            && point.Y >= bounds.Y
            && point.Y <= bounds.Y + bounds.Height;
    }

    private static bool IsNearPolyline(GraphPoint point, IReadOnlyList<GraphPoint> points, double tolerance)
    {
        for (int index = 1; index < points.Count; index++)
        {
            if (DistanceToSegment(point, points[index - 1], points[index]) <= tolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static double DistanceToSegment(GraphPoint point, GraphPoint start, GraphPoint end)
    {
        double deltaX = end.X - start.X;
        double deltaY = end.Y - start.Y;
        double lengthSquared = (deltaX * deltaX) + (deltaY * deltaY);
        if (lengthSquared < 0.0001d)
        {
            return Math.Sqrt(Math.Pow(point.X - start.X, 2d) + Math.Pow(point.Y - start.Y, 2d));
        }

        double t = (((point.X - start.X) * deltaX) + ((point.Y - start.Y) * deltaY)) / lengthSquared;
        t = Math.Clamp(t, 0d, 1d);
        double projectionX = start.X + (t * deltaX);
        double projectionY = start.Y + (t * deltaY);
        return Math.Sqrt(Math.Pow(point.X - projectionX, 2d) + Math.Pow(point.Y - projectionY, 2d));
    }

    private static SKPathEffect? CreatePathEffect(GraphLink link, GraphLayoutEdge edge)
    {
        if (edge.UsesFallbackGeometry || link.LineStyle == GraphLineStyle.Dashed || link.Kind is GraphLinkKind.Back or GraphLinkKind.Cancel or GraphLinkKind.PopupOpen or GraphLinkKind.PopupClose)
        {
            return SKPathEffect.CreateDash([12f, 7f], 0f);
        }

        if (link.LineStyle == GraphLineStyle.Dotted || link.Kind is GraphLinkKind.External or GraphLinkKind.Reference or GraphLinkKind.Diagnostic)
        {
            return SKPathEffect.CreateDash([2.5f, 7f], 0f);
        }

        return null;
    }

    private static SKColor GetEdgeColor(GraphLink link)
    {
        return link.Kind switch
        {
            GraphLinkKind.Primary => PrimaryEdgeColor,
            GraphLinkKind.Back or GraphLinkKind.Cancel => BackEdgeColor,
            GraphLinkKind.PopupOpen or GraphLinkKind.PopupClose => PopupEdgeColor,
            GraphLinkKind.External or GraphLinkKind.Reference => ExternalEdgeColor,
            GraphLinkKind.Diagnostic => DiagnosticEdgeColor,
            _ => EdgeColor
        };
    }

    private static string CreateNodeSubtitle(GraphNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Description))
        {
            return node.Description;
        }

        if (node.GroupMemberships.Count > 0)
        {
            return string.Join(" · ", node.GroupMemberships.Take(3));
        }

        return node.Id;
    }

    private enum GraphSurfaceHitKind
    {
        Node,
        Link,
        Group,
    }

    private sealed record GraphSurfaceHit(GraphSurfaceHitKind Kind, string Id);

    private sealed class GraphSurfaceModel
    {
        private GraphSurfaceModel(GraphDocument document, GraphLayoutResult layout)
        {
            Document = document;
            Layout = layout;
            NodesById = document.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
            LinksById = document.Links.ToDictionary(link => link.Id, StringComparer.Ordinal);
            GroupsById = document.Groups.ToDictionary(group => group.Id, StringComparer.Ordinal);
            NodeLayoutsById = layout.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        }

        public GraphDocument Document { get; }

        public GraphLayoutResult Layout { get; }

        public IReadOnlyDictionary<string, GraphNode> NodesById { get; }

        public IReadOnlyDictionary<string, GraphLink> LinksById { get; }

        public IReadOnlyDictionary<string, GraphGroup> GroupsById { get; }

        public IReadOnlyDictionary<string, GraphLayoutNode> NodeLayoutsById { get; }

        public static GraphSurfaceModel Create(GraphDocument document, GraphLayoutResult layout)
        {
            return new GraphSurfaceModel(document, layout);
        }
    }

    private sealed class GraphFocusSet
    {
        private readonly bool isActive;
        private readonly HashSet<string> nodeIds;
        private readonly HashSet<string> linkIds;
        private readonly HashSet<string> groupIds;

        private GraphFocusSet(bool isActive, HashSet<string> nodeIds, HashSet<string> linkIds, HashSet<string> groupIds)
        {
            this.isActive = isActive;
            this.nodeIds = nodeIds;
            this.linkIds = linkIds;
            this.groupIds = groupIds;
        }

        public static GraphFocusSet Create(
            GraphDocument document,
            IReadOnlyList<string> selectedNodeIds,
            IReadOnlyList<string> selectedLinkIds,
            string? focusedNodeId,
            string? focusedLinkId,
            string? focusedGroupId,
            GraphViewMode viewMode)
        {
            HashSet<string> nodeIds = new(selectedNodeIds.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            HashSet<string> linkIds = new(selectedLinkIds.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            HashSet<string> groupIds = new(StringComparer.Ordinal);

            if (!string.IsNullOrWhiteSpace(focusedNodeId))
            {
                nodeIds.Add(focusedNodeId);
            }

            if (!string.IsNullOrWhiteSpace(focusedLinkId))
            {
                linkIds.Add(focusedLinkId);
            }

            if (!string.IsNullOrWhiteSpace(focusedGroupId))
            {
                groupIds.Add(focusedGroupId);
                foreach (GraphNode node in document.Nodes.Where(node => node.GroupMemberships.Contains(focusedGroupId, StringComparer.Ordinal)))
                {
                    nodeIds.Add(node.Id);
                }
            }

            foreach (GraphLink link in document.Links)
            {
                if (linkIds.Contains(link.Id) || nodeIds.Contains(link.SourceNodeId) || nodeIds.Contains(link.TargetNodeId))
                {
                    linkIds.Add(link.Id);
                    nodeIds.Add(link.SourceNodeId);
                    nodeIds.Add(link.TargetNodeId);
                }
            }

            foreach (GraphNode node in document.Nodes.Where(node => nodeIds.Contains(node.Id)))
            {
                foreach (string groupId in node.GroupMemberships)
                {
                    groupIds.Add(groupId);
                }
            }

            bool isActive = nodeIds.Any(nodeId => document.Nodes.Any(node => StringComparer.Ordinal.Equals(node.Id, nodeId)))
                || linkIds.Any(linkId => document.Links.Any(link => StringComparer.Ordinal.Equals(link.Id, linkId)))
                || groupIds.Any(groupId => document.Groups.Any(group => StringComparer.Ordinal.Equals(group.Id, groupId)));

            return new GraphFocusSet(isActive, nodeIds, linkIds, groupIds);
        }

        public bool IsNodeRelevant(string nodeId)
        {
            return !isActive || nodeIds.Contains(nodeId);
        }

        public bool IsLinkRelevant(string linkId, string sourceNodeId, string targetNodeId)
        {
            return !isActive || linkIds.Contains(linkId) || nodeIds.Contains(sourceNodeId) || nodeIds.Contains(targetNodeId);
        }

        public bool IsGroupRelevant(string groupId)
        {
            return !isActive || groupIds.Contains(groupId);
        }

        public float GetAlpha(bool isRelevant)
        {
            return 1f;
        }
    }
}

