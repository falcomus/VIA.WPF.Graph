namespace VIA.WPF.Graph.Core.Requests;

/// <summary>
/// Represents one neutral graph interaction request sent to a host command.
/// </summary>
public sealed record GraphRequest
{
    public GraphRequest(
        GraphRequestKind kind,
        string? nodeId = null,
        string? linkId = null,
        string? groupId = null,
        bool isMultiSelection = false,
        bool? isGroupCollapsed = null)
    {
        Kind = kind;
        NodeId = NormalizeOptionalText(nodeId);
        LinkId = NormalizeOptionalText(linkId);
        GroupId = NormalizeOptionalText(groupId);
        IsMultiSelection = isMultiSelection;
        IsGroupCollapsed = isGroupCollapsed;
        Validate();
    }

    public GraphRequestKind Kind { get; }

    public string? NodeId { get; }

    public string? LinkId { get; }

    public string? GroupId { get; }

    public bool IsMultiSelection { get; }

    public bool? IsGroupCollapsed { get; }

    public static GraphRequest SelectNode(string nodeId, bool isMultiSelection = false)
    {
        return new GraphRequest(GraphRequestKind.SelectNode, nodeId: nodeId, isMultiSelection: isMultiSelection);
    }

    public static GraphRequest SelectLink(string linkId, bool isMultiSelection = false)
    {
        return new GraphRequest(GraphRequestKind.SelectLink, linkId: linkId, isMultiSelection: isMultiSelection);
    }

    public static GraphRequest SelectGroup(string groupId, bool isMultiSelection = false)
    {
        return new GraphRequest(GraphRequestKind.SelectGroup, groupId: groupId, isMultiSelection: isMultiSelection);
    }

    public static GraphRequest ClearSelection()
    {
        return new GraphRequest(GraphRequestKind.ClearSelection);
    }

    public static GraphRequest OpenNode(string nodeId)
    {
        return new GraphRequest(GraphRequestKind.OpenNode, nodeId: nodeId);
    }

    public static GraphRequest OpenLink(string linkId)
    {
        return new GraphRequest(GraphRequestKind.OpenLink, linkId: linkId);
    }

    public static GraphRequest OpenGroup(string groupId)
    {
        return new GraphRequest(GraphRequestKind.OpenGroup, groupId: groupId);
    }

    public static GraphRequest ReturnToOverview()
    {
        return new GraphRequest(GraphRequestKind.ReturnToOverview);
    }

    public static GraphRequest SetGroupCollapsed(string groupId, bool isCollapsed)
    {
        return new GraphRequest(GraphRequestKind.SetGroupCollapsed, groupId: groupId, isGroupCollapsed: isCollapsed);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private void Validate()
    {
        switch (Kind)
        {
            case GraphRequestKind.SelectNode:
            case GraphRequestKind.OpenNode:
                RequireOnlyNodeId();
                RequireNoGroupCollapsedState();
                break;
            case GraphRequestKind.SelectLink:
            case GraphRequestKind.OpenLink:
                RequireOnlyLinkId();
                RequireNoGroupCollapsedState();
                break;
            case GraphRequestKind.SelectGroup:
            case GraphRequestKind.OpenGroup:
                RequireOnlyGroupId();
                RequireNoGroupCollapsedState();
                break;
            case GraphRequestKind.SetGroupCollapsed:
                RequireOnlyGroupId();
                RequireGroupCollapsedState();
                break;
            case GraphRequestKind.ClearSelection:
            case GraphRequestKind.ReturnToOverview:
                RequireNoSubjectId();
                RequireNoGroupCollapsedState();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unsupported graph request kind.");
        }
    }

    private void RequireOnlyNodeId()
    {
        if (NodeId is null || LinkId is not null || GroupId is not null)
        {
            throw new ArgumentException("This graph request kind requires exactly one node id.");
        }
    }

    private void RequireOnlyLinkId()
    {
        if (LinkId is null || NodeId is not null || GroupId is not null)
        {
            throw new ArgumentException("This graph request kind requires exactly one link id.");
        }
    }

    private void RequireOnlyGroupId()
    {
        if (GroupId is null || NodeId is not null || LinkId is not null)
        {
            throw new ArgumentException("This graph request kind requires exactly one group id.");
        }
    }

    private void RequireNoSubjectId()
    {
        if (NodeId is not null || LinkId is not null || GroupId is not null)
        {
            throw new ArgumentException("This graph request kind must not contain a node, link or group id.");
        }
    }

    private void RequireGroupCollapsedState()
    {
        if (IsGroupCollapsed is null)
        {
            throw new ArgumentException("This graph request kind requires a group collapsed state.");
        }
    }

    private void RequireNoGroupCollapsedState()
    {
        if (IsGroupCollapsed is not null)
        {
            throw new ArgumentException("This graph request kind must not contain a group collapsed state.");
        }
    }
}
