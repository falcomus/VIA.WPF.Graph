using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Core.Validation;

namespace VIA.WPF.Graph.Core.Tests.Validation;

public sealed class GraphDocumentValidatorTests
{
    [Fact]
    public void Validate_ReturnsSuccessForValidDocumentWithContainerAndMarkerGroup()
    {
        GraphDocument document = new(
            "valid",
            nodes:
            [
                new("start", "Start", groupMemberships: ["entry", "critical"]),
                new("main", "Main", groupMemberships: ["work", "critical"])
            ],
            links: [new("start_main", "start", "main", GraphLinkDirection.Directed, GraphLinkKind.Primary)],
            groups:
            [
                new("entry", "Entry", GraphGroupKind.Container),
                new("work", "Work", GraphGroupKind.Container),
                new("critical", "Critical", GraphGroupKind.Marker)
            ]);

        GraphValidationResult result = GraphDocumentValidator.Validate(document);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_DetectsDuplicateNodeLinkAndGroupIds()
    {
        GraphDocument document = new(
            "duplicates",
            nodes: [new("n", "Node 1"), new("n", "Node 2")],
            links: [new("l", "n", "n"), new("l", "n", "n")],
            groups: [new("g", "Group 1", GraphGroupKind.Container), new("g", "Group 2", GraphGroupKind.Marker)]);

        GraphValidationResult result = GraphDocumentValidator.Validate(document);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == GraphValidationIssueCode.DuplicateNodeId);
        Assert.Contains(result.Issues, issue => issue.Code == GraphValidationIssueCode.DuplicateLinkId);
        Assert.Contains(result.Issues, issue => issue.Code == GraphValidationIssueCode.DuplicateGroupId);
    }

    [Fact]
    public void Validate_DetectsMissingLinkTargetsAndMissingGroups()
    {
        GraphDocument document = new(
            "missing",
            nodes: [new("start", "Start", groupMemberships: ["missing-group"])],
            links: [new("start_missing", "start", "missing-node")]);

        GraphValidationResult result = GraphDocumentValidator.Validate(document);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == GraphValidationIssueCode.MissingLinkTargetNode);
        Assert.Contains(result.Issues, issue => issue.Code == GraphValidationIssueCode.MissingGroup);
    }

    [Fact]
    public void Validate_ReportsSelfLinksAsWarningAndParallelLinksAsInfo()
    {
        GraphDocument document = new(
            "diagnostics",
            nodes: [new("start", "Start"), new("main", "Main")],
            links:
            [
                new("start_self", "start", "start"),
                new("start_main_1", "start", "main"),
                new("start_main_2", "start", "main")
            ]);

        GraphValidationResult result = GraphDocumentValidator.Validate(document);

        Assert.True(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == GraphValidationIssueCode.SelfLink
            && issue.Severity == GraphValidationSeverity.Warning);
        Assert.Contains(result.Issues, issue =>
            issue.Code == GraphValidationIssueCode.ParallelLinks
            && issue.Severity == GraphValidationSeverity.Info);
    }

    [Fact]
    public void Validate_DetectsInvalidContainerAndMarkerGroupRules()
    {
        GraphDocument document = new(
            "groups",
            nodes: [new("node", "Node", groupMemberships: ["container-a", "container-b"])],
            groups:
            [
                new("container-a", "Container A", GraphGroupKind.Container, parentGroupId: "container-b"),
                new("container-b", "Container B", GraphGroupKind.Container, parentGroupId: "container-a"),
                new("marker", "Marker", GraphGroupKind.Marker, parentGroupId: "container-a")
            ]);

        GraphValidationResult result = GraphDocumentValidator.Validate(document);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == GraphValidationIssueCode.MultipleContainerGroupsForNode);
        Assert.Contains(result.Issues, issue => issue.Code == GraphValidationIssueCode.MarkerGroupHasParent);
        Assert.Contains(result.Issues, issue => issue.Code == GraphValidationIssueCode.GroupParentCycle);
    }
}
