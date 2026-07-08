using System.Globalization;
using Rubjerg.Graphviz;
using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Core.Model;

namespace VIA.WPF.Graph.Graphviz.Layout;

/// <summary>
/// Creates neutral layout results from neutral graph documents by using Rubjerg.Graphviz.
/// This adapter does not depend on WPF and does not mutate host models.
/// </summary>
public static class GraphvizLayoutEngine
{
    public static GraphLayoutResult Layout(GraphDocument document, GraphLayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        GraphLayoutOptions resolvedOptions = options ?? GraphLayoutOptions.Default;

        try
        {
            return CreateLayout(document, resolvedOptions);
        }
        catch (Exception exception)
        {
            return new GraphLayoutResult(
                document.Id,
                resolvedOptions,
                error: new GraphLayoutError(
                    "Graphviz layout failed.",
                    exception.ToString(),
                    exception.GetType().FullName));
        }
    }

    private static GraphLayoutResult CreateLayout(GraphDocument document, GraphLayoutOptions options)
    {
        RootGraph source = RootGraph.CreateNew(GraphType.Directed, "via_wpf_graph_layout");
        source.SetAttribute("rankdir", ToGraphvizRankDirection(options.Direction));
        source.SetAttribute("splines", ToGraphvizSplines(options.EdgeRoutingStyle));
        source.SetAttribute("compound", "true");

        Dictionary<string, Node> sourceNodes = document.Nodes.ToDictionary(
            node => node.Id,
            node => CreateSourceNode(source, node),
            StringComparer.Ordinal);

        Dictionary<string, GraphGroup> containerGroupsById = document.Groups
            .Where(group => group.Kind == GraphGroupKind.Container)
            .ToDictionary(group => group.Id, StringComparer.Ordinal);

        Dictionary<string, string> clusterIdByGroupId = CreateClusterIds(containerGroupsById.Values);
        Dictionary<string, SubGraph> sourceClustersByGroupId = [];

        if (options.UseContainerGroupsAsClusters)
        {
            foreach (GraphGroup group in containerGroupsById.Values)
            {
                SubGraph cluster = source.GetOrAddSubgraph(clusterIdByGroupId[group.Id]);
                cluster.SetAttribute("label", group.Title);
                sourceClustersByGroupId[group.Id] = cluster;
            }

            AssignNodesToContainerClusters(document.Nodes, sourceNodes, containerGroupsById, sourceClustersByGroupId);
        }

        foreach (GraphLink link in document.Links)
        {
            if (!sourceNodes.TryGetValue(link.SourceNodeId, out Node? sourceNode)
                || !sourceNodes.TryGetValue(link.TargetNodeId, out Node? targetNode))
            {
                return new GraphLayoutResult(
                    document.Id,
                    options,
                    error: new GraphLayoutError(
                        $"Link '{link.Id}' references a missing source or target node.",
                        $"Source='{link.SourceNodeId}', Target='{link.TargetNodeId}'"));
            }

            Edge edge = source.GetOrAddEdge(sourceNode, targetNode, link.Id);
            ApplyEdgeAttributes(edge, link);
        }

        RootGraph layout = source.CreateLayout(coordinateSystem: CoordinateSystem.TopLeft);

        IReadOnlyList<GraphLayoutNode> nodes = document.Nodes
            .Select(node => CreateLayoutNode(layout, node))
            .ToArray();

        IReadOnlyList<GraphLayoutGroup> groups = options.UseContainerGroupsAsClusters
            ? containerGroupsById.Values
                .Select(group => CreateLayoutGroupOrNull(layout, group, clusterIdByGroupId[group.Id]))
                .Where(group => group is not null)
                .Cast<GraphLayoutGroup>()
                .ToArray()
            : Array.Empty<GraphLayoutGroup>();

        IReadOnlyList<GraphLayoutEdge> edges = document.Links
            .Select(link => CreateLayoutEdge(layout, link))
            .ToArray();

        return new GraphLayoutResult(
            document.Id,
            options,
            ToGraphRect(layout.GetBoundingBox()),
            nodes,
            groups,
            edges);
    }

    private static Node CreateSourceNode(RootGraph source, GraphNode graphNode)
    {
        Node node = source.GetOrAddNode(graphNode.Id);
        node.SetAttribute("shape", "box");
        node.SetAttribute("label", graphNode.Title);
        node.SetAttribute("width", ToGraphvizInches(graphNode.DefaultSize.Width));
        node.SetAttribute("height", ToGraphvizInches(graphNode.DefaultSize.Height));
        node.SetAttribute("fixedsize", "true");
        return node;
    }

    private static void AssignNodesToContainerClusters(
        IEnumerable<GraphNode> graphNodes,
        IReadOnlyDictionary<string, Node> sourceNodes,
        IReadOnlyDictionary<string, GraphGroup> containerGroupsById,
        IReadOnlyDictionary<string, SubGraph> sourceClustersByGroupId)
    {
        foreach (GraphNode graphNode in graphNodes)
        {
            string? containerGroupId = graphNode.GroupMemberships
                .FirstOrDefault(containerGroupsById.ContainsKey);

            if (containerGroupId is null)
            {
                continue;
            }

            sourceClustersByGroupId[containerGroupId].AddExisting(sourceNodes[graphNode.Id]);
        }
    }

    private static void ApplyEdgeAttributes(Edge edge, GraphLink link)
    {
        if (!string.IsNullOrWhiteSpace(link.Label))
        {
            edge.SetAttribute("label", link.Label);
        }

        edge.SetAttribute("dir", ToGraphvizDirection(link.Direction));
        edge.SetAttribute("style", ToGraphvizLineStyle(link.LineStyle));

        bool isLayoutConstraint = IsLayoutConstraint(link);
        edge.SetAttribute("constraint", isLayoutConstraint ? "true" : "false");
        edge.SetAttribute("weight", ToGraphvizWeight(link, isLayoutConstraint));
    }

    private static bool IsLayoutConstraint(GraphLink link)
    {
        if (!link.IsLayoutConstraint)
        {
            return false;
        }

        return link.Kind switch
        {
            GraphLinkKind.Back => false,
            GraphLinkKind.Reference => false,
            GraphLinkKind.Diagnostic => false,
            _ => true,
        };
    }

    private static string ToGraphvizWeight(GraphLink link, bool isLayoutConstraint)
    {
        double effectiveWeight = isLayoutConstraint
            ? GetLayoutWeight(link)
            : 0.1d;

        return effectiveWeight.ToString(CultureInfo.InvariantCulture);
    }

    private static double GetLayoutWeight(GraphLink link)
    {
        return link.Kind switch
        {
            GraphLinkKind.Primary => Math.Max(link.Weight, 3d),
            GraphLinkKind.Secondary => Math.Max(link.Weight, 1d),
            GraphLinkKind.PopupOpen => Math.Max(link.Weight, 0.5d),
            GraphLinkKind.PopupClose => Math.Max(link.Weight, 0.25d),
            GraphLinkKind.Cancel => Math.Max(link.Weight, 0.75d),
            GraphLinkKind.External => Math.Max(link.Weight, 0.5d),
            _ => link.Weight,
        };
    }

    private static GraphLayoutNode CreateLayoutNode(RootGraph layout, GraphNode graphNode)
    {
        Node layoutNode = layout.GetNode(graphNode.Id)
            ?? throw new InvalidOperationException($"Graphviz layout does not contain node '{graphNode.Id}'.");

        return new GraphLayoutNode(graphNode.Id, ToGraphRect(layoutNode.GetBoundingBox()));
    }

    private static GraphLayoutGroup? CreateLayoutGroupOrNull(RootGraph layout, GraphGroup group, string clusterId)
    {
        SubGraph? layoutCluster = layout.GetSubgraph(clusterId);
        return layoutCluster is null
            ? null
            : new GraphLayoutGroup(group.Id, ToGraphRect(layoutCluster.GetBoundingBox()));
    }

    private static GraphLayoutEdge CreateLayoutEdge(RootGraph layout, GraphLink link)
    {
        Node sourceNode = layout.GetNode(link.SourceNodeId)
            ?? throw new InvalidOperationException($"Graphviz layout does not contain source node '{link.SourceNodeId}'.");
        Node targetNode = layout.GetNode(link.TargetNodeId)
            ?? throw new InvalidOperationException($"Graphviz layout does not contain target node '{link.TargetNodeId}'.");

        Edge? layoutEdge = layout.GetEdge(sourceNode, targetNode, link.Id);
        if (layoutEdge is null)
        {
            return CreateFallbackEdge(link, sourceNode, targetNode);
        }

        PointD[] spline = layoutEdge.GetFirstSpline();
        return spline.Length == 0
            ? CreateFallbackEdge(link, sourceNode, targetNode)
            : new GraphLayoutEdge(link.Id, spline.Select(ToGraphPoint));
    }

    private static GraphLayoutEdge CreateFallbackEdge(GraphLink link, Node sourceNode, Node targetNode)
    {
        GraphRect sourceBounds = ToGraphRect(sourceNode.GetBoundingBox());
        GraphRect targetBounds = ToGraphRect(targetNode.GetBoundingBox());

        return new GraphLayoutEdge(
            link.Id,
            [GetCenter(sourceBounds), GetCenter(targetBounds)],
            usesFallbackGeometry: true);
    }

    private static GraphPoint GetCenter(GraphRect bounds)
    {
        return new GraphPoint(
            bounds.X + (bounds.Width / 2d),
            bounds.Y + (bounds.Height / 2d));
    }

    private static Dictionary<string, string> CreateClusterIds(IEnumerable<GraphGroup> groups)
    {
        Dictionary<string, string> clusterIds = new(StringComparer.Ordinal);
        HashSet<string> usedClusterIds = new(StringComparer.Ordinal);

        foreach (GraphGroup group in groups)
        {
            string baseClusterId = "cluster_" + CreateSafeIdentifier(group.Id);
            string clusterId = baseClusterId;
            int suffix = 2;

            while (!usedClusterIds.Add(clusterId))
            {
                clusterId = $"{baseClusterId}_{suffix}";
                suffix++;
            }

            clusterIds[group.Id] = clusterId;
        }

        return clusterIds;
    }

    private static string CreateSafeIdentifier(string value)
    {
        char[] characters = value
            .Select(character => char.IsLetterOrDigit(character) || character == '_' ? character : '_')
            .ToArray();

        string identifier = new(characters);
        return string.IsNullOrWhiteSpace(identifier) ? "group" : identifier;
    }

    private static string ToGraphvizRankDirection(GraphLayoutDirection direction)
    {
        return direction switch
        {
            GraphLayoutDirection.LeftToRight => "LR",
            _ => "TB",
        };
    }

    private static string ToGraphvizSplines(GraphEdgeRoutingStyle routingStyle)
    {
        return routingStyle switch
        {
            GraphEdgeRoutingStyle.Polyline => "polyline",
            GraphEdgeRoutingStyle.Orthogonal => "ortho",
            _ => "true",
        };
    }

    private static string ToGraphvizDirection(GraphLinkDirection direction)
    {
        return direction switch
        {
            GraphLinkDirection.Undirected => "none",
            _ => "forward",
        };
    }

    private static string ToGraphvizLineStyle(GraphLineStyle lineStyle)
    {
        return lineStyle switch
        {
            GraphLineStyle.Dashed => "dashed",
            GraphLineStyle.Dotted => "dotted",
            _ => "solid",
        };
    }

    private static string ToGraphvizInches(double layoutUnits)
    {
        const double graphvizPointsPerInch = 72d;
        return (layoutUnits / graphvizPointsPerInch).ToString(CultureInfo.InvariantCulture);
    }

    private static GraphPoint ToGraphPoint(PointD point)
    {
        return new GraphPoint(point.X, point.Y);
    }

    private static GraphRect ToGraphRect(RectangleD rectangle)
    {
        return new GraphRect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }
}
