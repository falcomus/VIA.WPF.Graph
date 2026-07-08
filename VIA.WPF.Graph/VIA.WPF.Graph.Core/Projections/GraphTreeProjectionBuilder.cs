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

            roots.Add(BuildNode(
                rootNode,
                GraphTreeNodeKind.Root,
                treeNodeId: $"root:{rootNode.Id}",
                linkId: null,
                linkKind: null,
                nodeById,
                outgoingLinksBySourceId,
                recursiveKinds,
                ancestorNodeIds: [],
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

                roots.Add(BuildNode(
                    node,
                    GraphTreeNodeKind.Root,
                    treeNodeId: $"root:{node.Id}",
                    linkId: null,
                    linkKind: null,
                    nodeById,
                    outgoingLinksBySourceId,
                    recursiveKinds,
                    ancestorNodeIds: [],
                    visitedNodeIds));
            }
        }

        return new GraphTreeProjection(roots);
    }

    private static GraphTreeNode BuildNode(
        GraphNode node,
        GraphTreeNodeKind kind,
        string treeNodeId,
        string? linkId,
        GraphLinkKind? linkKind,
        IReadOnlyDictionary<string, GraphNode> nodeById,
        IReadOnlyDictionary<string, GraphLink[]> outgoingLinksBySourceId,
        IReadOnlySet<GraphLinkKind> recursiveLinkKinds,
        HashSet<string> ancestorNodeIds,
        HashSet<string> visitedNodeIds)
    {
        HashSet<string> nextAncestorNodeIds = new(ancestorNodeIds, StringComparer.Ordinal)
        {
            node.Id
        };

        visitedNodeIds.Add(node.Id);

        IReadOnlyList<GraphTreeNode> children = BuildChildren(
            node,
            treeNodeId,
            nodeById,
            outgoingLinksBySourceId,
            recursiveLinkKinds,
            nextAncestorNodeIds,
            visitedNodeIds);

        return new GraphTreeNode(
            treeNodeId,
            node.Id,
            node.Title,
            kind,
            linkId,
            linkKind,
            children);
    }

    private static IReadOnlyList<GraphTreeNode> BuildChildren(
        GraphNode node,
        string parentTreeNodeId,
        IReadOnlyDictionary<string, GraphNode> nodeById,
        IReadOnlyDictionary<string, GraphLink[]> outgoingLinksBySourceId,
        IReadOnlySet<GraphLinkKind> recursiveLinkKinds,
        HashSet<string> ancestorNodeIds,
        HashSet<string> visitedNodeIds)
    {
        if (!outgoingLinksBySourceId.TryGetValue(node.Id, out GraphLink[]? outgoingLinks))
        {
            return Array.Empty<GraphTreeNode>();
        }

        List<GraphTreeNode> children = [];

        foreach (GraphLink link in outgoingLinks)
        {
            string childTreeNodeId = $"{parentTreeNodeId}/link:{link.Id}";

            if (!nodeById.TryGetValue(link.TargetNodeId, out GraphNode? targetNode))
            {
                children.Add(new GraphTreeNode(
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
                children.Add(new GraphTreeNode(
                    childTreeNodeId,
                    targetNode.Id,
                    targetNode.Title,
                    GraphTreeNodeKind.Terminal,
                    link.Id,
                    link.Kind));
                continue;
            }

            if (!recursiveLinkKinds.Contains(link.Kind)
                || ancestorNodeIds.Contains(targetNode.Id)
                || visitedNodeIds.Contains(targetNode.Id))
            {
                visitedNodeIds.Add(targetNode.Id);
                children.Add(new GraphTreeNode(
                    childTreeNodeId,
                    targetNode.Id,
                    targetNode.Title,
                    GraphTreeNodeKind.Reference,
                    link.Id,
                    link.Kind));
                continue;
            }

            children.Add(BuildNode(
                targetNode,
                GraphTreeNodeKind.Branch,
                childTreeNodeId,
                link.Id,
                link.Kind,
                nodeById,
                outgoingLinksBySourceId,
                recursiveLinkKinds,
                ancestorNodeIds,
                visitedNodeIds));
        }

        return children;
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
}
