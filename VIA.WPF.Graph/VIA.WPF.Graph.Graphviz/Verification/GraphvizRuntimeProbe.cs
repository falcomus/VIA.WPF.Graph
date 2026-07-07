using System.Reflection;
using System.Runtime.InteropServices;
using Rubjerg.Graphviz;

namespace VIA.WPF.Graph.Graphviz.Verification;

/// <summary>
/// Executes the P0-002 runtime verification against Rubjerg.Graphviz 3.0.5.
/// This is a temporary technical probe and not the later layout adapter.
/// </summary>
public static class GraphvizRuntimeProbe
{
    private const string ExpectedPackageVersion = "3.0.5";

    private static readonly string[] NodeIds =
    [
        "start",
        "registration",
        "main",
        "help_popup",
        "confirmation",
        "finish"
    ];

    public static GraphvizRuntimeProbeResult Run()
    {
        GraphvizLayoutProbeResult topToBottom = RunLayout("TB");
        GraphvizLayoutProbeResult leftToRight = RunLayout("LR");

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
            topToBottom,
            leftToRight);
    }

    private static GraphvizLayoutProbeResult RunLayout(string direction)
    {
        RootGraph source = CreateSourceGraph();
        source.SetAttribute("rankdir", direction);

        RootGraph layout = source.CreateLayout(coordinateSystem: CoordinateSystem.TopLeft);

        Dictionary<string, string> nodeBounds = new(StringComparer.Ordinal);
        foreach (string nodeId in NodeIds)
        {
            Node layoutNode = layout.GetNode(nodeId)
                ?? throw new InvalidOperationException($"The {direction} layout does not contain node '{nodeId}'.");

            nodeBounds.Add(nodeId, layoutNode.GetBoundingBox().ToString());
        }

        SubGraph entryCluster = layout.GetSubgraph("cluster_entry")
            ?? throw new InvalidOperationException($"The {direction} layout does not contain cluster 'cluster_entry'.");
        SubGraph workCluster = layout.GetSubgraph("cluster_work")
            ?? throw new InvalidOperationException($"The {direction} layout does not contain cluster 'cluster_work'.");

        Node confirmation = layout.GetNode("confirmation")
            ?? throw new InvalidOperationException($"The {direction} layout does not contain node 'confirmation'.");
        Node start = layout.GetNode("start")
            ?? throw new InvalidOperationException($"The {direction} layout does not contain node 'start'.");

        Edge backEdge = layout.GetEdge(confirmation, start, "confirmation_start_back")
            ?? throw new InvalidOperationException($"The {direction} layout does not contain the named back edge.");
        PointD[] backEdgeSpline = backEdge.GetFirstSpline();

        if (backEdgeSpline.Length == 0)
        {
            throw new InvalidOperationException($"The {direction} layout has no spline geometry for the named back edge.");
        }

        return new GraphvizLayoutProbeResult(
            direction,
            layout.GetBoundingBox().ToString(),
            entryCluster.GetBoundingBox().ToString(),
            workCluster.GetBoundingBox().ToString(),
            nodeBounds,
            backEdgeSpline.Length);
    }

    private static RootGraph CreateSourceGraph()
    {
        RootGraph source = RootGraph.CreateNew(GraphType.Directed, "via_wpf_graph_p0_002");

        Node start = source.GetOrAddNode("start");
        Node registration = source.GetOrAddNode("registration");
        Node main = source.GetOrAddNode("main");
        Node helpPopup = source.GetOrAddNode("help_popup");
        Node confirmation = source.GetOrAddNode("confirmation");
        Node finish = source.GetOrAddNode("finish");

        _ = source.GetOrAddEdge(start, registration, "start_registration");
        _ = source.GetOrAddEdge(start, main, "start_main");
        _ = source.GetOrAddEdge(main, helpPopup, "main_help_popup");
        _ = source.GetOrAddEdge(helpPopup, confirmation, "help_popup_confirmation");
        _ = source.GetOrAddEdge(confirmation, finish, "confirmation_finish");
        _ = source.GetOrAddEdge(confirmation, start, "confirmation_start_back");

        SubGraph entryCluster = source.GetOrAddSubgraph("cluster_entry");
        entryCluster.AddExisting(start);
        entryCluster.AddExisting(registration);

        SubGraph workCluster = source.GetOrAddSubgraph("cluster_work");
        workCluster.AddExisting(main);
        workCluster.AddExisting(helpPopup);
        workCluster.AddExisting(confirmation);
        workCluster.AddExisting(finish);

        return source;
    }
}

public sealed record GraphvizRuntimeProbeResult(
    string ExpectedPackageVersion,
    string PackageAssemblyVersion,
    string ProcessArchitecture,
    string RuntimeIdentifier,
    GraphvizLayoutProbeResult TopToBottom,
    GraphvizLayoutProbeResult LeftToRight)
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
            "- back-edge spline geometry read"
        ];

        AppendLayout(lines, TopToBottom);
        AppendLayout(lines, LeftToRight);

        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendLayout(List<string> lines, GraphvizLayoutProbeResult layout)
    {
        lines.Add(string.Empty);
        lines.Add($"{layout.Direction} layout:");
        lines.Add($"- Root graph bounds: {layout.RootGraphBounds}");
        lines.Add($"- Entry cluster bounds: {layout.EntryClusterBounds}");
        lines.Add($"- Work cluster bounds: {layout.WorkClusterBounds}");
        lines.Add($"- Back-edge spline point count: {layout.BackEdgeSplinePointCount}");
        lines.Add("- Node bounds:");

        foreach ((string nodeId, string bounds) in layout.NodeBounds.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            lines.Add($"  - {nodeId}: {bounds}");
        }
    }
}

public sealed record GraphvizLayoutProbeResult(
    string Direction,
    string RootGraphBounds,
    string EntryClusterBounds,
    string WorkClusterBounds,
    IReadOnlyDictionary<string, string> NodeBounds,
    int BackEdgeSplinePointCount);
