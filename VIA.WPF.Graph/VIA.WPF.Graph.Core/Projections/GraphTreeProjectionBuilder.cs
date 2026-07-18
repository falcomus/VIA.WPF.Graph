using VIA.WPF.Graph.Core.Model;

namespace VIA.WPF.Graph.Core.Projections;

/// <summary>
/// Builds a cycle-safe navigation tree projection from a neutral graph document.
/// </summary>
public static class GraphTreeProjectionBuilder
{
    private static readonly GraphLinkKind[] DefaultRecursiveLinkKinds =
    [
        GraphLinkKind.Primary,
        GraphLinkKind.Secondary,
        GraphLinkKind.PopupOpen
    ];

    public static GraphTreeProjection Build(
        GraphDocument document,
        string? rootNodeId = null,
        IEnumerable<GraphLinkKind>? recursiveLinkKinds = null,
        bool includeUnreachableComponents = true)
    {
        ArgumentNullException.ThrowIfNull(document);

        Dictionary<string, GraphNode> nodeById = document.Nodes
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        Dictionary<string, GraphLink[]> outgoingLinksBySourceId = document.Links
            .GroupBy(link => link.SourceNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        HashSet<GraphLinkKind> recursiveKinds = new(
            recursiveLinkKinds ?? DefaultRecursiveLinkKinds);

        List<GraphTreeNode> roots = [];
        HashSet<string> visitedNodeIds = new(StringComparer.Ordinal);

        foreach (string resolvedRootNodeId in ResolveRootNodeIds(document, nodeById, recursiveKinds, rootNodeId))
        {
            if (visitedNodeIds.Contains(resolvedRootNodeId))
            {
                continue;
            }

            if (!nodeById.TryGetValue(resolvedRootNodeId, out GraphNode? rootNode))
            {
                continue;
            }

            roots.Add(BuildNodeIterative(
                rootNode,
                GraphTreeNodeKind.Root,
                treeNodeId: $"root:{rootNode.Id}",
                linkId: null,
                linkKind: null,
                nodeById,
                outgoingLinksBySourceId,
                recursiveKinds,
                visitedNodeIds));
        }

        if (includeUnreachableComponents)
        {
            foreach (GraphNode node in document.Nodes)
            {
                if (visitedNodeIds.Contains(node.Id))
                {
                    continue;
                }

                roots.Add(BuildNodeIterative(
                    node,
                    GraphTreeNodeKind.Root,
                    treeNodeId: $"root:{node.Id}",
                    linkId: null,
                    linkKind: null,
                    nodeById,
                    outgoingLinksBySourceId,
                    recursiveKinds,
                    visitedNodeIds));
            }
        }

        return new GraphTreeProjection(roots);
    }

    private static GraphTreeNode BuildNodeIterative(
        GraphNode rootNode,
        GraphTreeNodeKind rootKind,
        string treeNodeId,
        string? linkId,
        GraphLinkKind? linkKind,
        IReadOnlyDictionary<string, GraphNode> nodeById,
        IReadOnlyDictionary<string, GraphLink[]> outgoingLinksBySourceId,
        IReadOnlySet<GraphLinkKind> recursiveLinkKinds,
        HashSet<string> visitedNodeIds)
    {
        Stack<BuildFrame> pendingFrames = new();
        HashSet<string> activeAncestorNodeIds = new(StringComparer.Ordinal);

        visitedNodeIds.Add(rootNode.Id);
        activeAncestorNodeIds.Add(rootNode.Id);
        pendingFrames.Push(CreateFrame(rootNode, rootKind, treeNodeId, linkId, linkKind, outgoingLinksBySourceId));

        while (pendingFrames.Count > 0)
        {
            BuildFrame frame = pendingFrames.Peek();
            if (frame.NextLinkIndex >= frame.OutgoingLinks.Count)
            {
                GraphTreeNode completedNode = new(
                    frame.TreeNodeId,
                    frame.Node.Id,
                    frame.Node.Title,
                    frame.Kind,
                    frame.LinkId,
                    frame.LinkKind,
                    frame.Children);

                pendingFrames.Pop();
                activeAncestorNodeIds.Remove(frame.Node.Id);

                if (pendingFrames.Count == 0)
                {
                    return completedNode;
                }

                pendingFrames.Peek().Children.Add(completedNode);
                continue;
            }

            GraphLink link = frame.OutgoingLinks[frame.NextLinkIndex++];
            string childTreeNodeId = $"{frame.TreeNodeId}/link:{link.Id}";

            if (!nodeById.TryGetValue(link.TargetNodeId, out GraphNode? targetNode))
            {
                frame.Children.Add(new GraphTreeNode(
                    childTreeNodeId,
                    link.TargetNodeId,
                    link.TargetNodeId,
                    GraphTreeNodeKind.MissingTarget,
                    link.Id,
                    link.Kind));
                continue;
            }

            if (link.Kind == GraphLinkKind.External || targetNode.Kind == GraphNodeKind.External)
            {
                visitedNodeIds.Add(targetNode.Id);
                frame.Children.Add(new GraphTreeNode(
                    childTreeNodeId,
                    targetNode.Id,
                    targetNode.Title,
                    GraphTreeNodeKind.Terminal,
                    link.Id,
                    link.Kind));
                continue;
            }

            if (!recursiveLinkKinds.Contains(link.Kind)
                || activeAncestorNodeIds.Contains(targetNode.Id)
                || visitedNodeIds.Contains(targetNode.Id))
            {
                visitedNodeIds.Add(targetNode.Id);
                frame.Children.Add(new GraphTreeNode(
                    childTreeNodeId,
                    targetNode.Id,
                    targetNode.Title,
                    GraphTreeNodeKind.Reference,
                    link.Id,
                    link.Kind));
                continue;
            }

            visitedNodeIds.Add(targetNode.Id);
            activeAncestorNodeIds.Add(targetNode.Id);
            pendingFrames.Push(CreateFrame(
                targetNode,
                GraphTreeNodeKind.Branch,
                childTreeNodeId,
                link.Id,
                link.Kind,
                outgoingLinksBySourceId));
        }

        throw new InvalidOperationException("The graph tree projection traversal ended without producing a root node.");
    }

    private static BuildFrame CreateFrame(
        GraphNode node,
        GraphTreeNodeKind kind,
        string treeNodeId,
        string? linkId,
        GraphLinkKind? linkKind,
        IReadOnlyDictionary<string, GraphLink[]> outgoingLinksBySourceId)
    {
        GraphLink[] outgoingLinks = outgoingLinksBySourceId.TryGetValue(node.Id, out GraphLink[]? resolvedLinks)
            ? resolvedLinks
            : Array.Empty<GraphLink>();

        return new BuildFrame(node, kind, treeNodeId, linkId, linkKind, outgoingLinks);
    }

    private static IReadOnlyList<string> ResolveRootNodeIds(
        GraphDocument document,
        IReadOnlyDictionary<string, GraphNode> nodeById,
        IReadOnlySet<GraphLinkKind> recursiveLinkKinds,
        string? requestedRootNodeId)
    {
        if (!string.IsNullOrWhiteSpace(requestedRootNodeId) && nodeById.ContainsKey(requestedRootNodeId))
        {
            return [requestedRootNodeId];
        }

        HashSet<string> incomingStructuralTargetNodeIds = new(StringComparer.Ordinal);

        foreach (GraphLink link in document.Links)
        {
            if (!recursiveLinkKinds.Contains(link.Kind))
            {
                continue;
            }

            if (nodeById.ContainsKey(link.SourceNodeId) && nodeById.ContainsKey(link.TargetNodeId))
            {
                incomingStructuralTargetNodeIds.Add(link.TargetNodeId);
            }
        }

        string[] rootNodeIds = document.Nodes
            .Where(node => !incomingStructuralTargetNodeIds.Contains(node.Id))
            .Select(node => node.Id)
            .ToArray();

        if (rootNodeIds.Length > 0)
        {
            return rootNodeIds;
        }

        GraphNode? fallbackRoot = document.Nodes.FirstOrDefault();
        return fallbackRoot is null ? Array.Empty<string>() : [fallbackRoot.Id];
    }

    private sealed class BuildFrame
    {
        public BuildFrame(
            GraphNode node,
            GraphTreeNodeKind kind,
            string treeNodeId,
            string? linkId,
            GraphLinkKind? linkKind,
            IReadOnlyList<GraphLink> outgoingLinks)
        {
            Node = node;
            Kind = kind;
            TreeNodeId = treeNodeId;
            LinkId = linkId;
            LinkKind = linkKind;
            OutgoingLinks = outgoingLinks;
        }

        public GraphNode Node { get; }

        public GraphTreeNodeKind Kind { get; }

        public string TreeNodeId { get; }

        public string? LinkId { get; }

        public GraphLinkKind? LinkKind { get; }

        public IReadOnlyList<GraphLink> OutgoingLinks { get; }

        public List<GraphTreeNode> Children { get; } = [];

        public int NextLinkIndex { get; set; }
    }
}
