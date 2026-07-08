using VIA.WPF.Graph.Core.Model;

namespace VIA.WPF.Graph.Core.Validation;

/// <summary>
/// Validates neutral graph documents without WPF, Graphviz, UserFlow or host dependencies.
/// </summary>
public static class GraphDocumentValidator
{
    public static GraphValidationResult Validate(GraphDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<GraphValidationIssue> issues = [];

        ValidateDuplicateIds(
            document.Nodes.Select(node => node.Id),
            GraphValidationIssueCode.DuplicateNodeId,
            "Duplicate node id.",
            issues);
        ValidateDuplicateIds(
            document.Links.Select(link => link.Id),
            GraphValidationIssueCode.DuplicateLinkId,
            "Duplicate link id.",
            issues);
        ValidateDuplicateIds(
            document.Groups.Select(group => group.Id),
            GraphValidationIssueCode.DuplicateGroupId,
            "Duplicate group id.",
            issues);

        Dictionary<string, GraphNode> nodesById = document.Nodes
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        Dictionary<string, GraphGroup> groupsById = document.Groups
            .GroupBy(group => group.Id, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        ValidateLinks(document.Links, nodesById, issues);
        ValidateNodeGroupMemberships(document.Nodes, groupsById, issues);
        ValidateGroupHierarchy(document.Groups, groupsById, issues);

        return issues.Count == 0
            ? GraphValidationResult.Success
            : new GraphValidationResult(issues);
    }

    private static void ValidateDuplicateIds(
        IEnumerable<string> ids,
        GraphValidationIssueCode issueCode,
        string message,
        List<GraphValidationIssue> issues)
    {
        IEnumerable<string> duplicateIds = ids
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);

        foreach (string duplicateId in duplicateIds)
        {
            issues.Add(new GraphValidationIssue(
                issueCode,
                GraphValidationSeverity.Error,
                message,
                duplicateId));
        }
    }

    private static void ValidateLinks(
        IReadOnlyList<GraphLink> links,
        IReadOnlyDictionary<string, GraphNode> nodesById,
        List<GraphValidationIssue> issues)
    {
        foreach (GraphLink link in links)
        {
            if (!nodesById.ContainsKey(link.SourceNodeId))
            {
                issues.Add(new GraphValidationIssue(
                    GraphValidationIssueCode.MissingLinkSourceNode,
                    GraphValidationSeverity.Error,
                    $"Link '{link.Id}' references missing source node '{link.SourceNodeId}'.",
                    link.Id,
                    link.SourceNodeId));
            }

            if (!nodesById.ContainsKey(link.TargetNodeId))
            {
                issues.Add(new GraphValidationIssue(
                    GraphValidationIssueCode.MissingLinkTargetNode,
                    GraphValidationSeverity.Error,
                    $"Link '{link.Id}' references missing target node '{link.TargetNodeId}'.",
                    link.Id,
                    link.TargetNodeId));
            }

            if (StringComparer.Ordinal.Equals(link.SourceNodeId, link.TargetNodeId))
            {
                issues.Add(new GraphValidationIssue(
                    GraphValidationIssueCode.SelfLink,
                    GraphValidationSeverity.Warning,
                    $"Link '{link.Id}' is a self-link and should be treated as a diagnostic case.",
                    link.Id,
                    link.SourceNodeId));
            }
        }

        IEnumerable<IGrouping<(string SourceNodeId, string TargetNodeId, GraphLinkDirection Direction), GraphLink>> parallelGroups = links
            .GroupBy(link => (link.SourceNodeId, link.TargetNodeId, link.Direction))
            .Where(group => group.Count() > 1);

        foreach (IGrouping<(string SourceNodeId, string TargetNodeId, GraphLinkDirection Direction), GraphLink> parallelGroup in parallelGroups)
        {
            string linkIds = string.Join(", ", parallelGroup.Select(link => link.Id));
            issues.Add(new GraphValidationIssue(
                GraphValidationIssueCode.ParallelLinks,
                GraphValidationSeverity.Info,
                $"Parallel links are present between '{parallelGroup.Key.SourceNodeId}' and '{parallelGroup.Key.TargetNodeId}': {linkIds}.",
                parallelGroup.Key.SourceNodeId,
                parallelGroup.Key.TargetNodeId));
        }
    }

    private static void ValidateNodeGroupMemberships(
        IReadOnlyList<GraphNode> nodes,
        IReadOnlyDictionary<string, GraphGroup> groupsById,
        List<GraphValidationIssue> issues)
    {
        foreach (GraphNode node in nodes)
        {
            List<GraphGroup> containerGroups = [];

            foreach (string groupId in node.GroupMemberships)
            {
                if (!groupsById.TryGetValue(groupId, out GraphGroup? group))
                {
                    issues.Add(new GraphValidationIssue(
                        GraphValidationIssueCode.MissingGroup,
                        GraphValidationSeverity.Error,
                        $"Node '{node.Id}' references missing group '{groupId}'.",
                        node.Id,
                        groupId));
                    continue;
                }

                if (group.Kind == GraphGroupKind.Container)
                {
                    containerGroups.Add(group);
                }
            }

            if (containerGroups.Count > 1)
            {
                string groupIds = string.Join(", ", containerGroups.Select(group => group.Id));
                issues.Add(new GraphValidationIssue(
                    GraphValidationIssueCode.MultipleContainerGroupsForNode,
                    GraphValidationSeverity.Error,
                    $"Node '{node.Id}' belongs to multiple container groups: {groupIds}.",
                    node.Id));
            }
        }
    }

    private static void ValidateGroupHierarchy(
        IReadOnlyList<GraphGroup> groups,
        IReadOnlyDictionary<string, GraphGroup> groupsById,
        List<GraphValidationIssue> issues)
    {
        foreach (GraphGroup group in groups)
        {
            if (group.ParentGroupId is null)
            {
                continue;
            }

            if (group.Kind == GraphGroupKind.Marker)
            {
                issues.Add(new GraphValidationIssue(
                    GraphValidationIssueCode.MarkerGroupHasParent,
                    GraphValidationSeverity.Error,
                    $"Marker group '{group.Id}' must not define parent group '{group.ParentGroupId}'.",
                    group.Id,
                    group.ParentGroupId));
            }

            if (StringComparer.Ordinal.Equals(group.Id, group.ParentGroupId))
            {
                issues.Add(new GraphValidationIssue(
                    GraphValidationIssueCode.GroupParentSelfReference,
                    GraphValidationSeverity.Error,
                    $"Group '{group.Id}' must not reference itself as parent.",
                    group.Id,
                    group.ParentGroupId));
                continue;
            }

            if (!groupsById.TryGetValue(group.ParentGroupId, out GraphGroup? parentGroup))
            {
                issues.Add(new GraphValidationIssue(
                    GraphValidationIssueCode.MissingParentGroup,
                    GraphValidationSeverity.Error,
                    $"Group '{group.Id}' references missing parent group '{group.ParentGroupId}'.",
                    group.Id,
                    group.ParentGroupId));
                continue;
            }

            if (parentGroup.Kind != GraphGroupKind.Container)
            {
                issues.Add(new GraphValidationIssue(
                    GraphValidationIssueCode.InvalidParentGroupKind,
                    GraphValidationSeverity.Error,
                    $"Group '{group.Id}' references non-container parent group '{parentGroup.Id}'.",
                    group.Id,
                    parentGroup.Id));
            }
        }

        ValidateGroupParentCycles(groups, groupsById, issues);
    }

    private static void ValidateGroupParentCycles(
        IReadOnlyList<GraphGroup> groups,
        IReadOnlyDictionary<string, GraphGroup> groupsById,
        List<GraphValidationIssue> issues)
    {
        foreach (GraphGroup group in groups)
        {
            HashSet<string> visitedGroupIds = new(StringComparer.Ordinal);
            GraphGroup current = group;

            while (current.ParentGroupId is not null && groupsById.TryGetValue(current.ParentGroupId, out GraphGroup? parent))
            {
                if (!visitedGroupIds.Add(current.Id))
                {
                    issues.Add(new GraphValidationIssue(
                        GraphValidationIssueCode.GroupParentCycle,
                        GraphValidationSeverity.Error,
                        $"Group '{group.Id}' is part of a parent group cycle.",
                        group.Id));
                    break;
                }

                current = parent;
            }
        }
    }
}
