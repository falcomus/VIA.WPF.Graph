using Rubjerg.Graphviz;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Core.Model;

namespace VIA.WPF.Graph.Graphviz.Layout;

/// <summary>
/// Creates neutral layout results from neutral graph documents by using Rubjerg.Graphviz.
/// This adapter does not depend on WPF and does not mutate host models.
/// </summary>
public static class GraphvizLayoutEngine
{
    private static readonly ConcurrentDictionary<string, GraphLayoutResult> LayoutCache = new(StringComparer.Ordinal);

    public static GraphLayoutResult Layout(
        GraphDocument document,
        GraphLayoutOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        GraphLayoutOptions resolvedOptions = options ?? GraphLayoutOptions.Default;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            string cacheKey = CreateCacheKey(document, resolvedOptions, cancellationToken);
            if (LayoutCache.TryGetValue(cacheKey, out GraphLayoutResult? cachedResult))
            {
                return cachedResult;
            }

            GraphLayoutResult result = CreateLayout(document, resolvedOptions, cancellationToken);
            if (result.Succeeded)
            {
                LayoutCache.TryAdd(cacheKey, result);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CreateCanceledResult(document, resolvedOptions);
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

    private static GraphLayoutResult CreateLayout(
        GraphDocument document,
        GraphLayoutOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RootGraph source = RootGraph.CreateNew(GraphType.Directed, "via_wpf_graph_layout");
        source.SetAttribute("rankdir", ToGraphvizRankDirection(options.Direction));
        source.SetAttribute("splines", ToGraphvizSplines(options.EdgeRoutingStyle));
        source.SetAttribute("compound", "true");

        Dictionary<string, Node> sourceNodes = [];
        foreach (GraphNode node in document.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sourceNodes.Add(node.Id, CreateSourceNode(source, node));
        }

        Dictionary<string, GraphGroup> containerGroupsById = document.Groups
            .Where(group => group.Kind == GraphGroupKind.Container)
            .ToDictionary(group => group.Id, StringComparer.Ordinal);

        Dictionary<string, string> clusterIdByGroupId = CreateClusterIds(containerGroupsById.Values);
        Dictionary<string, SubGraph> sourceClustersByGroupId = [];

        if (options.UseContainerGroupsAsClusters)
        {
            foreach (GraphGroup group in containerGroupsById.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SubGraph cluster = source.GetOrAddSubgraph(clusterIdByGroupId[group.Id]);
                cluster.SetAttribute("label", group.Title);
                sourceClustersByGroupId[group.Id] = cluster;
            }

            AssignNodesToContainerClusters(document.Nodes, sourceNodes, containerGroupsById, sourceClustersByGroupId, cancellationToken);
        }

        foreach (GraphLink link in document.Links)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

        cancellationToken.ThrowIfCancellationRequested();
        RootGraph layout = source.CreateLayout(coordinateSystem: CoordinateSystem.TopLeft);
        cancellationToken.ThrowIfCancellationRequested();

        List<GraphLayoutNode> nodes = [];
        foreach (GraphNode node in document.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            nodes.Add(CreateLayoutNode(layout, node));
        }

        List<GraphLayoutGroup> groups = [];
        if (options.UseContainerGroupsAsClusters)
        {
            foreach (GraphGroup group in containerGroupsById.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GraphLayoutGroup? layoutGroup = CreateLayoutGroupOrNull(layout, group, clusterIdByGroupId[group.Id]);
                if (layoutGroup is not null)
                {
                    groups.Add(layoutGroup);
                }
            }
        }

        List<GraphLayoutEdge> edges = [];
        foreach (GraphLink link in document.Links)
        {
            cancellationToken.ThrowIfCancellationRequested();
            edges.Add(CreateLayoutEdge(layout, link));
        }

        return new GraphLayoutResult(
            document.Id,
            options,
            GraphvizGeometryMapper.ToGraphRect(layout.GetBoundingBox()),
            nodes,
            groups,
            edges);
    }

    private static GraphLayoutResult CreateCanceledResult(GraphDocument document, GraphLayoutOptions options)
    {
        return new GraphLayoutResult(
            document.Id,
            options,
            error: new GraphLayoutError(
                "Graphviz layout was canceled.",
                "The layout request was canceled before a complete layout result was available.",
                typeof(OperationCanceledException).FullName));
    }

    private static Node CreateSourceNode(RootGraph source, GraphNode graphNode)
    {
        Node node = source.GetOrAddNode(graphNode.Id);
        node.SetAttribute("shape", "box");
        node.SetAttribute("label", graphNode.Title);
        node.SetAttribute("width", GraphvizGeometryMapper.ToGraphvizInches(graphNode.DefaultSize.Width));
        node.SetAttribute("height", GraphvizGeometryMapper.ToGraphvizInches(graphNode.DefaultSize.Height));
        node.SetAttribute("fixedsize", "true");
        return node;
    }

    private static void AssignNodesToContainerClusters(
        IEnumerable<GraphNode> graphNodes,
        IReadOnlyDictionary<string, Node> sourceNodes,
        IReadOnlyDictionary<string, GraphGroup> containerGroupsById,
        IReadOnlyDictionary<string, SubGraph> sourceClustersByGroupId,
        CancellationToken cancellationToken)
    {
        foreach (GraphNode graphNode in graphNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

        return new GraphLayoutNode(graphNode.Id, GraphvizGeometryMapper.ToGraphRect(layoutNode.GetBoundingBox()));
    }

    private static GraphLayoutGroup? CreateLayoutGroupOrNull(RootGraph layout, GraphGroup group, string clusterId)
    {
        SubGraph? layoutCluster = layout.GetSubgraph(clusterId);
        return layoutCluster is null
            ? null
            : new GraphLayoutGroup(group.Id, GraphvizGeometryMapper.ToGraphRect(layoutCluster.GetBoundingBox()));
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
            : new GraphLayoutEdge(link.Id, spline.Select(GraphvizGeometryMapper.ToGraphPoint));
    }

    private static GraphLayoutEdge CreateFallbackEdge(GraphLink link, Node sourceNode, Node targetNode)
    {
        GraphRect sourceBounds = GraphvizGeometryMapper.ToGraphRect(sourceNode.GetBoundingBox());
        GraphRect targetBounds = GraphvizGeometryMapper.ToGraphRect(targetNode.GetBoundingBox());

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

    private static string CreateCacheKey(
        GraphDocument document,
        GraphLayoutOptions options,
        CancellationToken cancellationToken)
    {
        StringBuilder builder = new();

        AppendText(builder, document.Id);
        AppendEnum(builder, options.Direction);
        AppendEnum(builder, options.EdgeRoutingStyle);
        AppendBoolean(builder, options.UseContainerGroupsAsClusters);

        AppendCount(builder, document.Nodes.Count);
        foreach (GraphNode node in document.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendText(builder, node.Id);
            AppendText(builder, node.Title);
            AppendEnum(builder, node.Kind);
            AppendDouble(builder, node.DefaultSize.Width);
            AppendDouble(builder, node.DefaultSize.Height);
            AppendText(builder, node.VisualStyleKey);
            AppendTextList(builder, node.GroupMemberships);
        }

        AppendCount(builder, document.Groups.Count);
        foreach (GraphGroup group in document.Groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendText(builder, group.Id);
            AppendText(builder, group.Title);
            AppendEnum(builder, group.Kind);
            AppendText(builder, group.ParentGroupId);
            AppendText(builder, group.VisualStyleKey);
        }

        AppendCount(builder, document.Links.Count);
        foreach (GraphLink link in document.Links)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendText(builder, link.Id);
            AppendText(builder, link.SourceNodeId);
            AppendText(builder, link.TargetNodeId);
            AppendEnum(builder, link.Direction);
            AppendEnum(builder, link.Kind);
            AppendText(builder, link.Label);
            AppendEnum(builder, link.LineStyle);
            AppendDouble(builder, link.Weight);
            AppendBoolean(builder, link.IsLayoutConstraint);
        }

        return builder.ToString();
    }

    private static void AppendText(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("-1:");
            return;
        }

        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
        builder.Append('|');
    }

    private static void AppendTextList(StringBuilder builder, IReadOnlyList<string> values)
    {
        AppendCount(builder, values.Count);
        foreach (string value in values)
        {
            AppendText(builder, value);
        }
    }

    private static void AppendEnum<TEnum>(StringBuilder builder, TEnum value)
        where TEnum : struct, Enum
    {
        builder.Append(Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
        builder.Append('|');
    }

    private static void AppendDouble(StringBuilder builder, double value)
    {
        builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        builder.Append('|');
    }

    private static void AppendBoolean(StringBuilder builder, bool value)
    {
        builder.Append(value ? '1' : '0');
        builder.Append('|');
    }

    private static void AppendCount(StringBuilder builder, int count)
    {
        builder.Append(count.ToString(CultureInfo.InvariantCulture));
        builder.Append('|');
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
}
