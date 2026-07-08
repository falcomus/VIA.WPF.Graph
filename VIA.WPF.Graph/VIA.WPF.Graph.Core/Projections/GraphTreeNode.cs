using VIA.WPF.Graph.Core.Model;

namespace VIA.WPF.Graph.Core.Projections;

/// <summary>
/// Represents one neutral item in a cycle-safe graph tree projection.
/// </summary>
public sealed record GraphTreeNode
{
    public GraphTreeNode(
        string treeNodeId,
        string nodeId,
        string title,
        GraphTreeNodeKind kind = GraphTreeNodeKind.Branch,
        string? linkId = null,
        GraphLinkKind? linkKind = null,
        IEnumerable<GraphTreeNode>? children = null)
    {
        TreeNodeId = RequireText(treeNodeId, nameof(treeNodeId));
        NodeId = RequireText(nodeId, nameof(nodeId));
        Title = RequireText(title, nameof(title));
        Kind = kind;
        LinkId = linkId;
        LinkKind = linkKind;
        Children = CopyItems(children);
    }

    public string TreeNodeId { get; }

    public string NodeId { get; }

    public string Title { get; }

    public GraphTreeNodeKind Kind { get; }

    public string? LinkId { get; }

    public GraphLinkKind? LinkKind { get; }

    public IReadOnlyList<GraphTreeNode> Children { get; }

    public bool HasChildren => Children.Count > 0;

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
