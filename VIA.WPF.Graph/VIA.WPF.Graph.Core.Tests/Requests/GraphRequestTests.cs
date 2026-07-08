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
    public void InvalidRequestSubject_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GraphRequest(GraphRequestKind.OpenNode, linkId: "link-1"));
    }
}
