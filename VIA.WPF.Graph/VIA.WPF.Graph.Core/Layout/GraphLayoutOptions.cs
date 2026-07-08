namespace VIA.WPF.Graph.Core.Layout;

/// <summary>
/// Describes neutral layout options supplied to a layout adapter.
/// </summary>
public sealed record GraphLayoutOptions
{
    public GraphLayoutOptions(
        GraphLayoutDirection direction = GraphLayoutDirection.TopToBottom,
        GraphEdgeRoutingStyle edgeRoutingStyle = GraphEdgeRoutingStyle.Spline,
        bool useContainerGroupsAsClusters = true)
    {
        Direction = direction;
        EdgeRoutingStyle = edgeRoutingStyle;
        UseContainerGroupsAsClusters = useContainerGroupsAsClusters;
    }

    public GraphLayoutDirection Direction { get; }

    public GraphEdgeRoutingStyle EdgeRoutingStyle { get; }

    public bool UseContainerGroupsAsClusters { get; }

    public static GraphLayoutOptions Default { get; } = new();
}
