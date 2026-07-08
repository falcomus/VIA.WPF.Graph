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

    [Fact]
    public void CreateNode_CreatesNeutralCreateRequest()
    {
        GraphRequest request = GraphRequest.CreateNode("node-new", "New node", "group-1");

        Assert.Equal(GraphRequestKind.CreateNode, request.Kind);
        Assert.Equal("node-new", request.NodeId);
        Assert.Equal("New node", request.Title);
        Assert.Equal("group-1", request.GroupId);
        Assert.Null(request.LinkId);
    }

    [Fact]
    public void CreateLink_CreatesNeutralCreateRequest()
    {
        GraphRequest request = GraphRequest.CreateLink("link-new", "source", "target");

        Assert.Equal(GraphRequestKind.CreateLink, request.Kind);
        Assert.Equal("link-new", request.LinkId);
        Assert.Equal("source", request.SourceNodeId);
        Assert.Equal("target", request.TargetNodeId);
    }

    [Fact]
    public void RetargetLink_CreatesNeutralRetargetRequest()
    {
        GraphRequest request = GraphRequest.RetargetLink("link-1", targetNodeId: "target-2");

        Assert.Equal(GraphRequestKind.RetargetLink, request.Kind);
        Assert.Equal("link-1", request.LinkId);
        Assert.Null(request.SourceNodeId);
        Assert.Equal("target-2", request.TargetNodeId);
    }

    [Fact]
    public void DeleteRequests_CreateNeutralDeleteRequests()
    {
        Assert.Equal(GraphRequestKind.DeleteNode, GraphRequest.DeleteNode("node-1").Kind);
        Assert.Equal(GraphRequestKind.DeleteLink, GraphRequest.DeleteLink("link-1").Kind);
    }

    [Fact]
    public void InvalidEditRequestPayload_Throws()
    {
        Assert.Throws<ArgumentException>(() => GraphRequest.CreateNode("node-new", " "));
        Assert.Throws<ArgumentException>(() => GraphRequest.CreateLink("link-new", "source", " "));
        Assert.Throws<ArgumentException>(() => GraphRequest.RetargetLink("link-1"));
    }

}
