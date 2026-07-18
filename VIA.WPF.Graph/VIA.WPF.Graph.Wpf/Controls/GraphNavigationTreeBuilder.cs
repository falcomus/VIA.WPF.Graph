using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Core.Projections;

namespace VIA.WPF.Graph.Wpf.Controls;

/// <summary>
/// Builds complete, cycle-safe WPF navigation item snapshots from neutral graph data.
/// All traversals are iterative so malformed or very deep input cannot exhaust the call stack.
/// </summary>
internal static class GraphNavigationTreeBuilder
{
    private const string UngroupedBucketKey = "\u0000ungrouped";

    public static IReadOnlyList<GraphNavigationTreeItem> Build(
        GraphDocument? document,
        GraphTreeProjection? projection,
        IReadOnlySet<string> collapsedGroupIds)
    {
        GraphGroup[] containerGroups = document?.Groups
            .Where(group => group.Kind == GraphGroupKind.Container)
            .GroupBy(group => group.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray() ?? Array.Empty<GraphGroup>();

        Dictionary<string, GraphGroup> containerGroupsById = containerGroups
            .ToDictionary(group => group.Id, StringComparer.Ordinal);
        Dictionary<string, GraphNode> nodesById = document?.Nodes
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal)
            ?? new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        Dictionary<string, GraphLink> linksById = document?.Links
            .GroupBy(link => link.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal)
            ?? new Dictionary<string, GraphLink>(StringComparer.Ordinal);

        List<NavigationSubject> subjects = [];
        int order = 0;
        AppendProjectionSubjects(
            projection?.Roots ?? Array.Empty<GraphTreeNode>(),
            subjects,
            ref order,
            nodesById,
            linksById,
            containerGroupsById);

        if (document is not null)
        {
            HashSet<string> representedNodeIds = subjects
                .Where(subject => subject.NodeId is not null)
                .Select(subject => subject.NodeId!)
                .ToHashSet(StringComparer.Ordinal);

            foreach (GraphNode node in document.Nodes)
            {
                if (representedNodeIds.Contains(node.Id))
                {
                    continue;
                }

                string? groupId = ResolveNodeContainerGroupId(node, containerGroupsById);
                subjects.Add(new NavigationSubject(
                    $"document-node:{node.Id}",
                    null,
                    node.Title,
                    node.Kind == GraphNodeKind.External
                        ? GraphNavigationTreeItemKind.Terminal
                        : GraphNavigationTreeItemKind.Node,
                    node.Id,
                    groupId,
                    node.Id,
                    null,
                    order++));
            }
        }

        bool preserveProjectionHierarchy = containerGroups.Length == 0;
        IReadOnlyDictionary<string, IReadOnlyList<GraphNavigationTreeItem>> subjectRootsByBucket =
            BuildSubjectRootsByBucket(subjects, preserveProjectionHierarchy);

        List<GraphNavigationTreeItem> roots = BuildGroupRoots(
            containerGroups,
            subjectRootsByBucket,
            collapsedGroupIds);

        if (subjectRootsByBucket.TryGetValue(UngroupedBucketKey, out IReadOnlyList<GraphNavigationTreeItem>? ungroupedItems)
            && ungroupedItems.Count > 0)
        {
            GraphNavigationTreeItem ungroupedItem = new(
                "group:__ungrouped__",
                "Ungrouped",
                GraphNavigationTreeItemKind.Group,
                CreateItemCountText(ungroupedItems.Count),
                isExpanded: true);
            ungroupedItem.SetChildren(ungroupedItems);
            roots.Add(ungroupedItem);
        }

        return roots;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<GraphNavigationTreeItem>> BuildSubjectRootsByBucket(
        IEnumerable<NavigationSubject> subjects,
        bool preserveProjectionHierarchy)
    {
        Dictionary<string, IReadOnlyList<GraphNavigationTreeItem>> result = new(StringComparer.Ordinal);

        foreach (IGrouping<string, NavigationSubject> bucket in subjects
                     .GroupBy(subject => subject.GroupId ?? UngroupedBucketKey, StringComparer.Ordinal))
        {
            NavigationSubject[] orderedSubjects = NormalizeBucketSubjects(
                bucket,
                preserveProjectionHierarchy);
            Dictionary<string, NavigationSubject> subjectsById = orderedSubjects
                .ToDictionary(subject => subject.TreeNodeId, StringComparer.Ordinal);
            Dictionary<string, string?> parentById = new(StringComparer.Ordinal);

            foreach (NavigationSubject subject in orderedSubjects)
            {
                parentById[subject.TreeNodeId] = preserveProjectionHierarchy
                    && subject.ParentTreeNodeId is not null
                    && subjectsById.ContainsKey(subject.ParentTreeNodeId)
                    ? subject.ParentTreeNodeId
                    : null;
            }

            BreakParentCycles(orderedSubjects.Select(subject => subject.TreeNodeId), parentById);

            Dictionary<string, GraphNavigationTreeItem> itemsById = orderedSubjects.ToDictionary(
                subject => subject.TreeNodeId,
                subject => new GraphNavigationTreeItem(
                    subject.TreeNodeId,
                    subject.Title,
                    subject.Kind,
                    subject.Subtitle,
                    subject.GroupId,
                    subject.NodeId,
                    subject.LinkId,
                    isExpanded: true),
                StringComparer.Ordinal);
            Dictionary<string, List<GraphNavigationTreeItem>> childrenByParentId = new(StringComparer.Ordinal);

            foreach (NavigationSubject subject in orderedSubjects)
            {
                string? parentId = parentById[subject.TreeNodeId];
                if (parentId is null)
                {
                    continue;
                }

                if (!childrenByParentId.TryGetValue(parentId, out List<GraphNavigationTreeItem>? children))
                {
                    children = [];
                    childrenByParentId[parentId] = children;
                }

                children.Add(itemsById[subject.TreeNodeId]);
            }

            foreach (NavigationSubject subject in orderedSubjects)
            {
                if (childrenByParentId.TryGetValue(subject.TreeNodeId, out List<GraphNavigationTreeItem>? children))
                {
                    itemsById[subject.TreeNodeId].SetChildren(children);
                }
            }

            result[bucket.Key] = orderedSubjects
                .Where(subject => parentById[subject.TreeNodeId] is null)
                .Select(subject => itemsById[subject.TreeNodeId])
                .ToArray();
        }

        return result;
    }

    private static NavigationSubject[] NormalizeBucketSubjects(
        IEnumerable<NavigationSubject> subjects,
        bool preserveProjectionHierarchy)
    {
        NavigationSubject[] orderedSubjects = subjects
            .OrderBy(subject => subject.Order)
            .GroupBy(subject => subject.TreeNodeId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();

        if (preserveProjectionHierarchy || orderedSubjects.Length < 2)
        {
            return orderedSubjects;
        }

        List<NavigationSubject> normalizedSubjects = [];
        Dictionary<string, int> itemIndexByDocumentNodeId = new(StringComparer.Ordinal);

        foreach (NavigationSubject subject in orderedSubjects)
        {
            if (subject.NodeId is null
                || subject.Kind == GraphNavigationTreeItemKind.MissingTarget)
            {
                normalizedSubjects.Add(subject);
                continue;
            }

            if (!itemIndexByDocumentNodeId.TryGetValue(subject.NodeId, out int existingIndex))
            {
                itemIndexByDocumentNodeId[subject.NodeId] = normalizedSubjects.Count;
                normalizedSubjects.Add(subject);
                continue;
            }

            NavigationSubject existingSubject = normalizedSubjects[existingIndex];
            if (GetGroupedSubjectPriority(subject.Kind) < GetGroupedSubjectPriority(existingSubject.Kind))
            {
                normalizedSubjects[existingIndex] = subject with { Order = existingSubject.Order };
            }
        }

        return normalizedSubjects
            .OrderBy(subject => subject.Order)
            .ToArray();
    }

    private static int GetGroupedSubjectPriority(GraphNavigationTreeItemKind kind)
    {
        return kind switch
        {
            GraphNavigationTreeItemKind.Node => 0,
            GraphNavigationTreeItemKind.Terminal => 1,
            GraphNavigationTreeItemKind.Reference => 2,
            _ => 3,
        };
    }

    private static List<GraphNavigationTreeItem> BuildGroupRoots(
        IReadOnlyList<GraphGroup> groups,
        IReadOnlyDictionary<string, IReadOnlyList<GraphNavigationTreeItem>> subjectRootsByBucket,
        IReadOnlySet<string> collapsedGroupIds)
    {
        Dictionary<string, GraphGroup> groupsById = groups.ToDictionary(group => group.Id, StringComparer.Ordinal);
        Dictionary<string, string?> parentById = new(StringComparer.Ordinal);

        foreach (GraphGroup group in groups)
        {
            parentById[group.Id] = group.ParentGroupId is not null && groupsById.ContainsKey(group.ParentGroupId)
                ? group.ParentGroupId
                : null;
        }

        BreakParentCycles(groups.Select(group => group.Id), parentById);

        Dictionary<string, GraphNavigationTreeItem> itemsById = groups.ToDictionary(
            group => group.Id,
            group => new GraphNavigationTreeItem(
                $"group:{group.Id}",
                group.Title,
                GraphNavigationTreeItemKind.Group,
                NormalizeOptionalText(group.Description)
                    ?? CreateItemCountText(GetBucketItemCount(group.Id, subjectRootsByBucket)),
                groupId: group.Id,
                isExpanded: !collapsedGroupIds.Contains(group.Id)),
            StringComparer.Ordinal);
        Dictionary<string, List<GraphNavigationTreeItem>> childGroupsByParentId = new(StringComparer.Ordinal);

        foreach (GraphGroup group in groups)
        {
            string? parentId = parentById[group.Id];
            if (parentId is null)
            {
                continue;
            }

            if (!childGroupsByParentId.TryGetValue(parentId, out List<GraphNavigationTreeItem>? children))
            {
                children = [];
                childGroupsByParentId[parentId] = children;
            }

            children.Add(itemsById[group.Id]);
        }

        foreach (GraphGroup group in groups)
        {
            List<GraphNavigationTreeItem> children = [];
            if (childGroupsByParentId.TryGetValue(group.Id, out List<GraphNavigationTreeItem>? childGroups))
            {
                children.AddRange(childGroups);
            }

            if (subjectRootsByBucket.TryGetValue(group.Id, out IReadOnlyList<GraphNavigationTreeItem>? subjects))
            {
                children.AddRange(subjects);
            }

            itemsById[group.Id].SetChildren(children);
        }

        return groups
            .Where(group => parentById[group.Id] is null)
            .Select(group => itemsById[group.Id])
            .ToList();
    }

    private static int GetBucketItemCount(
        string groupId,
        IReadOnlyDictionary<string, IReadOnlyList<GraphNavigationTreeItem>> subjectRootsByBucket)
    {
        return subjectRootsByBucket.TryGetValue(groupId, out IReadOnlyList<GraphNavigationTreeItem>? subjects)
            ? subjects.Count
            : 0;
    }

    private static void BreakParentCycles(IEnumerable<string> orderedIds, IDictionary<string, string?> parentById)
    {
        HashSet<string> completedIds = new(StringComparer.Ordinal);

        foreach (string startId in orderedIds)
        {
            if (completedIds.Contains(startId))
            {
                continue;
            }

            List<string> path = [];
            Dictionary<string, int> pathIndexById = new(StringComparer.Ordinal);
            string? currentId = startId;

            while (currentId is not null && parentById.ContainsKey(currentId) && !completedIds.Contains(currentId))
            {
                if (pathIndexById.TryGetValue(currentId, out int cycleStartIndex))
                {
                    parentById[path[cycleStartIndex]] = null;
                    break;
                }

                pathIndexById[currentId] = path.Count;
                path.Add(currentId);
                currentId = parentById[currentId];
            }

            foreach (string pathId in path)
            {
                completedIds.Add(pathId);
            }
        }
    }

    private static void AppendProjectionSubjects(
        IReadOnlyList<GraphTreeNode> roots,
        List<NavigationSubject> subjects,
        ref int order,
        IReadOnlyDictionary<string, GraphNode> nodesById,
        IReadOnlyDictionary<string, GraphLink> linksById,
        IReadOnlyDictionary<string, GraphGroup> containerGroupsById)
    {
        Stack<(GraphTreeNode Node, string? ParentTreeNodeId)> pending = new();
        for (int index = roots.Count - 1; index >= 0; index--)
        {
            pending.Push((roots[index], null));
        }

        while (pending.Count > 0)
        {
            (GraphTreeNode node, string? parentTreeNodeId) = pending.Pop();
            string? groupId = ResolveProjectionContainerGroupId(node, nodesById, linksById, containerGroupsById);
            subjects.Add(new NavigationSubject(
                node.TreeNodeId,
                parentTreeNodeId,
                node.Title,
                MapItemKind(node.Kind),
                GetSubtitle(node),
                groupId,
                node.NodeId,
                node.LinkId,
                order++));

            for (int index = node.Children.Count - 1; index >= 0; index--)
            {
                pending.Push((node.Children[index], node.TreeNodeId));
            }
        }
    }

    private static string? ResolveProjectionContainerGroupId(
        GraphTreeNode treeNode,
        IReadOnlyDictionary<string, GraphNode> nodesById,
        IReadOnlyDictionary<string, GraphLink> linksById,
        IReadOnlyDictionary<string, GraphGroup> containerGroupsById)
    {
        if (nodesById.TryGetValue(treeNode.NodeId, out GraphNode? node))
        {
            string? directGroupId = ResolveNodeContainerGroupId(node, containerGroupsById);
            if (directGroupId is not null)
            {
                return directGroupId;
            }
        }

        if (treeNode.LinkId is not null
            && linksById.TryGetValue(treeNode.LinkId, out GraphLink? link)
            && nodesById.TryGetValue(link.SourceNodeId, out GraphNode? sourceNode))
        {
            return ResolveNodeContainerGroupId(sourceNode, containerGroupsById);
        }

        return null;
    }

    private static string? ResolveNodeContainerGroupId(
        GraphNode node,
        IReadOnlyDictionary<string, GraphGroup> containerGroupsById)
    {
        return node.GroupMemberships.FirstOrDefault(containerGroupsById.ContainsKey);
    }

    private static GraphNavigationTreeItemKind MapItemKind(GraphTreeNodeKind kind)
    {
        return kind switch
        {
            GraphTreeNodeKind.Reference => GraphNavigationTreeItemKind.Reference,
            GraphTreeNodeKind.Terminal => GraphNavigationTreeItemKind.Terminal,
            GraphTreeNodeKind.MissingTarget => GraphNavigationTreeItemKind.MissingTarget,
            _ => GraphNavigationTreeItemKind.Node,
        };
    }

    private static string GetSubtitle(GraphTreeNode node)
    {
        return node.Kind switch
        {
            GraphTreeNodeKind.Root => node.NodeId,
            GraphTreeNodeKind.Reference => $"Reference · {node.NodeId}",
            GraphTreeNodeKind.Terminal => $"Terminal · {node.NodeId}",
            GraphTreeNodeKind.MissingTarget => $"Missing target · {node.NodeId}",
            _ => node.LinkId is null ? node.NodeId : $"{node.LinkKind}: {node.LinkId}",
        };
    }

    private static string CreateItemCountText(int itemCount)
    {
        return itemCount == 1 ? "1 item" : $"{itemCount} items";
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record NavigationSubject(
        string TreeNodeId,
        string? ParentTreeNodeId,
        string Title,
        GraphNavigationTreeItemKind Kind,
        string? Subtitle,
        string? GroupId,
        string? NodeId,
        string? LinkId,
        int Order);
}
