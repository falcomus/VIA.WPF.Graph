using VIA.WPF.Graph.Core.Model;

namespace VIA.WPF.Graph.Core.Tests.Model;

public sealed class GraphDocumentModelTests
{
    [Fact]
    public void GraphDocument_CopiesNodesLinksGroupsAndMetadata()
    {
        List<GraphNode> nodes = [new("start", "Start")];
        List<GraphLink> links = [new("start_main", "start", "main")];
        List<GraphGroup> groups = [new("entry", "Entry", GraphGroupKind.Container)];
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["source"] = "test"
        };

        GraphDocument document = new(
            "doc",
            nodes,
            links,
            metadata,
            groups);

        nodes.Add(new GraphNode("main", "Main"));
        links.Clear();
        groups.Clear();
        metadata["source"] = "changed";

        Assert.Equal("doc", document.Id);
        Assert.Single(document.Nodes);
        Assert.Single(document.Links);
        Assert.Single(document.Groups);
        Assert.Equal("test", document.Metadata["source"]);
    }

    [Fact]
    public void GraphNode_FiltersBlankAndDuplicateGroupMemberships()
    {
        GraphNode node = new(
            "main",
            "Main",
            groupMemberships: ["work", "", "work", "marker"]);

        Assert.Equal(new[] { "work", "marker" }, node.GroupMemberships);
    }

    [Fact]
    public void GraphSize_RejectsInvalidDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphSize(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphSize(10, -1));
    }
}
