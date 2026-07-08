namespace VIA.WPF.Graph.Core.Projections;

/// <summary>
/// Represents a neutral tree projection built from a graph document.
/// </summary>
public sealed record GraphTreeProjection
{
    public GraphTreeProjection(IEnumerable<GraphTreeNode>? roots = null)
    {
        Roots = CopyItems(roots);
    }

    public IReadOnlyList<GraphTreeNode> Roots { get; }

    public bool HasRoots => Roots.Count > 0;

    private static IReadOnlyList<T> CopyItems<T>(IEnumerable<T>? items)
    {
        return items?.ToArray() ?? Array.Empty<T>();
    }
}
