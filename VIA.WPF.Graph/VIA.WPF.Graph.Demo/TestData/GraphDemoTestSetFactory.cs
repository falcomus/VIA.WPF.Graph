using VIA.WPF.Graph.Core.Model;

namespace VIA.WPF.Graph.Demo.TestData;

public static class GraphDemoTestSetFactory
{
    public static IReadOnlyList<GraphDemoTestSet> CreateAll()
    {
        return
        [
            CreateSmall(),
            CreateMedium(),
            CreateLarge()
        ];
    }

    public static GraphDemoTestSet CreateSmall()
    {
        GraphGroup[] groups =
        [
            new("entry", "Entry", GraphGroupKind.Container),
            new("checkout", "Checkout", GraphGroupKind.Container),
            new("happy_path", "Happy path", GraphGroupKind.Marker)
        ];

        GraphNode[] nodes =
        [
            CreateNode("start", "Start", "entry", "happy_path"),
            CreateNode("choose_path", "Choose path", "entry", "happy_path"),
            CreateNode("details", "Details", "entry", "happy_path"),
            CreateNode("help_popup", "Help popup", "entry", kind: GraphNodeKind.Popup, size: GraphSize.Popup),
            CreateNode("confirm", "Confirm", "checkout", "happy_path"),
            CreateNode("payment", "Payment", "checkout"),
            CreateNode("finish", "Finish", "checkout", "happy_path"),
            CreateNode("cancel", "Cancel", "checkout"),
            CreateNode("retry", "Retry", "entry")
        ];

        GraphLink[] links =
        [
            new("start_choose_path", "start", "choose_path", kind: GraphLinkKind.Primary),
            new("choose_path_details", "choose_path", "details", kind: GraphLinkKind.Primary),
            new("details_help_popup", "details", "help_popup", kind: GraphLinkKind.PopupOpen, lineStyle: GraphLineStyle.Dashed),
            new("help_popup_confirm", "help_popup", "confirm", kind: GraphLinkKind.PopupClose, lineStyle: GraphLineStyle.Dashed),
            new("details_confirm", "details", "confirm", kind: GraphLinkKind.Primary),
            new("confirm_payment", "confirm", "payment", kind: GraphLinkKind.Secondary),
            new("payment_finish", "payment", "finish", kind: GraphLinkKind.Primary),
            new("confirm_finish", "confirm", "finish", kind: GraphLinkKind.Primary),
            new("confirm_cancel", "confirm", "cancel", kind: GraphLinkKind.Cancel, lineStyle: GraphLineStyle.Dashed),
            new("cancel_retry", "cancel", "retry", kind: GraphLinkKind.Secondary),
            new("retry_choose_path_back", "retry", "choose_path", kind: GraphLinkKind.Back, lineStyle: GraphLineStyle.Dashed)
        ];

        return new GraphDemoTestSet(
            "Small",
            "9 nodes, one popup and one back route. Used for quick visual regression checks.",
            new GraphDocument("demo-small", nodes, links, groups: groups),
            GraphViewMode.Overview,
            1d);
    }

    public static GraphDemoTestSet CreateMedium()
    {
        List<GraphGroup> groups =
        [
            new("onboarding", "Onboarding", GraphGroupKind.Container),
            new("account", "Account", GraphGroupKind.Container),
            new("workspace", "Workspace", GraphGroupKind.Container),
            new("reporting", "Reporting", GraphGroupKind.Container),
            new("handover", "Handover", GraphGroupKind.Container),
            new("critical", "Critical", GraphGroupKind.Marker),
            new("review", "Review", GraphGroupKind.Marker)
        ];

        string[] containerGroupIds = ["onboarding", "account", "workspace", "reporting", "handover"];
        List<GraphNode> nodes = [];
        List<GraphLink> links = [];

        for (int areaIndex = 0; areaIndex < containerGroupIds.Length; areaIndex++)
        {
            string groupId = containerGroupIds[areaIndex];
            for (int nodeIndex = 1; nodeIndex <= 6; nodeIndex++)
            {
                string nodeId = $"{groupId}_{nodeIndex:D2}";
                List<string> memberships = [groupId];
                if (nodeIndex is 1 or 2)
                {
                    memberships.Add("critical");
                }

                if (nodeIndex is 4 or 5)
                {
                    memberships.Add("review");
                }

                GraphNodeKind kind = nodeIndex == 5 && areaIndex % 2 == 0
                    ? GraphNodeKind.Popup
                    : GraphNodeKind.Standard;
                GraphSize size = kind == GraphNodeKind.Popup ? GraphSize.Popup : GraphSize.Standard;
                nodes.Add(new GraphNode(nodeId, ToTitle(groupId, nodeIndex), kind: kind, defaultSize: size, groupMemberships: memberships));

                if (nodeIndex > 1)
                {
                    links.Add(new GraphLink(
                        $"{groupId}_{nodeIndex - 1:D2}_{groupId}_{nodeIndex:D2}",
                        $"{groupId}_{nodeIndex - 1:D2}",
                        nodeId,
                        kind: nodeIndex == 5 && kind == GraphNodeKind.Popup ? GraphLinkKind.PopupOpen : GraphLinkKind.Primary,
                        lineStyle: kind == GraphNodeKind.Popup ? GraphLineStyle.Dashed : GraphLineStyle.Solid));
                }
            }
        }

        nodes.Add(new GraphNode("external_docs", "External docs", kind: GraphNodeKind.External, defaultSize: GraphSize.Stub));
        nodes.Add(new GraphNode("external_support", "External support", kind: GraphNodeKind.External, defaultSize: GraphSize.Stub));

        links.Add(new GraphLink("onboarding_to_account", "onboarding_06", "account_01", kind: GraphLinkKind.Primary));
        links.Add(new GraphLink("account_to_workspace", "account_06", "workspace_01", kind: GraphLinkKind.Primary));
        links.Add(new GraphLink("workspace_to_reporting", "workspace_06", "reporting_01", kind: GraphLinkKind.Primary));
        links.Add(new GraphLink("reporting_to_handover", "reporting_06", "handover_01", kind: GraphLinkKind.Primary));
        links.Add(new GraphLink("workspace_to_account_back", "workspace_03", "account_02", kind: GraphLinkKind.Back, lineStyle: GraphLineStyle.Dashed));
        links.Add(new GraphLink("handover_to_reporting_back", "handover_04", "reporting_02", kind: GraphLinkKind.Back, lineStyle: GraphLineStyle.Dashed));
        links.Add(new GraphLink("reporting_external_docs", "reporting_03", "external_docs", kind: GraphLinkKind.External, lineStyle: GraphLineStyle.Dotted));
        links.Add(new GraphLink("handover_external_support", "handover_05", "external_support", kind: GraphLinkKind.External, lineStyle: GraphLineStyle.Dotted));
        links.Add(new GraphLink("onboarding_skip_workspace", "onboarding_03", "workspace_02", kind: GraphLinkKind.Secondary));
        links.Add(new GraphLink("account_review_reporting", "account_04", "reporting_04", kind: GraphLinkKind.Secondary));

        return new GraphDemoTestSet(
            "Medium",
            "32 nodes with five areas, popup nodes, cross-area links and external transitions.",
            new GraphDocument("demo-medium", nodes, links, groups: groups),
            GraphViewMode.Overview,
            0.9d);
    }

    public static GraphDemoTestSet CreateLarge()
    {
        List<GraphGroup> groups = [];
        List<GraphNode> nodes = [];
        List<GraphLink> links = [];

        for (int areaIndex = 1; areaIndex <= 15; areaIndex++)
        {
            string groupId = $"area_{areaIndex:D2}";
            groups.Add(new GraphGroup(groupId, $"Area {areaIndex:D2}", GraphGroupKind.Container));
        }

        groups.Add(new GraphGroup("critical", "Critical", GraphGroupKind.Marker));
        groups.Add(new GraphGroup("review", "Review", GraphGroupKind.Marker));
        groups.Add(new GraphGroup("externalized", "Externalized", GraphGroupKind.Marker));

        for (int areaIndex = 1; areaIndex <= 15; areaIndex++)
        {
            string groupId = $"area_{areaIndex:D2}";
            for (int nodeIndex = 1; nodeIndex <= 8; nodeIndex++)
            {
                string nodeId = $"a{areaIndex:D2}_n{nodeIndex:D2}";
                List<string> memberships = [groupId];
                if (nodeIndex is 1 or 2)
                {
                    memberships.Add("critical");
                }

                if ((areaIndex + nodeIndex) % 4 == 0)
                {
                    memberships.Add("review");
                }

                if (areaIndex % 5 == 0 && nodeIndex is 6 or 7)
                {
                    memberships.Add("externalized");
                }

                bool isPopup = nodeIndex == 5 && areaIndex % 3 == 0;
                nodes.Add(new GraphNode(
                    nodeId,
                    $"Area {areaIndex:D2} / Step {nodeIndex:D2}",
                    kind: isPopup ? GraphNodeKind.Popup : GraphNodeKind.Standard,
                    defaultSize: isPopup ? GraphSize.Popup : GraphSize.Standard,
                    groupMemberships: memberships));

                if (nodeIndex > 1)
                {
                    GraphLinkKind kind = isPopup ? GraphLinkKind.PopupOpen : GraphLinkKind.Primary;
                    links.Add(new GraphLink(
                        $"a{areaIndex:D2}_n{nodeIndex - 1:D2}_a{areaIndex:D2}_n{nodeIndex:D2}",
                        $"a{areaIndex:D2}_n{nodeIndex - 1:D2}",
                        nodeId,
                        kind: kind,
                        lineStyle: isPopup ? GraphLineStyle.Dashed : GraphLineStyle.Solid));
                }
            }
        }

        for (int areaIndex = 1; areaIndex < 15; areaIndex++)
        {
            links.Add(new GraphLink(
                $"area_{areaIndex:D2}_to_area_{areaIndex + 1:D2}",
                $"a{areaIndex:D2}_n08",
                $"a{areaIndex + 1:D2}_n01",
                kind: GraphLinkKind.Primary));
        }

        for (int areaIndex = 3; areaIndex <= 15; areaIndex += 3)
        {
            links.Add(new GraphLink(
                $"area_{areaIndex:D2}_cycle_back",
                $"a{areaIndex:D2}_n06",
                $"a{areaIndex - 2:D2}_n03",
                kind: GraphLinkKind.Back,
                lineStyle: GraphLineStyle.Dashed));
        }

        for (int areaIndex = 2; areaIndex <= 14; areaIndex += 2)
        {
            links.Add(new GraphLink(
                $"area_{areaIndex:D2}_skip_to_area_{areaIndex + 1:D2}",
                $"a{areaIndex:D2}_n04",
                $"a{areaIndex + 1:D2}_n04",
                kind: GraphLinkKind.Secondary));
        }

        for (int areaIndex = 5; areaIndex <= 15; areaIndex += 5)
        {
            string externalNodeId = $"external_{areaIndex:D2}";
            nodes.Add(new GraphNode(externalNodeId, $"External {areaIndex:D2}", kind: GraphNodeKind.External, defaultSize: GraphSize.Stub, groupMemberships: ["externalized"]));
            links.Add(new GraphLink(
                $"area_{areaIndex:D2}_external",
                $"a{areaIndex:D2}_n07",
                externalNodeId,
                kind: GraphLinkKind.External,
                lineStyle: GraphLineStyle.Dotted));
        }

        return new GraphDemoTestSet(
            "Large",
            "123 nodes across 15 areas, with cycles, popup nodes, marker groups and external transitions.",
            new GraphDocument("demo-large", nodes, links, groups: groups),
            GraphViewMode.Overview,
            0.75d);
    }

    private static GraphNode CreateNode(
        string id,
        string title,
        string containerGroupId,
        string? markerGroupId = null,
        GraphNodeKind kind = GraphNodeKind.Standard,
        GraphSize? size = null)
    {
        List<string> groupMemberships = [containerGroupId];
        if (!string.IsNullOrWhiteSpace(markerGroupId))
        {
            groupMemberships.Add(markerGroupId);
        }

        return new GraphNode(id, title, kind: kind, defaultSize: size ?? GraphSize.Standard, groupMemberships: groupMemberships);
    }

    private static string ToTitle(string areaId, int nodeIndex)
    {
        string areaTitle = areaId.Replace('_', ' ');
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{char.ToUpperInvariant(areaTitle[0])}{areaTitle[1..]} {nodeIndex:D2}");
    }
}
