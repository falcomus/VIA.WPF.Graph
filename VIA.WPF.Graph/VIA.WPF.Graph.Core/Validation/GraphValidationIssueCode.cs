namespace VIA.WPF.Graph.Core.Validation;

/// <summary>
/// Defines stable codes for neutral graph validation issues.
/// </summary>
public enum GraphValidationIssueCode
{
    DuplicateNodeId = 0,
    DuplicateLinkId = 1,
    DuplicateGroupId = 2,
    MissingLinkSourceNode = 3,
    MissingLinkTargetNode = 4,
    SelfLink = 5,
    ParallelLinks = 6,
    MissingGroup = 7,
    MultipleContainerGroupsForNode = 8,
    MissingParentGroup = 9,
    InvalidParentGroupKind = 10,
    MarkerGroupHasParent = 11,
    GroupParentSelfReference = 12,
    GroupParentCycle = 13,
}
