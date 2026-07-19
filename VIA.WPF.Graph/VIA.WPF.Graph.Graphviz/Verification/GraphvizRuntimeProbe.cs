using Rubjerg.Graphviz;
using System.Reflection;
using System.Runtime.InteropServices;

namespace VIA.WPF.Graph.Graphviz.Verification;

/// <summary>
/// Executes the P0-002 runtime verification and provides the P0-003 technical reference geometry.
/// This code is a temporary technical probe and not the later layout adapter.
/// </summary>
public static class GraphvizRuntimeProbe
{
    private const string ExpectedPackageVersion = "3.0.5";

    public static GraphvizRuntimeProbeResult Run()
    {
        GraphvizMinimalGraphReferenceResult reference = GraphvizMinimalGraphReference.Create();

        Assembly packageAssembly = typeof(RootGraph).Assembly;
        string packageAssemblyVersion = packageAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? packageAssembly.GetName().Version?.ToString()
            ?? "unknown";

        return new GraphvizRuntimeProbeResult(
            ExpectedPackageVersion,
            packageAssemblyVersion,
            RuntimeInformation.ProcessArchitecture.ToString(),
            RuntimeInformation.RuntimeIdentifier,
            reference.TopToBottom,
            reference.LeftToRight);
    }
}

/// <summary>
/// Creates the fixed P0-003 graph and exposes only neutral numeric layout data to the demo.
/// </summary>
public static class GraphvizMinimalGraphReference
{
    private static readonly NodeDefinition[] NodeDefinitions =
    [
        new("start", "Start"),
        new("registration", "Registration"),
        new("main", "Main"),
        new("help_popup", "Help popup"),
        new("confirmation", "Confirmation"),
        new("finish", "Finish")
    ];

    private static readonly EdgeDefinition[] EdgeDefinitions =
    [
        new("start_registration", "start", "registration", false),
        new("start_main", "start", "main", false),
        new("main_help_popup", "main", "help_popup", false),
        new("help_popup_confirmation", "help_popup", "confirmation", false),
        new("confirmation_finish", "confirmation", "finish", false),
        new("confirmation_start_back", "confirmation", "start", true)
    ];

    private static readonly ClusterDefinition[] ClusterDefinitions =
    [
        new("cluster_entry", "Entry", ["start", "registration"]),
        new("cluster_work", "Work", ["main", "help_popup", "confirmation", "finish"])
    ];

    public static GraphvizMinimalGraphReferenceResult Create()
    {
        return new GraphvizMinimalGraphReferenceResult(
            CreateLayout("TB"),
            CreateLayout("LR"));
    }

    private static GraphvizReferenceLayout CreateLayout(string direction)
    {
        RootGraph source = CreateSourceGraph();
        source.SetAttribute("rankdir", direction);

        RootGraph layout = source.CreateLayout(coordinateSystem: CoordinateSystem.TopLeft);

        IReadOnlyList<GraphvizReferenceNode> nodes = NodeDefinitions
            .Select(definition =>
            {
                Node layoutNode = layout.GetNode(definition.Id)
                    ?? throw new InvalidOperationException($"The {direction} layout does not contain node '{definition.Id}'.");

                return new GraphvizReferenceNode(
                    definition.Id,
                    definition.Title,
                    ToReferenceRectangle(layoutNode.GetBoundingBox()));
            })
            .ToArray();

        IReadOnlyList<GraphvizReferenceCluster> clusters = ClusterDefinitions
            .Select(definition =>
            {
                SubGraph layoutCluster = layout.GetSubgraph(definition.Id)
                    ?? throw new InvalidOperationException($"The {direction} layout does not contain cluster '{definition.Id}'.");

                return new GraphvizReferenceCluster(
                    definition.Id,
                    definition.Title,
                    ToReferenceRectangle(layoutCluster.GetBoundingBox()));
            })
            .ToArray();

        IReadOnlyList<GraphvizReferenceEdge> edges = EdgeDefinitions
            .Select(definition =>
            {
                Node sourceNode = layout.GetNode(definition.SourceNodeId)
                    ?? throw new InvalidOperationException($"The {direction} layout does not contain source node '{definition.SourceNodeId}'.");
                Node targetNode = layout.GetNode(definition.TargetNodeId)
                    ?? throw new InvalidOperationException($"The {direction} layout does not contain target node '{definition.TargetNodeId}'.");
                Edge layoutEdge = layout.GetEdge(sourceNode, targetNode, definition.Id)
                    ?? throw new InvalidOperationException($"The {direction} layout does not contain edge '{definition.Id}'.");

                PointD[] spline = layoutEdge.GetFirstSpline();
                if (spline.Length == 0)
                {
                    throw new InvalidOperationException($"The {direction} layout has no spline geometry for edge '{definition.Id}'.");
                }

                return new GraphvizReferenceEdge(
                    definition.Id,
                    definition.IsBackEdge,
                    spline.Select(ToReferencePoint).ToArray());
            })
            .ToArray();

        return new GraphvizReferenceLayout(
            direction,
            ToReferenceRectangle(layout.GetBoundingBox()),
            clusters,
            nodes,
            edges);
    }

    private static RootGraph CreateSourceGraph()
    {
        RootGraph source = RootGraph.CreateNew(GraphType.Directed, "via_wpf_graph_p0_003");

        Dictionary<string, Node> nodes = NodeDefinitions.ToDictionary(
            definition => definition.Id,
            definition => source.GetOrAddNode(definition.Id),
            StringComparer.Ordinal);

        foreach (EdgeDefinition edgeDefinition in EdgeDefinitions)
        {
            _ = source.GetOrAddEdge(
                nodes[edgeDefinition.SourceNodeId],
                nodes[edgeDefinition.TargetNodeId],
                edgeDefinition.Id);
        }

        foreach (ClusterDefinition clusterDefinition in ClusterDefinitions)
        {
            SubGraph cluster = source.GetOrAddSubgraph(clusterDefinition.Id);
            foreach (string nodeId in clusterDefinition.NodeIds)
            {
                cluster.AddExisting(nodes[nodeId]);
            }
        }

        return source;
    }

    private static GraphvizReferencePoint ToReferencePoint(PointD point)
    {
        return new GraphvizReferencePoint(point.X, point.Y);
    }

    private static GraphvizReferenceRectangle ToReferenceRectangle(RectangleD rectangle)
    {
        return new GraphvizReferenceRectangle(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }

    private sealed record NodeDefinition(string Id, string Title);

    private sealed record EdgeDefinition(string Id, string SourceNodeId, string TargetNodeId, bool IsBackEdge);

    private sealed record ClusterDefinition(string Id, string Title, IReadOnlyList<string> NodeIds);
}

public sealed record GraphvizRuntimeProbeResult(
    string ExpectedPackageVersion,
    string PackageAssemblyVersion,
    string ProcessArchitecture,
    string RuntimeIdentifier,
    GraphvizReferenceLayout TopToBottom,
    GraphvizReferenceLayout LeftToRight)
{
    public string ToDisplayText()
    {
        List<string> lines =
        [
            "P0-002 PASSED",
            string.Empty,
            $"Expected Rubjerg.Graphviz package version: {ExpectedPackageVersion}",
            $"Rubjerg.Graphviz assembly version: {PackageAssemblyVersion}",
            $"Process architecture: {ProcessArchitecture}",
            $"Runtime identifier: {RuntimeIdentifier}",
            string.Empty,
            "Verified Graphviz operations:",
            "- 6 nodes created and laid out",
            "- 6 named directed edges created, including one back edge",
            "- 2 Graphviz clusters created and measured",
            "- top-to-bottom and left-to-right layouts created",
            "- node, graph and cluster bounds read",
            "- edge spline geometry read"
        ];

        AppendLayout(lines, TopToBottom);
        AppendLayout(lines, LeftToRight);

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendLayout(List<string> lines, GraphvizReferenceLayout layout)
    {
        GraphvizReferenceCluster entryCluster = layout.Clusters.Single(cluster => cluster.Id == "cluster_entry");
        GraphvizReferenceCluster workCluster = layout.Clusters.Single(cluster => cluster.Id == "cluster_work");
        GraphvizReferenceEdge backEdge = layout.Edges.Single(edge => edge.IsBackEdge);

        lines.Add(string.Empty);
        lines.Add($"{layout.Direction} layout:");
        lines.Add($"- Root graph bounds: {layout.GraphBounds.ToDisplayText()}");
        lines.Add($"- Entry cluster bounds: {entryCluster.Bounds.ToDisplayText()}");
        lines.Add($"- Work cluster bounds: {workCluster.Bounds.ToDisplayText()}");
        lines.Add($"- Back-edge spline point count: {backEdge.Points.Count}");
        lines.Add("- Node bounds:");

        foreach (GraphvizReferenceNode node in layout.Nodes.OrderBy(node => node.Id, StringComparer.Ordinal))
        {
            lines.Add($"  - {node.Id}: {node.Bounds.ToDisplayText()}");
        }
    }
}

public sealed record GraphvizMinimalGraphReferenceResult(
    GraphvizReferenceLayout TopToBottom,
    GraphvizReferenceLayout LeftToRight);

public sealed record GraphvizReferenceLayout(
    string Direction,
    GraphvizReferenceRectangle GraphBounds,
    IReadOnlyList<GraphvizReferenceCluster> Clusters,
    IReadOnlyList<GraphvizReferenceNode> Nodes,
    IReadOnlyList<GraphvizReferenceEdge> Edges);

public sealed record GraphvizReferenceCluster(
    string Id,
    string Title,
    GraphvizReferenceRectangle Bounds);

public sealed record GraphvizReferenceNode(
    string Id,
    string Title,
    GraphvizReferenceRectangle Bounds);

public sealed record GraphvizReferenceEdge(
    string Id,
    bool IsBackEdge,
    IReadOnlyList<GraphvizReferencePoint> Points);

public sealed record GraphvizReferencePoint(double X, double Y);

public sealed record GraphvizReferenceRectangle(double X, double Y, double Width, double Height)
{
    public string ToDisplayText()
    {
        return $"{{X={X}, Y={Y}, Width={Width}, Height={Height}}}";
    }
}
