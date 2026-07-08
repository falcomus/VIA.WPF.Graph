namespace VIA.WPF.Graph.Core.Layout;

/// <summary>
/// Represents a neutral point in device-independent layout units.
/// </summary>
public readonly record struct GraphPoint
{
    public GraphPoint(double x, double y)
    {
        if (!double.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "X must be a finite value.");
        }

        if (!double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Y must be a finite value.");
        }

        X = x;
        Y = y;
    }

    public double X { get; }

    public double Y { get; }
}
