namespace VIA.WPF.Graph.Core.Model;

/// <summary>
/// Represents neutral viewport coordinates owned by a host view model.
/// </summary>
public readonly record struct GraphViewportState
{
    public GraphViewportState(double zoom, double panX, double panY)
    {
        if (!double.IsFinite(zoom) || zoom <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom), "Zoom must be a finite positive value.");
        }

        if (!double.IsFinite(panX))
        {
            throw new ArgumentOutOfRangeException(nameof(panX), "Pan X must be a finite value.");
        }

        if (!double.IsFinite(panY))
        {
            throw new ArgumentOutOfRangeException(nameof(panY), "Pan Y must be a finite value.");
        }

        Zoom = zoom;
        PanX = panX;
        PanY = panY;
    }

    public double Zoom { get; }

    public double PanX { get; }

    public double PanY { get; }

    public static GraphViewportState Default { get; } = new(1d, 0d, 0d);
}
