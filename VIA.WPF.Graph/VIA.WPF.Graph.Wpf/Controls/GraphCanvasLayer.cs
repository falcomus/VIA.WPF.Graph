using System.Windows.Media;

namespace VIA.WPF.Graph.Wpf.Controls;

/// <summary>
/// Owns one drawing visual used by <see cref="GraphCanvas" /> for a dedicated rendering layer.
/// </summary>
internal sealed class GraphCanvasLayer
{
    public DrawingVisual Visual { get; } = new();

    public void Render(Action<DrawingContext> draw)
    {
        ArgumentNullException.ThrowIfNull(draw);

        using DrawingContext drawingContext = Visual.RenderOpen();
        draw(drawingContext);
    }

    public void Clear()
    {
        using DrawingContext drawingContext = Visual.RenderOpen();
    }
}
