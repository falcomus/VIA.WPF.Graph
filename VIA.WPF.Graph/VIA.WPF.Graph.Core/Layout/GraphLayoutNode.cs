namespace VIA.WPF.Graph.Core.Layout;

/// <summary>
/// Represents the layout bounds for one graph node.
/// </summary>
public sealed record GraphLayoutNode
{
    public GraphLayoutNode(string nodeId, GraphRect bounds)
    {
        NodeId = RequireText(nodeId, nameof(nodeId));
        Bounds = bounds;
    }

    public string NodeId { get; }

    public GraphRect Bounds { get; }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null, empty or whitespace.", parameterName);
        }

        return value;
    }
}
