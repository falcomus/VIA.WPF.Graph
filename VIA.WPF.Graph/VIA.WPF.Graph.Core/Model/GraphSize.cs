namespace VIA.WPF.Graph.Core.Model;

/// <summary>
/// Represents a neutral width and height in WPF device-independent pixels.
/// </summary>
public readonly record struct GraphSize
{
    public GraphSize(double width, double height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
    }

    public double Width { get; }

    public double Height { get; }

    public static GraphSize Compact { get; } = new(120, 48);

    public static GraphSize Standard { get; } = new(180, 72);

    public static GraphSize Detail { get; } = new(240, 120);

    public static GraphSize Popup { get; } = new(160, 56);

    public static GraphSize Stub { get; } = new(140, 48);

    public static GraphSize GroupProxy { get; } = new(200, 80);
}
