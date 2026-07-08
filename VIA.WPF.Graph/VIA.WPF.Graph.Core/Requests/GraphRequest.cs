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
        bool? isGroupCollapsed = null,
        string? sourceNodeId = null,
        string? targetNodeId = null,
        string? title = null)
    {
        Kind = kind;
        NodeId = NormalizeOptionalText(nodeId);
        LinkId = NormalizeOptionalText(linkId);
        GroupId = NormalizeOptionalText(groupId);
        IsMultiSelection = isMultiSelection;
        IsGroupCollapsed = isGroupCollapsed;
        SourceNodeId = NormalizeOptionalText(sourceNodeId);
        TargetNodeId = NormalizeOptionalText(targetNodeId);
        Title = NormalizeOptionalText(title);
        Validate();
    }

    public GraphRequestKind Kind { get; }

    public string? NodeId { get; }

    public string? LinkId { get; }

    public string? GroupId { get; }

    public bool IsMultiSelection { get; }

    public bool? IsGroupCollapsed { get; }

    public string? SourceNodeId { get; }

    public string? TargetNodeId { get; }

    public string? Title { get; }

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

    public static GraphRequest CreateNode(string nodeId, string title, string? groupId = null)
    {
        return new GraphRequest(GraphRequestKind.CreateNode, nodeId: nodeId, groupId: groupId, title: title);
    }

    public static GraphRequest CreateLink(string linkId, string sourceNodeId, string targetNodeId)
    {
        return new GraphRequest(GraphRequestKind.CreateLink, linkId: linkId, sourceNodeId: sourceNodeId, targetNodeId: targetNodeId);
    }

    public static GraphRequest RetargetLink(string linkId, string? sourceNodeId = null, string? targetNodeId = null)
    {
        return new GraphRequest(GraphRequestKind.RetargetLink, linkId: linkId, sourceNodeId: sourceNodeId, targetNodeId: targetNodeId);
    }

    public static GraphRequest DeleteNode(string nodeId)
    {
        return new GraphRequest(GraphRequestKind.DeleteNode, nodeId: nodeId);
    }

    public static GraphRequest DeleteLink(string linkId)
    {
        return new GraphRequest(GraphRequestKind.DeleteLink, linkId: linkId);
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
            case GraphRequestKind.DeleteNode:
                RequireOnlyNodeId();
                RequireNoGroupCollapsedState();
                RequireNoEditPayload();
                break;
            case GraphRequestKind.SelectLink:
            case GraphRequestKind.OpenLink:
            case GraphRequestKind.DeleteLink:
                RequireOnlyLinkId();
                RequireNoGroupCollapsedState();
                RequireNoEditPayload();
                break;
            case GraphRequestKind.SelectGroup:
            case GraphRequestKind.OpenGroup:
                RequireOnlyGroupId();
                RequireNoGroupCollapsedState();
                RequireNoEditPayload();
                break;
            case GraphRequestKind.SetGroupCollapsed:
                RequireOnlyGroupId();
                RequireGroupCollapsedState();
                RequireNoEditPayload();
                break;
            case GraphRequestKind.ClearSelection:
            case GraphRequestKind.ReturnToOverview:
                RequireNoSubjectId();
                RequireNoGroupCollapsedState();
                RequireNoEditPayload();
                break;
            case GraphRequestKind.CreateNode:
                RequireCreateNodePayload();
                RequireNoGroupCollapsedState();
                break;
            case GraphRequestKind.CreateLink:
                RequireCreateLinkPayload();
                RequireNoGroupCollapsedState();
                break;
            case GraphRequestKind.RetargetLink:
                RequireRetargetLinkPayload();
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

    private void RequireNoEditPayload()
    {
        if (SourceNodeId is not null || TargetNodeId is not null || Title is not null)
        {
            throw new ArgumentException("This graph request kind must not contain edit payload.");
        }
    }

    private void RequireCreateNodePayload()
    {
        if (NodeId is null || Title is null || LinkId is not null || SourceNodeId is not null || TargetNodeId is not null)
        {
            throw new ArgumentException("CreateNode requires a node id and title and may optionally contain a target group id.");
        }
    }

    private void RequireCreateLinkPayload()
    {
        if (LinkId is null || SourceNodeId is null || TargetNodeId is null || NodeId is not null || GroupId is not null || Title is not null)
        {
            throw new ArgumentException("CreateLink requires a link id, source node id and target node id.");
        }
    }

    private void RequireRetargetLinkPayload()
    {
        if (LinkId is null || NodeId is not null || GroupId is not null || Title is not null || (SourceNodeId is null && TargetNodeId is null))
        {
            throw new ArgumentException("RetargetLink requires a link id and at least one new endpoint id.");
        }
    }
}
