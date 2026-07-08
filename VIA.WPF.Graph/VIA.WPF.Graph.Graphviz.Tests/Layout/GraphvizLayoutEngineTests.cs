using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Graphviz.Layout;

namespace VIA.WPF.Graph.Graphviz.Tests.Layout;

public sealed class GraphvizLayoutEngineTests
{
    [Fact]
    public void Layout_CreatesTopToBottomReferenceGraph()
    {
        GraphDocument document = CreateReferenceDocument();
        GraphLayoutOptions options = new(GraphLayoutDirection.TopToBottom, GraphEdgeRoutingStyle.Spline);

        GraphLayoutResult result = GraphvizLayoutEngine.Layout(document, options);

        AssertReferenceLayout(result, document, options);
        Assert.True(result.GraphBounds!.Value.Height > result.GraphBounds.Value.Width);
    }

    [Fact]
    public void Layout_CreatesLeftToRightReferenceGraph()
    {
        GraphDocument document = CreateReferenceDocument();
        GraphLayoutOptions options = new(GraphLayoutDirection.LeftToRight, GraphEdgeRoutingStyle.Spline);

        GraphLayoutResult result = GraphvizLayoutEngine.Layout(document, options);

        AssertReferenceLayout(result, document, options);
        Assert.True(result.GraphBounds!.Value.Width > result.GraphBounds.Value.Height);
    }

    [Theory]
    [InlineData(GraphEdgeRoutingStyle.Spline)]
    [InlineData(GraphEdgeRoutingStyle.Polyline)]
    [InlineData(GraphEdgeRoutingStyle.Orthogonal)]
    public void Layout_AppliesRoutingStyle(GraphEdgeRoutingStyle routingStyle)
    {
        GraphDocument document = CreateReferenceDocument();
        GraphLayoutOptions options = new(GraphLayoutDirection.LeftToRight, routingStyle);

        GraphLayoutResult result = GraphvizLayoutEngine.Layout(document, options);

        Assert.True(result.Succeeded, result.Error?.Details);
        Assert.Equal(routingStyle, result.Options.EdgeRoutingStyle);
        Assert.All(result.Edges, edge => Assert.True(edge.HasGeometry));
    }

    [Fact]
    public void Layout_DoesNotCreateClusterForMarkerGroup()
    {
        GraphDocument document = CreateReferenceDocument();

        GraphLayoutResult result = GraphvizLayoutEngine.Layout(document);

        Assert.True(result.Succeeded, result.Error?.Details);
        Assert.DoesNotContain(result.Groups, group => group.GroupId == "critical_path");
        Assert.Contains(result.Groups, group => group.GroupId == "entry");
        Assert.Contains(result.Groups, group => group.GroupId == "work");
    }

    [Fact]
    public void Layout_ReturnsControlledErrorForMissingTarget()
    {
        GraphDocument document = new(
            "missing-target",
            nodes: [new GraphNode("start", "Start")],
            links: [new GraphLink("missing", "start", "does_not_exist")]);

        GraphLayoutResult result = GraphvizLayoutEngine.Layout(document);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Contains("missing", result.Error!.Message.ToLowerInvariant());
    }

    [Fact]
    public void Layout_ReturnsControlledErrorWhenCanceledBeforeStart()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        GraphLayoutResult result = GraphvizLayoutEngine.Layout(
            CreateReferenceDocument(),
            cancellationToken: cancellationTokenSource.Token);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Equal(typeof(OperationCanceledException).FullName, result.Error!.ExceptionType);
    }

    [Fact]
    public void Layout_ReusesSuccessfulCachedResultForIdenticalRequest()
    {
        GraphDocument document = CreateReferenceDocument();
        GraphLayoutOptions options = new(GraphLayoutDirection.TopToBottom, GraphEdgeRoutingStyle.Spline);

        GraphLayoutResult first = GraphvizLayoutEngine.Layout(document, options);
        GraphLayoutResult second = GraphvizLayoutEngine.Layout(document, options);

        Assert.True(first.Succeeded, first.Error?.Details);
        Assert.Same(first, second);
    }

    private static void AssertReferenceLayout(
        GraphLayoutResult result,
        GraphDocument document,
        GraphLayoutOptions options)
    {
        Assert.True(result.Succeeded, result.Error?.Details);
        Assert.Equal(document.Id, result.DocumentId);
        Assert.Equal(options, result.Options);
        Assert.NotNull(result.GraphBounds);
        Assert.True(result.GraphBounds.Value.Width > 0d);
        Assert.True(result.GraphBounds.Value.Height > 0d);
        Assert.Equal(document.Nodes.Count, result.Nodes.Count);
        Assert.Equal(document.Links.Count, result.Edges.Count);
        Assert.Equal(2, result.Groups.Count);

        foreach (GraphNode node in document.Nodes)
        {
            GraphLayoutNode layoutNode = Assert.Single(result.Nodes, candidate => candidate.NodeId == node.Id);
            Assert.True(layoutNode.Bounds.Width > 0d);
            Assert.True(layoutNode.Bounds.Height > 0d);
        }

        foreach (GraphLink link in document.Links)
        {
            GraphLayoutEdge layoutEdge = Assert.Single(result.Edges, candidate => candidate.LinkId == link.Id);
            Assert.True(layoutEdge.HasGeometry);
        }

        GraphLayoutEdge backEdge = Assert.Single(result.Edges, edge => edge.LinkId == "confirmation_start_back");
        Assert.True(backEdge.Points.Count >= 2);
    }

    private static GraphDocument CreateReferenceDocument()
    {
        return new GraphDocument(
            "p2-reference",
            nodes:
            [
                new GraphNode("start", "Start", groupMemberships: ["entry"]),
                new GraphNode("registration", "Registration", groupMemberships: ["entry", "critical_path"]),
                new GraphNode("main", "Main", groupMemberships: ["work"]),
                new GraphNode("help_popup", "Help popup", kind: GraphNodeKind.Popup, defaultSize: GraphSize.Popup, groupMemberships: ["work"]),
                new GraphNode("confirmation", "Confirmation", groupMemberships: ["work", "critical_path"]),
                new GraphNode("finish", "Finish", groupMemberships: ["work"])
            ],
            links:
            [
                new GraphLink("start_registration", "start", "registration", kind: GraphLinkKind.Primary),
                new GraphLink("start_main", "start", "main", kind: GraphLinkKind.Primary),
                new GraphLink("main_help_popup", "main", "help_popup", kind: GraphLinkKind.PopupOpen, lineStyle: GraphLineStyle.Dashed),
                new GraphLink("help_popup_confirmation", "help_popup", "confirmation", kind: GraphLinkKind.Secondary),
                new GraphLink("confirmation_finish", "confirmation", "finish", kind: GraphLinkKind.Primary),
                new GraphLink("confirmation_start_back", "confirmation", "start", kind: GraphLinkKind.Back, lineStyle: GraphLineStyle.Dashed)
            ],
            groups:
            [
                new GraphGroup("entry", "Entry", GraphGroupKind.Container),
                new GraphGroup("work", "Work", GraphGroupKind.Container),
                new GraphGroup("critical_path", "Critical path", GraphGroupKind.Marker)
            ]);
    }
}
