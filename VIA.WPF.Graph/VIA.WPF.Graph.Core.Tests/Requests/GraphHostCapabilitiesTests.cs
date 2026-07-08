using VIA.WPF.Graph.Core.Requests;

namespace VIA.WPF.Graph.Core.Tests.Requests;

public sealed class GraphHostCapabilitiesTests
{
    [Fact]
    public void ReadOnly_DefaultsToNonMutatingInteractionCapabilities()
    {
        GraphHostCapabilities capabilities = GraphHostCapabilities.ReadOnly();

        Assert.True(capabilities.IsReadOnly);
        Assert.False(capabilities.IsEditable);
        Assert.False(capabilities.HasGraphMutationCapabilities);
        Assert.True(capabilities.Supports(GraphRequest.SelectNode("node-1")));
        Assert.True(capabilities.Supports(GraphRequest.SetGroupCollapsed("group-1", isCollapsed: true)));
    }

    [Fact]
    public void ReadOnly_WithGraphMutationCapability_Throws()
    {
        Assert.Throws<ArgumentException>(() => new GraphHostCapabilities(
            GraphHostEditMode.ReadOnly,
            canCreateLinks: true));
    }

    [Fact]
    public void Editable_DefaultsToGraphMutationCapabilities()
    {
        GraphHostCapabilities capabilities = GraphHostCapabilities.Editable();

        Assert.False(capabilities.IsReadOnly);
        Assert.True(capabilities.IsEditable);
        Assert.True(capabilities.HasGraphMutationCapabilities);
        Assert.True(capabilities.CanCreateNodes);
        Assert.True(capabilities.CanCreateLinks);
        Assert.True(capabilities.CanRetargetLinks);
        Assert.True(capabilities.CanDeleteNodes);
        Assert.True(capabilities.CanDeleteLinks);
    }

    [Fact]
    public void SupportedRequestKinds_AreCopiedAndDistinct()
    {
        List<GraphRequestKind> requestKinds =
        [
            GraphRequestKind.SelectNode,
            GraphRequestKind.SelectNode,
            GraphRequestKind.OpenNode
        ];

        GraphHostCapabilities capabilities = GraphHostCapabilities.ReadOnly(requestKinds);
        requestKinds.Clear();

        Assert.Equal(new[] { GraphRequestKind.SelectNode, GraphRequestKind.OpenNode }, capabilities.SupportedRequestKinds);
    }

    [Fact]
    public void Supports_RespectsSupportedRequestKinds()
    {
        GraphHostCapabilities capabilities = GraphHostCapabilities.ReadOnly(
            [GraphRequestKind.SelectNode]);

        Assert.True(capabilities.Supports(GraphRequest.SelectNode("node-1")));
        Assert.False(capabilities.Supports(GraphRequest.ClearSelection()));
    }

    [Fact]
    public void Metadata_IsCopied()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["profile"] = "demo"
        };

        GraphHostCapabilities capabilities = GraphHostCapabilities.Editable(metadata: metadata);
        metadata["profile"] = "changed";

        Assert.Equal("demo", capabilities.Metadata["profile"]);
    }

    [Fact]
    public async Task RequestHandler_ExposesHostCapabilities()
    {
        IGraphRequestHandler handler = new RecordingRequestHandler(GraphHostCapabilities.Editable(canDeleteNodes: false));

        GraphRequestResult result = await handler.HandleAsync(GraphRequest.SelectNode("node-1"));

        Assert.True(handler.Capabilities.IsEditable);
        Assert.False(handler.Capabilities.CanDeleteNodes);
        Assert.True(result.Succeeded);
    }

    private sealed class RecordingRequestHandler : IGraphRequestHandler
    {
        public RecordingRequestHandler(GraphHostCapabilities capabilities)
        {
            Capabilities = capabilities;
        }

        public GraphHostCapabilities Capabilities { get; }

        public ValueTask<GraphRequestResult> HandleAsync(GraphRequest request, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GraphRequestResult.Success(request));
        }
    }
}
