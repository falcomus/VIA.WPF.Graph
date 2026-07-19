using Rubjerg.Graphviz;
using System.Globalization;
using VIA.WPF.Graph.Core.Layout;

namespace VIA.WPF.Graph.Graphviz.Layout;

/// <summary>
/// Centralizes Graphviz geometry conversion into neutral layout units.
/// </summary>
internal static class GraphvizGeometryMapper
{
    private const double GraphvizPointsPerInch = 72d;

    public static string ToGraphvizInches(double layoutUnits)
    {
        if (!double.IsFinite(layoutUnits) || layoutUnits <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(layoutUnits), "Layout units must be finite and positive.");
        }

        return (layoutUnits / GraphvizPointsPerInch).ToString(CultureInfo.InvariantCulture);
    }

    public static GraphPoint ToGraphPoint(PointD point)
    {
        return new GraphPoint(point.X, point.Y);
    }

    public static GraphRect ToGraphRect(RectangleD rectangle)
    {
        return new GraphRect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }
}
