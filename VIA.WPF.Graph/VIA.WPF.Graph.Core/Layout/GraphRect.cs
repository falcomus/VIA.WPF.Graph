namespace VIA.WPF.Graph.Core.Layout;

/// <summary>
/// Represents neutral rectangle bounds in device-independent layout units.
/// </summary>
public readonly record struct GraphRect
{
    public GraphRect(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), "X must be a finite value.");
        }

        if (!double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Y must be a finite value.");
        }

        if (!double.IsFinite(width) || width < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be a finite non-negative value.");
        }

        if (!double.IsFinite(height) || height < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be a finite non-negative value.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }
}
