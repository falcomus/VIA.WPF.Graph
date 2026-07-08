using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VIA.WPF.Graph.Core.Layout;

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

    private const double DefaultTextSize = 12d;
    private const double NodeCornerRadius = 5d;
    private const double ArrowLength = 10d;
    private const double ArrowHalfWidth = 4.5d;
    private const double ZoomWheelFactor = 1.1d;
    private const double DefaultFitPadding = 32d;

    private static readonly Brush BackgroundBrush = CreateFrozenBrush(Color.FromRgb(248, 248, 248));
    private static readonly Brush GroupFillBrush = CreateFrozenBrush(Color.FromArgb(20, 96, 125, 139));
    private static readonly Brush NodeFillBrush = Brushes.White;
    private static readonly Brush TextBrush = Brushes.Black;
    private static readonly Pen GroupPen = CreateFrozenPen(Color.FromRgb(96, 125, 139), 1.25d, DashStyles.Dash);
    private static readonly Pen EdgePen = CreateFrozenPen(Color.FromRgb(84, 96, 108), 1.5d, DashStyles.Solid);
    private static readonly Pen FallbackEdgePen = CreateFrozenPen(Color.FromRgb(120, 120, 120), 1.5d, DashStyles.Dash);
    private static readonly Pen NodePen = CreateFrozenPen(Color.FromRgb(84, 96, 108), 1.25d, DashStyles.Solid);
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

        if (e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Right)
        {
            return;
        }

        Focus();
        CaptureMouse();
        isPanning = true;
        lastPanPoint = e.GetPosition(this);
        previousCursor = Cursor;
        Cursor = Cursors.SizeAll;
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

    private static void OnLayoutResultChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        GraphLayoutResult? layoutResult = (GraphLayoutResult?)eventArgs.NewValue;

        canvas.UpdateLayoutBounds(layoutResult);
        canvas.RenderLayers(layoutResult);
        canvas.UpdateLayerTransform();
        canvas.InvalidateMeasure();
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

    private static object CoerceZoom(DependencyObject dependencyObject, object baseValue)
    {
        GraphCanvas canvas = (GraphCanvas)dependencyObject;
        double value = (double)baseValue;
        double min = Math.Min(canvas.MinZoom, canvas.MaxZoom);
        double max = Math.Max(canvas.MinZoom, canvas.MaxZoom);
        return Math.Clamp(value, min, max);
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

        groupLayer.Render(drawingContext => DrawGroups(drawingContext, layoutResult.Groups));
        edgeLayer.Render(drawingContext => DrawEdges(drawingContext, layoutResult.Edges));
        nodeLayer.Render(drawingContext => DrawNodes(drawingContext, layoutResult.Nodes));
    }

    private void DrawGroups(DrawingContext drawingContext, IReadOnlyList<GraphLayoutGroup> groups)
    {
        foreach (GraphLayoutGroup group in groups)
        {
            Rect rect = ToRect(group.Bounds);
            drawingContext.DrawRectangle(GroupFillBrush, GroupPen, rect);
            DrawText(drawingContext, group.GroupId, rect, TextBrush, TextAlignment.Left, 8d, 4d);
        }
    }

    private void DrawEdges(DrawingContext drawingContext, IReadOnlyList<GraphLayoutEdge> edges)
    {
        foreach (GraphLayoutEdge edge in edges)
        {
            if (edge.Points.Count < 2)
            {
                continue;
            }

            Pen pen = edge.UsesFallbackGeometry ? FallbackEdgePen : EdgePen;
            StreamGeometry lineGeometry = CreatePolylineGeometry(edge.Points);
            drawingContext.DrawGeometry(null, pen, lineGeometry);

            StreamGeometry? arrowGeometry = CreateArrowHeadGeometry(edge.Points);
            if (arrowGeometry is not null)
            {
                drawingContext.DrawGeometry(pen.Brush, null, arrowGeometry);
            }
        }
    }

    private void DrawNodes(DrawingContext drawingContext, IReadOnlyList<GraphLayoutNode> nodes)
    {
        foreach (GraphLayoutNode node in nodes)
        {
            Rect rect = ToRect(node.Bounds);
            drawingContext.DrawRoundedRectangle(NodeFillBrush, NodePen, rect, NodeCornerRadius, NodeCornerRadius);
            DrawText(drawingContext, node.NodeId, rect, TextBrush, TextAlignment.Center, 6d, 0d);
        }
    }

    private void DrawText(
        DrawingContext drawingContext,
        string text,
        Rect bounds,
        Brush brush,
        TextAlignment textAlignment,
        double horizontalPadding,
        double verticalPadding)
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
            DefaultTextSize,
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

    private static Rect ToRect(GraphRect rect)
    {
        return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
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
}
