namespace VIA.WPF.Graph.Core.Layout;

/// <summary>
/// Represents the neutral geometry for one graph link.
/// </summary>
public sealed record GraphLayoutEdge
{
    public GraphLayoutEdge(
        string linkId,
        IEnumerable<GraphPoint>? points = null,
        bool usesFallbackGeometry = false)
    {
        LinkId = RequireText(linkId, nameof(linkId));
        Points = CopyItems(points);
        UsesFallbackGeometry = usesFallbackGeometry;
    }

    public string LinkId { get; }

    public IReadOnlyList<GraphPoint> Points { get; }

    public bool UsesFallbackGeometry { get; }

    public bool HasGeometry => Points.Count > 0;

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null, empty or whitespace.", parameterName);
        }

        return value;
    }

    private static IReadOnlyList<T> CopyItems<T>(IEnumerable<T>? items)
    {
        return items?.ToArray() ?? Array.Empty<T>();
    }
}
