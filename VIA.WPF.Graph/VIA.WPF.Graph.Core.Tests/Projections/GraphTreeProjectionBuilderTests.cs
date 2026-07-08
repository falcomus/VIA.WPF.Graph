using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Core.Projections;

namespace VIA.WPF.Graph.Core.Tests.Projections;

public sealed class GraphTreeProjectionBuilderTests
{
    [Fact]
    public void Build_CreatesBranchTreeForStructuralLinks()
    {
        GraphDocument document = new(
            "tree",
            nodes: [new("start", "Start"), new("main", "Main"), new("finish", "Finish")],
            links:
            [
                new("start_main", "start", "main", kind: GraphLinkKind.Primary),
                new("main_finish", "main", "finish", kind: GraphLinkKind.Secondary)
            ]);

        GraphTreeProjection projection = GraphTreeProjectionBuilder.Build(document, rootNodeId: "start");

        GraphTreeNode root = Assert.Single(projection.Roots);
        Assert.Equal(GraphTreeNodeKind.Root, root.Kind);
        GraphTreeNode main = Assert.Single(root.Children);
        Assert.Equal(GraphTreeNodeKind.Branch, main.Kind);
        Assert.Equal("main", main.NodeId);
        GraphTreeNode finish = Assert.Single(main.Children);
        Assert.Equal(GraphTreeNodeKind.Branch, finish.Kind);
        Assert.Equal("finish", finish.NodeId);
    }

    [Fact]
    public void Build_StopsCyclesWithReferenceNode()
    {
        GraphDocument document = new(
            "cycle",
            nodes: [new("a", "A"), new("b", "B")],
            links:
            [
                new("a_b", "a", "b", kind: GraphLinkKind.Primary),
                new("b_a", "b", "a", kind: GraphLinkKind.Primary)
            ]);

        GraphTreeProjection projection = GraphTreeProjectionBuilder.Build(document, rootNodeId: "a");

        GraphTreeNode root = Assert.Single(projection.Roots);
        GraphTreeNode b = Assert.Single(root.Children);
        GraphTreeNode referenceToA = Assert.Single(b.Children);

        Assert.Equal(GraphTreeNodeKind.Reference, referenceToA.Kind);
        Assert.Equal("a", referenceToA.NodeId);
        Assert.Empty(referenceToA.Children);
    }

    [Fact]
    public void Build_TreatsBackLinksAsReferenceWithoutRecursion()
    {
        GraphDocument document = new(
            "back",
            nodes: [new("start", "Start"), new("confirm", "Confirm")],
            links:
            [
                new("start_confirm", "start", "confirm", kind: GraphLinkKind.Primary),
                new("confirm_start_back", "confirm", "start", kind: GraphLinkKind.Back)
            ]);

        GraphTreeProjection projection = GraphTreeProjectionBuilder.Build(document, rootNodeId: "start");

        GraphTreeNode confirm = Assert.Single(Assert.Single(projection.Roots).Children);
        GraphTreeNode back = Assert.Single(confirm.Children);

        Assert.Equal(GraphTreeNodeKind.Reference, back.Kind);
        Assert.Equal(GraphLinkKind.Back, back.LinkKind);
        Assert.Empty(back.Children);
    }

    [Fact]
    public void Build_CreatesTerminalForExternalTargetsAndMissingTargetNode()
    {
        GraphDocument document = new(
            "external-and-missing",
            nodes:
            [
                new("start", "Start"),
                new("external", "External", kind: GraphNodeKind.External)
            ],
            links:
            [
                new("start_external", "start", "external", kind: GraphLinkKind.External),
                new("start_missing", "start", "missing", kind: GraphLinkKind.Secondary)
            ]);

        GraphTreeProjection projection = GraphTreeProjectionBuilder.Build(document, rootNodeId: "start");

        GraphTreeNode root = Assert.Single(projection.Roots);
        Assert.Contains(root.Children, child =>
            child.Kind == GraphTreeNodeKind.Terminal
            && child.NodeId == "external");
        Assert.Contains(root.Children, child =>
            child.Kind == GraphTreeNodeKind.MissingTarget
            && child.NodeId == "missing");
    }

    [Fact]
    public void Build_KeepsUnreachableComponentsVisibleAsSeparateRoots()
    {
        GraphDocument document = new(
            "multi-root",
            nodes: [new("start", "Start"), new("main", "Main"), new("isolated", "Isolated")],
            links: [new("start_main", "start", "main", kind: GraphLinkKind.Primary)]);

        GraphTreeProjection projection = GraphTreeProjectionBuilder.Build(document);

        Assert.Contains(projection.Roots, root => root.NodeId == "start");
        Assert.Contains(projection.Roots, root => root.NodeId == "isolated");
        Assert.DoesNotContain(projection.Roots, root => root.NodeId == "main");
    }
}
