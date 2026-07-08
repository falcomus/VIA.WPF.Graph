using VIA.WPF.Graph.Core.Requests;

namespace VIA.WPF.Graph.Core.Tests.Requests;

public sealed class GraphRequestResultTests
{
    [Fact]
    public void Success_CopiesAffectedIdsAndMetadata()
    {
        GraphRequest request = GraphRequest.SelectNode("node-1");
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["source"] = "test"
        };

        GraphRequestResult result = GraphRequestResult.Success(
            request,
            "handled",
            affectedNodeIds: ["node-1", "node-1", " "],
            affectedLinkIds: ["link-1"],
            affectedGroupIds: ["group-1"],
            metadata: metadata);

        metadata["source"] = "changed";

        Assert.True(result.Succeeded);
        Assert.Equal(GraphRequestResultStatus.Succeeded, result.Status);
        Assert.Same(request, result.Request);
        Assert.Equal("handled", result.Message);
        Assert.Equal(new[] { "node-1" }, result.AffectedNodeIds);
        Assert.Equal(new[] { "link-1" }, result.AffectedLinkIds);
        Assert.Equal(new[] { "group-1" }, result.AffectedGroupIds);
        Assert.Equal("test", result.Metadata["source"]);
    }

    [Theory]
    [InlineData(GraphRequestResultStatus.Rejected)]
    [InlineData(GraphRequestResultStatus.NotSupported)]
    [InlineData(GraphRequestResultStatus.Failed)]
    public void NonSuccessStatuses_AreNotSucceeded(GraphRequestResultStatus status)
    {
        GraphRequestResult result = new(GraphRequest.ClearSelection(), status);

        Assert.False(result.Succeeded);
        Assert.Equal(status, result.Status);
    }

    [Fact]
    public void OptionalMessage_IsNormalized()
    {
        GraphRequestResult result = GraphRequestResult.Rejected(GraphRequest.ClearSelection(), " ");

        Assert.Null(result.Message);
    }

    [Fact]
    public void NullRequest_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GraphRequestResult(null!, GraphRequestResultStatus.Succeeded));
    }
}
