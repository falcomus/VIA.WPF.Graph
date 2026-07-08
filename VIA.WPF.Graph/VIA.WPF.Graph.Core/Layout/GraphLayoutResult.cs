namespace VIA.WPF.Graph.Core.Layout;

/// <summary>
/// Represents a neutral layout result or a controlled layout failure.
/// </summary>
public sealed record GraphLayoutResult
{
    public GraphLayoutResult(
        string documentId,
        GraphLayoutOptions? options = null,
        GraphRect? graphBounds = null,
        IEnumerable<GraphLayoutNode>? nodes = null,
        IEnumerable<GraphLayoutGroup>? groups = null,
        IEnumerable<GraphLayoutEdge>? edges = null,
        GraphLayoutError? error = null)
    {
        DocumentId = RequireText(documentId, nameof(documentId));
        Options = options ?? GraphLayoutOptions.Default;
        GraphBounds = graphBounds;
        Nodes = CopyItems(nodes);
        Groups = CopyItems(groups);
        Edges = CopyItems(edges);
        Error = error;
    }

    public string DocumentId { get; }

    public GraphLayoutOptions Options { get; }

    public GraphRect? GraphBounds { get; }

    public IReadOnlyList<GraphLayoutNode> Nodes { get; }

    public IReadOnlyList<GraphLayoutGroup> Groups { get; }

    public IReadOnlyList<GraphLayoutEdge> Edges { get; }

    public GraphLayoutError? Error { get; }

    public bool Succeeded => Error is null;

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
