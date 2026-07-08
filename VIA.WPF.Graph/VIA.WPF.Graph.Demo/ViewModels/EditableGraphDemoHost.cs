using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Core.Requests;

namespace VIA.WPF.Graph.Demo.ViewModels;

/// <summary>
/// Host-side demo model for Phase 6. It owns the mutable graph state and exposes immutable snapshots to the graph library.
/// </summary>
public sealed class EditableGraphDemoHost : IGraphRequestHandler
{
    private readonly Dictionary<string, GraphNode> nodesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphLink> linksById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphGroup> groupsById = new(StringComparer.Ordinal);
    private string documentId = "demo-host";

    public EditableGraphDemoHost(GraphHostEditMode editMode = GraphHostEditMode.ReadOnly)
    {
        SetEditMode(editMode);
        Snapshot = new GraphDocument(documentId);
    }

    public GraphHostCapabilities Capabilities { get; private set; } = GraphHostCapabilities.ReadOnly();

    public GraphDocument Snapshot { get; private set; }

    public void SetEditMode(GraphHostEditMode editMode)
    {
        Capabilities = editMode == GraphHostEditMode.Editable
            ? GraphHostCapabilities.Editable()
            : GraphHostCapabilities.ReadOnly();
    }

    public void LoadSnapshot(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        documentId = document.Id;
        nodesById.Clear();
        linksById.Clear();
        groupsById.Clear();

        foreach (GraphGroup group in document.Groups)
        {
            groupsById[group.Id] = group;
        }

        foreach (GraphNode node in document.Nodes)
        {
            nodesById[node.Id] = node;
        }

        foreach (GraphLink link in document.Links)
        {
            linksById[link.Id] = link;
        }

        RebuildSnapshot();
    }

    public ValueTask<GraphRequestResult> HandleAsync(GraphRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        GraphRequestValidationResult validationResult = GraphRequestValidator.Validate(request, Capabilities);
        if (!validationResult.IsValid)
        {
            return ValueTask.FromResult(GraphRequestResult.FromValidation(request, validationResult));
        }

        GraphRequestResult result = request.Kind switch
        {
            GraphRequestKind.SelectNode => AcceptSelection(request, request.NodeId),
            GraphRequestKind.SelectLink => AcceptSelection(request, request.LinkId),
            GraphRequestKind.SelectGroup => AcceptSelection(request, request.GroupId),
            GraphRequestKind.ClearSelection => GraphRequestResult.Success(request, "Selection cleared."),
            GraphRequestKind.OpenNode => AcceptOpen(request, request.NodeId),
            GraphRequestKind.OpenLink => AcceptOpen(request, request.LinkId),
            GraphRequestKind.OpenGroup => AcceptOpen(request, request.GroupId),
            GraphRequestKind.ReturnToOverview => GraphRequestResult.Success(request, "Returned to overview."),
            GraphRequestKind.SetGroupCollapsed => AcceptCollapse(request),
            GraphRequestKind.CreateNode => CreateNode(request),
            GraphRequestKind.CreateLink => CreateLink(request),
            GraphRequestKind.RetargetLink => RetargetLink(request),
            GraphRequestKind.DeleteNode => DeleteNode(request),
            GraphRequestKind.DeleteLink => DeleteLink(request),
            _ => GraphRequestResult.NotSupported(request, $"Unsupported graph request kind '{request.Kind}'."),
        };

        if (result.Succeeded && IsMutationRequest(request.Kind))
        {
            RebuildSnapshot();
        }

        return ValueTask.FromResult(result);
    }

    private GraphRequestResult AcceptSelection(GraphRequest request, string? subjectId)
    {
        return GraphRequestResult.Success(
            request,
            $"Host accepted selection '{subjectId}'.",
            affectedNodeIds: request.NodeId is null ? null : [request.NodeId],
            affectedLinkIds: request.LinkId is null ? null : [request.LinkId],
            affectedGroupIds: request.GroupId is null ? null : [request.GroupId]);
    }

    private GraphRequestResult AcceptOpen(GraphRequest request, string? subjectId)
    {
        return GraphRequestResult.Success(
            request,
            $"Host accepted open request for '{subjectId}'.",
            affectedNodeIds: request.NodeId is null ? null : [request.NodeId],
            affectedLinkIds: request.LinkId is null ? null : [request.LinkId],
            affectedGroupIds: request.GroupId is null ? null : [request.GroupId]);
    }

    private GraphRequestResult AcceptCollapse(GraphRequest request)
    {
        return GraphRequestResult.Success(
            request,
            $"Host accepted collapse state '{request.IsGroupCollapsed}' for group '{request.GroupId}'.",
            affectedGroupIds: request.GroupId is null ? null : [request.GroupId]);
    }

    private GraphRequestResult CreateNode(GraphRequest request)
    {
        if (request.NodeId is null || request.Title is null)
        {
            return GraphRequestResult.Rejected(request, "CreateNode requires a node id and title.");
        }

        if (nodesById.ContainsKey(request.NodeId))
        {
            return GraphRequestResult.Rejected(request, $"Node '{request.NodeId}' already exists.");
        }

        string[] groupMemberships = ResolveCreateNodeGroupMemberships(request.GroupId);
        nodesById[request.NodeId] = new GraphNode(
            request.NodeId,
            request.Title,
            groupMemberships: groupMemberships);

        return GraphRequestResult.Success(
            request,
            $"Node '{request.NodeId}' was created in the demo host model.",
            affectedNodeIds: [request.NodeId],
            affectedGroupIds: groupMemberships);
    }

    private GraphRequestResult CreateLink(GraphRequest request)
    {
        if (request.LinkId is null || request.SourceNodeId is null || request.TargetNodeId is null)
        {
            return GraphRequestResult.Rejected(request, "CreateLink requires a link id, source node id and target node id.");
        }

        if (linksById.ContainsKey(request.LinkId))
        {
            return GraphRequestResult.Rejected(request, $"Link '{request.LinkId}' already exists.");
        }

        if (!nodesById.ContainsKey(request.SourceNodeId) || !nodesById.ContainsKey(request.TargetNodeId))
        {
            return GraphRequestResult.Rejected(request, "CreateLink requires existing source and target nodes.");
        }

        linksById[request.LinkId] = new GraphLink(
            request.LinkId,
            request.SourceNodeId,
            request.TargetNodeId,
            kind: GraphLinkKind.Secondary);

        return GraphRequestResult.Success(
            request,
            $"Link '{request.LinkId}' was created in the demo host model.",
            affectedLinkIds: [request.LinkId],
            affectedNodeIds: [request.SourceNodeId, request.TargetNodeId]);
    }

    private GraphRequestResult RetargetLink(GraphRequest request)
    {
        if (request.LinkId is null || !linksById.TryGetValue(request.LinkId, out GraphLink? existingLink))
        {
            return GraphRequestResult.Rejected(request, $"Link '{request.LinkId}' does not exist.");
        }

        string sourceNodeId = request.SourceNodeId ?? existingLink.SourceNodeId;
        string targetNodeId = request.TargetNodeId ?? existingLink.TargetNodeId;
        if (!nodesById.ContainsKey(sourceNodeId) || !nodesById.ContainsKey(targetNodeId))
        {
            return GraphRequestResult.Rejected(request, "RetargetLink requires existing endpoint nodes.");
        }

        linksById[request.LinkId] = new GraphLink(
            existingLink.Id,
            sourceNodeId,
            targetNodeId,
            existingLink.Direction,
            existingLink.Kind,
            existingLink.Label,
            existingLink.LineStyle,
            existingLink.Weight,
            existingLink.IsLayoutConstraint,
            existingLink.Metadata);

        return GraphRequestResult.Success(
            request,
            $"Link '{request.LinkId}' was retargeted in the demo host model.",
            affectedLinkIds: [request.LinkId],
            affectedNodeIds: [sourceNodeId, targetNodeId]);
    }

    private GraphRequestResult DeleteNode(GraphRequest request)
    {
        if (request.NodeId is null || !nodesById.Remove(request.NodeId))
        {
            return GraphRequestResult.Rejected(request, $"Node '{request.NodeId}' does not exist.");
        }

        string[] removedLinkIds = linksById.Values
            .Where(link => string.Equals(link.SourceNodeId, request.NodeId, StringComparison.Ordinal)
                || string.Equals(link.TargetNodeId, request.NodeId, StringComparison.Ordinal))
            .Select(link => link.Id)
            .ToArray();

        foreach (string removedLinkId in removedLinkIds)
        {
            linksById.Remove(removedLinkId);
        }

        return GraphRequestResult.Success(
            request,
            $"Node '{request.NodeId}' and {removedLinkIds.Length} connected link(s) were deleted in the demo host model.",
            affectedNodeIds: [request.NodeId],
            affectedLinkIds: removedLinkIds);
    }

    private GraphRequestResult DeleteLink(GraphRequest request)
    {
        if (request.LinkId is null || !linksById.Remove(request.LinkId))
        {
            return GraphRequestResult.Rejected(request, $"Link '{request.LinkId}' does not exist.");
        }

        return GraphRequestResult.Success(
            request,
            $"Link '{request.LinkId}' was deleted in the demo host model.",
            affectedLinkIds: [request.LinkId]);
    }

    private string[] ResolveCreateNodeGroupMemberships(string? requestedGroupId)
    {
        if (!string.IsNullOrWhiteSpace(requestedGroupId)
            && groupsById.TryGetValue(requestedGroupId, out GraphGroup? requestedGroup)
            && requestedGroup.Kind == GraphGroupKind.Container)
        {
            return [requestedGroupId];
        }

        string? firstContainerGroupId = groupsById.Values
            .FirstOrDefault(group => group.Kind == GraphGroupKind.Container)
            ?.Id;

        return firstContainerGroupId is null ? [] : [firstContainerGroupId];
    }

    private void RebuildSnapshot()
    {
        Snapshot = new GraphDocument(
            documentId,
            nodesById.Values.OrderBy(node => node.Id, StringComparer.Ordinal).ToArray(),
            linksById.Values.OrderBy(link => link.Id, StringComparer.Ordinal).ToArray(),
            groups: groupsById.Values.OrderBy(group => group.Id, StringComparer.Ordinal).ToArray());
    }

    private static bool IsMutationRequest(GraphRequestKind requestKind)
    {
        return requestKind is GraphRequestKind.CreateNode
            or GraphRequestKind.CreateLink
            or GraphRequestKind.RetargetLink
            or GraphRequestKind.DeleteNode
            or GraphRequestKind.DeleteLink;
    }
}
