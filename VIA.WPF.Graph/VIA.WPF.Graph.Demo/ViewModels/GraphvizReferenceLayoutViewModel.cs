using System.Windows;
using System.Windows.Media;
using VIA.WPF.Graph.Graphviz.Verification;

namespace VIA.WPF.Graph.Demo.ViewModels;

/// <summary>
/// WPF-only projection of the temporary P0-003 reference geometry.
/// It belongs to the demo and is not the future reusable GraphCanvas.
/// </summary>
public sealed class GraphvizReferenceLayoutViewModel
{
    private const double CanvasPadding = 32d;

    private GraphvizReferenceLayoutViewModel(GraphvizReferenceLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        Direction = layout.Direction;
        CanvasWidth = Math.Ceiling(layout.GraphBounds.Width + (CanvasPadding * 2));
        CanvasHeight = Math.Ceiling(layout.GraphBounds.Height + (CanvasPadding * 2));

        Clusters = layout.Clusters
            .Select(cluster => new GraphvizReferenceClusterViewModel(
                cluster.Title,
                OffsetRectangle(layout.GraphBounds, cluster.Bounds)))
            .ToArray();

        Edges = layout.Edges
            .Select(edge => new GraphvizReferenceEdgeViewModel(
                edge.Id,
                edge.IsBackEdge,
                edge.Points.Select(point => OffsetPoint(layout.GraphBounds, point)).ToArray()))
            .ToArray();

        Nodes = layout.Nodes
            .Select(node => new GraphvizReferenceNodeViewModel(
                node.Id,
                node.Title,
                OffsetRectangle(layout.GraphBounds, node.Bounds)))
            .ToArray();
    }

    public string Direction { get; }

    public double CanvasWidth { get; }

    public double CanvasHeight { get; }

    public IReadOnlyList<GraphvizReferenceClusterViewModel> Clusters { get; }

    public IReadOnlyList<GraphvizReferenceEdgeViewModel> Edges { get; }

    public IReadOnlyList<GraphvizReferenceNodeViewModel> Nodes { get; }

    public static GraphvizReferenceLayoutViewModel Create(GraphvizReferenceLayout layout)
    {
        return new GraphvizReferenceLayoutViewModel(layout);
    }

    private static GraphvizReferenceRectangleViewModel OffsetRectangle(
        GraphvizReferenceRectangle graphBounds,
        GraphvizReferenceRectangle bounds)
    {
        return new GraphvizReferenceRectangleViewModel(
            bounds.X - graphBounds.X + CanvasPadding,
            bounds.Y - graphBounds.Y + CanvasPadding,
            bounds.Width,
            bounds.Height);
    }

    private static Point OffsetPoint(GraphvizReferenceRectangle graphBounds, GraphvizReferencePoint point)
    {
        return new Point(
            point.X - graphBounds.X + CanvasPadding,
            point.Y - graphBounds.Y + CanvasPadding);
    }
}

public sealed class GraphvizReferenceClusterViewModel
{
    public GraphvizReferenceClusterViewModel(string title, GraphvizReferenceRectangleViewModel bounds)
    {
        Title = title;
        X = bounds.X;
        Y = bounds.Y;
        Width = bounds.Width;
        Height = bounds.Height;
    }

    public string Title { get; }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }
}

public sealed class GraphvizReferenceNodeViewModel
{
    public GraphvizReferenceNodeViewModel(string id, string title, GraphvizReferenceRectangleViewModel bounds)
    {
        Id = id;
        Title = title;
        X = bounds.X;
        Y = bounds.Y;
        Width = bounds.Width;
        Height = bounds.Height;
        Fill = id == "help_popup" ? Brushes.Lavender : Brushes.White;
        BorderBrush = id == "help_popup" ? Brushes.MediumPurple : Brushes.SlateGray;
    }

    public string Id { get; }

    public string Title { get; }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public Brush Fill { get; }

    public Brush BorderBrush { get; }
}

public sealed class GraphvizReferenceEdgeViewModel
{
    public GraphvizReferenceEdgeViewModel(string id, bool isBackEdge, IReadOnlyList<Point> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
        {
            throw new ArgumentException("The technical reference edge must contain spline points.", nameof(points));
        }

        Id = id;
        IsBackEdge = isBackEdge;
        Stroke = isBackEdge ? Brushes.IndianRed : Brushes.SlateGray;
        StrokeDashArray = isBackEdge ? new DoubleCollection([4d, 3d]) : null;
        Points = new PointCollection(points);
        ArrowHeadPoints = CreateArrowHead(points);
    }

    public string Id { get; }

    public bool IsBackEdge { get; }

    public Brush Stroke { get; }

    public DoubleCollection? StrokeDashArray { get; }

    public PointCollection Points { get; }

    public PointCollection ArrowHeadPoints { get; }

    private static PointCollection CreateArrowHead(IReadOnlyList<Point> points)
    {
        Point tip = points[^1];
        Point previous = points.Count > 1 ? points[^2] : new Point(tip.X - 1d, tip.Y);

        double deltaX = tip.X - previous.X;
        double deltaY = tip.Y - previous.Y;
        double length = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));

        if (length < 0.001d)
        {
            return new PointCollection([tip]);
        }

        double unitX = deltaX / length;
        double unitY = deltaY / length;
        const double arrowLength = 10d;
        const double arrowHalfWidth = 4.5d;

        Point baseCenter = new(
            tip.X - (unitX * arrowLength),
            tip.Y - (unitY * arrowLength));
        Point left = new(
            baseCenter.X - (unitY * arrowHalfWidth),
            baseCenter.Y + (unitX * arrowHalfWidth));
        Point right = new(
            baseCenter.X + (unitY * arrowHalfWidth),
            baseCenter.Y - (unitX * arrowHalfWidth));

        return new PointCollection([tip, left, right]);
    }
}

public sealed record GraphvizReferenceRectangleViewModel(double X, double Y, double Width, double Height);
