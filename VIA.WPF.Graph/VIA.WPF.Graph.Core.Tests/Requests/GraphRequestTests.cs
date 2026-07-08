using VIA.WPF.Graph.Core.Requests;

namespace VIA.WPF.Graph.Core.Tests.Requests;

public sealed class GraphRequestTests
{
    [Fact]
    public void SelectNode_CreatesNeutralSelectionRequest()
    {
        GraphRequest request = GraphRequest.SelectNode("node-1", isMultiSelection: true);

        Assert.Equal(GraphRequestKind.SelectNode, request.Kind);
        Assert.Equal("node-1", request.NodeId);
        Assert.Null(request.LinkId);
        Assert.Null(request.GroupId);
        Assert.True(request.IsMultiSelection);
    }


    [Fact]
    public void SetGroupCollapsed_CreatesNeutralCollapseRequest()
    {
        GraphRequest request = GraphRequest.SetGroupCollapsed("group-1", isCollapsed: true);

        Assert.Equal(GraphRequestKind.SetGroupCollapsed, request.Kind);
        Assert.Equal("group-1", request.GroupId);
        Assert.True(request.IsGroupCollapsed);
        Assert.Null(request.NodeId);
        Assert.Null(request.LinkId);
    }

    [Fact]
    public void InvalidRequestSubject_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GraphRequest(GraphRequestKind.OpenNode, linkId: "link-1"));
        Assert.Throws<ArgumentException>(() => new GraphRequest(GraphRequestKind.SetGroupCollapsed, groupId: "group-1"));
    }
}
