using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Core.Requests;
using VIA.WPF.Graph.Graphviz.Layout;

namespace VIA.WPF.Graph.Demo.ViewModels;

public partial class UIXDemoViewModel : ObservableObject
{
    private readonly IReadOnlyDictionary<string, DemoScreenInfo> screenInfoByNodeId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountsText))]
    [NotifyPropertyChangedFor(nameof(FooterText))]
    private GraphDocument currentDocument;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeText))]
    [NotifyPropertyChangedFor(nameof(CurrentScopeText))]
    [NotifyPropertyChangedFor(nameof(FooterText))]
    private GraphViewState workspaceViewState = GraphViewState.Default;

    [ObservableProperty]
    private GraphLayoutDirection layoutDirection = GraphLayoutDirection.LeftToRight;

    [ObservableProperty]
    private GraphEdgeRoutingStyle edgeRoutingStyle = GraphEdgeRoutingStyle.Spline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DensityButtonText))]
    private double visualDensity = 1d;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedScreenTitle))]
    [NotifyPropertyChangedFor(nameof(SelectedScreenSubtitle))]
    [NotifyPropertyChangedFor(nameof(SelectedPathText))]
    [NotifyPropertyChangedFor(nameof(SelectedScreenAreaText))]
    [NotifyPropertyChangedFor(nameof(SelectedScreenKindText))]
    [NotifyPropertyChangedFor(nameof(SelectedIncomingLinksText))]
    [NotifyPropertyChangedFor(nameof(SelectedOutgoingLinksText))]
    [NotifyPropertyChangedFor(nameof(FooterText))]
    private IReadOnlyList<string> selectedNodeIds = Array.Empty<string>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FooterText))]
    private IReadOnlyList<string> selectedLinkIds = Array.Empty<string>();

    [ObservableProperty]
    private IReadOnlyList<string> selectedGroupIds = Array.Empty<string>();

    [ObservableProperty]
    private string? selectedNodeId;

    [ObservableProperty]
    private string? selectedLinkId;

    [ObservableProperty]
    private string? focusedNodeId;

    [ObservableProperty]
    private string? focusedGroupId;

    public UIXDemoViewModel()
    {
        CurrentDocument = CreateDemoDocument();
        screenInfoByNodeId = CurrentDocument.Nodes.ToDictionary(
            node => node.Id,
            node => CreateScreenInfo(node),
            StringComparer.Ordinal);
        LayoutEngine = new GraphvizDemoLayoutEngine();
        HostCapabilities = GraphHostCapabilities.ReadOnly();
        GraphRequestCommand = new RelayCommand<GraphRequest>(HandleGraphRequest);
        SetWorkspaceSelection("splash", GraphViewMode.Focus);
    }

    public IGraphLayoutEngine LayoutEngine { get; }

    public GraphHostCapabilities HostCapabilities { get; }

    public IRelayCommand<GraphRequest> GraphRequestCommand { get; }

    public string CountsText => $"{CurrentDocument.Nodes.Count} screens · {CurrentDocument.Links.Count} transitions · {CurrentDocument.Groups.Count} areas";

    public string DensityButtonText => VisualDensity < 1d ? "Comfort" : "Compact";

    public string CurrentScopeText
    {
        get
        {
            string scopeName = WorkspaceViewState.ActiveViewMode switch
            {
                GraphViewMode.Focus => "Focus",
                GraphViewMode.Tree => "Branch",
                GraphViewMode.Group or GraphViewMode.GroupOverview => GetSelectedAreaTitle(),
                GraphViewMode.Overview => "Overview",
                _ => "Diagnostic"
            };

            return $"{scopeName} · {GetSelectedScopeSubjectText()}";
        }
    }

    public string ModeText => WorkspaceViewState.ActiveViewMode switch
    {
        GraphViewMode.Group or GraphViewMode.GroupOverview => "Group Compact shows a limited, readable subset of the selected area. Full overview is explicit.",
        GraphViewMode.Focus => "Focus shows the selected screen and direct navigation context.",
        GraphViewMode.Tree => "Branch shows the selected screen, local path and direct alternatives.",
        GraphViewMode.Overview => "Overview shows the complete graph intentionally.",
        _ => "Diagnostic view shows the technical graph structure."
    };

    public string SelectedScreenTitle => GetSelectedScreenInfo().Title;

    public string SelectedScreenSubtitle => GetSelectedScreenInfo().Subtitle;

    public string SelectedPathText => GetSelectedScreenInfo().PathHint;

    public string SelectedScreenAreaText => GetSelectedScreenInfo().Area;

    public string SelectedScreenKindText
    {
        get
        {
            string? nodeId = GetSelectedNodeIdOrNull();
            GraphNode? node = nodeId is null ? null : CurrentDocument.Nodes.FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Id, nodeId));
            DemoScreenInfo info = GetSelectedScreenInfo();
            return node is null ? info.Role : $"{node.Kind} · {info.Role}";
        }
    }

    public string SelectedIncomingLinksText => DescribeLinks(GetIncomingLinks(), incoming: true);

    public string SelectedOutgoingLinksText => DescribeLinks(GetOutgoingLinks(), incoming: false);

    public string FooterText => $"{CountsText} · Scope {CurrentScopeText} · Selection {SelectedScreenTitle}";

    partial void OnWorkspaceViewStateChanged(GraphViewState value)
    {
        ApplyWorkspaceViewState(value);
    }

    private void HandleGraphRequest(GraphRequest? request)
    {
        if (request is null)
        {
            return;
        }

        switch (request.Kind)
        {
            case GraphRequestKind.SelectNode:
            case GraphRequestKind.OpenNode:
                GraphViewMode nextMode = WorkspaceViewState.ActiveViewMode == GraphViewMode.Tree
                    ? GraphViewMode.Tree
                    : GraphViewMode.Focus;
                SetWorkspaceSelection(request.NodeId, nextMode);
                break;

            case GraphRequestKind.SelectLink:
            case GraphRequestKind.OpenLink:
                SelectLink(request.LinkId);
                break;

            case GraphRequestKind.SelectGroup:
            case GraphRequestKind.OpenGroup:
                SelectGroup(request.GroupId);
                break;

            case GraphRequestKind.ClearSelection:
                ClearSelection();
                break;

            case GraphRequestKind.ReturnToOverview:
                SetWorkspaceSelection(GetSelectedNodeIdOrNull() ?? "splash", GraphViewMode.Overview);
                break;
        }
    }

    private void SetWorkspaceSelection(string? nodeId, GraphViewMode viewMode)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        string? groupId = viewMode is GraphViewMode.Group or GraphViewMode.GroupOverview
            ? GetContainerGroupIdOrNull(nodeId)
            : null;

        WorkspaceViewState = new GraphViewState(
            viewMode,
            nodeId,
            groupId,
            new GraphSelectionState(selectedNodeIds: [nodeId]),
            WorkspaceViewState.Viewport,
            WorkspaceViewState.CollapsedContainerGroupIds,
            WorkspaceViewState.ExpandedTreeItemIds);
    }

    private void SelectLink(string? linkId)
    {
        if (string.IsNullOrWhiteSpace(linkId))
        {
            return;
        }

        GraphLink? link = CurrentDocument.Links.FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Id, linkId));
        string? activeNodeId = WorkspaceViewState.ActiveNodeId ?? link?.SourceNodeId ?? GetSelectedNodeIdOrNull();
        WorkspaceViewState = new GraphViewState(
            WorkspaceViewState.ActiveViewMode == GraphViewMode.Overview ? GraphViewMode.Overview : GraphViewMode.Focus,
            activeNodeId,
            null,
            new GraphSelectionState(selectedLinkIds: [linkId]),
            WorkspaceViewState.Viewport,
            WorkspaceViewState.CollapsedContainerGroupIds,
            WorkspaceViewState.ExpandedTreeItemIds);
    }

    private void SelectGroup(string? groupId)
    {
        if (string.IsNullOrWhiteSpace(groupId))
        {
            return;
        }

        string? firstNodeId = CurrentDocument.Nodes.FirstOrDefault(node => node.GroupMemberships.Contains(groupId, StringComparer.Ordinal))?.Id;
        WorkspaceViewState = new GraphViewState(
            GraphViewMode.Group,
            firstNodeId,
            groupId,
            new GraphSelectionState(
                selectedNodeIds: string.IsNullOrWhiteSpace(firstNodeId) ? null : [firstNodeId],
                selectedGroupIds: [groupId]),
            WorkspaceViewState.Viewport,
            WorkspaceViewState.CollapsedContainerGroupIds,
            WorkspaceViewState.ExpandedTreeItemIds);
    }

    private void ClearSelection()
    {
        WorkspaceViewState = new GraphViewState(
            GraphViewMode.Overview,
            viewport: WorkspaceViewState.Viewport,
            collapsedContainerGroupIds: WorkspaceViewState.CollapsedContainerGroupIds,
            expandedTreeItemIds: WorkspaceViewState.ExpandedTreeItemIds);
    }

    private void ApplyWorkspaceViewState(GraphViewState value)
    {
        SelectedNodeIds = value.Selection.SelectedNodeIds;
        SelectedLinkIds = value.Selection.SelectedLinkIds;
        SelectedGroupIds = value.Selection.SelectedGroupIds;
        SelectedNodeId = value.ActiveNodeId ?? value.Selection.SelectedNodeIds.FirstOrDefault();
        SelectedLinkId = value.Selection.SelectedLinkIds.FirstOrDefault();
        FocusedNodeId = value.ActiveNodeId;
        FocusedGroupId = value.ActiveGroupId;
        OnPropertyChanged(nameof(CurrentScopeText));
        OnPropertyChanged(nameof(ModeText));
        OnPropertyChanged(nameof(SelectedScreenTitle));
        OnPropertyChanged(nameof(SelectedScreenSubtitle));
        OnPropertyChanged(nameof(SelectedPathText));
        OnPropertyChanged(nameof(SelectedScreenAreaText));
        OnPropertyChanged(nameof(SelectedScreenKindText));
        OnPropertyChanged(nameof(SelectedIncomingLinksText));
        OnPropertyChanged(nameof(SelectedOutgoingLinksText));
        OnPropertyChanged(nameof(FooterText));
    }

    private string GetSelectedScopeSubjectText()
    {
        string? nodeId = GetSelectedNodeIdOrNull();
        if (!string.IsNullOrWhiteSpace(nodeId))
        {
            return GetNodeTitle(nodeId);
        }

        string? groupId = WorkspaceViewState.ActiveGroupId ?? WorkspaceViewState.Selection.SelectedGroupIds.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(groupId))
        {
            return CurrentDocument.Groups.FirstOrDefault(group => StringComparer.Ordinal.Equals(group.Id, groupId))?.Title ?? groupId;
        }

        return "no active selection";
    }

    private string? GetContainerGroupIdOrNull(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        GraphNode? node = CurrentDocument.Nodes.FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Id, nodeId));
        return node?.GroupMemberships
            .FirstOrDefault(groupId => CurrentDocument.Groups.Any(group => group.Kind == GraphGroupKind.Container && StringComparer.Ordinal.Equals(group.Id, groupId)));
    }

    private string GetSelectedAreaTitle()
    {
        string? groupId = WorkspaceViewState.ActiveGroupId
            ?? WorkspaceViewState.Selection.SelectedGroupIds.FirstOrDefault()
            ?? GetContainerGroupIdOrNull(GetSelectedNodeIdOrNull());
        GraphGroup? group = string.IsNullOrWhiteSpace(groupId)
            ? null
            : CurrentDocument.Groups.FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Id, groupId));

        return group is null ? "Group" : group.Title;
    }

    private DemoScreenInfo GetSelectedScreenInfo()
    {
        string? nodeId = GetSelectedNodeIdOrNull();
        if (nodeId is not null && screenInfoByNodeId.TryGetValue(nodeId, out DemoScreenInfo? info))
        {
            return info;
        }

        return new DemoScreenInfo(
            "No screen selected",
            "Select a screen in the navigation tree or graph.",
            "The graph preview follows the current workspace selection.",
            "None",
            "No active screen");
    }

    private string? GetSelectedNodeIdOrNull()
    {
        return SelectedNodeIds.FirstOrDefault()
            ?? SelectedNodeId
            ?? WorkspaceViewState.ActiveNodeId
            ?? WorkspaceViewState.Selection.SelectedNodeIds.FirstOrDefault();
    }

    private IEnumerable<GraphLink> GetIncomingLinks()
    {
        string? nodeId = GetSelectedNodeIdOrNull();
        return nodeId is null
            ? Array.Empty<GraphLink>()
            : CurrentDocument.Links.Where(link => StringComparer.Ordinal.Equals(link.TargetNodeId, nodeId));
    }

    private IEnumerable<GraphLink> GetOutgoingLinks()
    {
        string? nodeId = GetSelectedNodeIdOrNull();
        return nodeId is null
            ? Array.Empty<GraphLink>()
            : CurrentDocument.Links.Where(link => StringComparer.Ordinal.Equals(link.SourceNodeId, nodeId));
    }

    private string DescribeLinks(IEnumerable<GraphLink> links, bool incoming)
    {
        string[] parts = links
            .Take(6)
            .Select(link => incoming
                ? $"{GetNodeTitle(link.SourceNodeId)} → {link.Label ?? link.Kind.ToString()}"
                : $"{link.Label ?? link.Kind.ToString()} → {GetNodeTitle(link.TargetNodeId)}")
            .ToArray();

        return parts.Length == 0 ? "None" : string.Join("\n", parts);
    }

    private string GetNodeTitle(string nodeId)
    {
        GraphNode? node = CurrentDocument.Nodes.FirstOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Id, nodeId));
        return node?.Title ?? nodeId;
    }

    private static DemoScreenInfo CreateScreenInfo(GraphNode node)
    {
        return node.Id switch
        {
            "splash" => new DemoScreenInfo("Splash", "Launch screen with branding, session restore and app-version check.", "Splash → Login or restored Home", "Entry", "Start"),
            "login" => new DemoScreenInfo("Login", "Email/password entry with social sign-in and recovery options.", "Login → Home, Register or Recovery", "Entry", "Authentication"),
            "register" => new DemoScreenInfo("Register", "Account creation with validation, consent and password rules.", "Register → Verify email → Onboarding", "Entry", "Registration"),
            "verify_email" => new DemoScreenInfo("Verify email", "Confirmation step before the first authenticated session.", "Register → Verify email → Onboarding", "Entry", "Verification"),
            "forgot_password" => new DemoScreenInfo("Forgot password", "Recovery flow returning safely to Login.", "Login → Forgot password → Login", "Entry", "Recovery"),
            "onboarding" => new DemoScreenInfo("Onboarding", "Preference and notification setup for first-time users.", "Onboarding → Home", "Entry", "First-run setup"),
            "home" => new DemoScreenInfo("Home", "Personalized dashboard and main navigation hub.", "Home connects discovery, shopping, account and support.", "Home", "Hub"),
            "feed" => new DemoScreenInfo("For you", "Personalized content, offers and recently viewed products.", "Home → For you → Product list or Details", "Home", "Personalized feed"),
            "search" => new DemoScreenInfo("Search", "Product discovery with query suggestions, filters and recent searches.", "Home → Search → Product list", "Home", "Search"),
            "categories" => new DemoScreenInfo("Categories", "Category browsing with curated entry points.", "Home → Categories → Product list", "Home", "Browse"),
            "campaign_landing" => new DemoScreenInfo("Campaign landing", "Promotional landing page with curated product collections.", "Home → Campaign landing → Product list", "Home", "Campaign"),
            "wishlist" => new DemoScreenInfo("Wishlist", "Saved products with quick return to product details or cart.", "Home/Product details → Wishlist", "Home", "Saved items"),
            "product_list" => new DemoScreenInfo("Product list", "Filtered product results with sorting, paging and promoted items.", "Search/Categories/Campaign → Product list → Product details", "Shop", "Result list"),
            "product_details" => new DemoScreenInfo("Product details", "Primary conversion screen with media, variants, reviews and add-to-cart.", "Product list → Product details → Cart", "Shop", "Detail"),
            "product_images" => new DemoScreenInfo("Product images", "Image gallery and zoomable media preview.", "Product details → Product images → Product details", "Shop", "Gallery"),
            "reviews" => new DemoScreenInfo("Reviews", "Customer ratings, filters and review details.", "Product details → Reviews → Product details", "Shop", "Social proof"),
            "recommendations" => new DemoScreenInfo("Recommendations", "Related products, bundles and alternative choices.", "Product details → Recommendations → Product details", "Shop", "Cross-sell"),
            "variant_selector" => new DemoScreenInfo("Variant selector", "Color, size and configuration selection before purchase.", "Product details → Variant selector → Product details", "Shop", "Configuration"),
            "availability" => new DemoScreenInfo("Availability", "Store pickup and delivery availability check.", "Product details → Availability → Cart", "Shop", "Inventory"),
            "cart" => new DemoScreenInfo("Cart", "Basket with quantities, coupons, delivery estimate and checkout readiness.", "Cart → Coupon or Address", "Checkout", "Basket"),
            "coupon" => new DemoScreenInfo("Coupon", "Promotion code entry and discount validation.", "Cart → Coupon → Cart", "Checkout", "Promotion"),
            "address" => new DemoScreenInfo("Address", "Shipping address selection, validation and edit path.", "Cart → Address → Delivery", "Checkout", "Address"),
            "delivery" => new DemoScreenInfo("Delivery", "Delivery slot, pickup method and shipping service selection.", "Address → Delivery → Payment", "Checkout", "Fulfillment"),
            "payment" => new DemoScreenInfo("Payment", "Payment method selection with optional external provider authorization.", "Delivery → Payment → Provider or Review", "Checkout", "Payment"),
            "payment_provider" => new DemoScreenInfo("Payment provider", "External payment authorization boundary.", "Payment → External provider → Payment", "External", "External"),
            "order_review" => new DemoScreenInfo("Order review", "Final order summary, terms and place-order action.", "Payment → Order review → Confirmation", "Checkout", "Review"),
            "order_confirmation" => new DemoScreenInfo("Order confirmation", "Success state with order number and next-step navigation.", "Order review → Confirmation → Home or Orders", "Checkout", "Completion"),
            "shipment_tracking" => new DemoScreenInfo("Shipment tracking", "External carrier tracking handoff.", "Order details → Shipment tracking", "External", "External"),
            "profile" => new DemoScreenInfo("Profile", "User profile, saved data and account overview.", "Home → Profile → Orders, Settings or Saved data", "Account", "Hub"),
            "orders" => new DemoScreenInfo("Orders", "Order history with status filters and recent purchases.", "Profile → Orders → Order details", "Account", "History"),
            "order_details" => new DemoScreenInfo("Order details", "Shipment status, invoice, tracking and return options.", "Orders → Order details → Returns or Tracking", "Account", "Detail"),
            "returns" => new DemoScreenInfo("Return request", "Return reason, item selection and refund method.", "Order details → Return request → Return label", "Account", "Return"),
            "return_label" => new DemoScreenInfo("Return label", "Generated return label and drop-off instructions.", "Return request → Return label → Orders", "Account", "Document"),
            "invoices" => new DemoScreenInfo("Invoices", "Invoice list and PDF download entry.", "Profile → Invoices → Order details", "Account", "Documents"),
            "account_addresses" => new DemoScreenInfo("Saved addresses", "Manage delivery and billing addresses outside checkout.", "Profile → Saved addresses → Profile", "Account", "Settings"),
            "payment_methods" => new DemoScreenInfo("Payment methods", "Manage cards, wallets and default payment options.", "Profile → Payment methods → Profile", "Account", "Settings"),
            "settings" => new DemoScreenInfo("Settings", "Notifications, privacy and app preferences.", "Profile → Settings → Profile", "Account", "Preferences"),
            "help_center" => new DemoScreenInfo("Help center", "Support hub with search, FAQ, chat and contact options.", "Home → Help center → FAQ, Chat or Contact", "Support", "Hub"),
            "faq" => new DemoScreenInfo("FAQ", "Self-service answers grouped by topic.", "Help center → FAQ → Help center", "Support", "Knowledge base"),
            "support_chat" => new DemoScreenInfo("Support chat", "Live chat or bot-assisted support conversation.", "Help center → Support chat → Ticket details", "Support", "Conversation"),
            "contact_form" => new DemoScreenInfo("Contact form", "Structured support request with category and attachments.", "Help center → Contact form → Ticket details", "Support", "Form"),
            "ticket_details" => new DemoScreenInfo("Ticket details", "Support case timeline and replies.", "Chat/Contact → Ticket details → Help center", "Support", "Case"),
            "support_popup" => new DemoScreenInfo("Support popup", "Lightweight contextual overlay for immediate help options.", "Any important screen → Support popup → Source", "Support", "Popup"),
            _ => new DemoScreenInfo(node.Title, node.Description ?? "Demo screen.", node.Id, "Demo", node.Kind.ToString())
        };
    }

    private static GraphDocument CreateDemoDocument()
    {
        GraphGroup[] groups =
        [
            new("entry", "Entry", GraphGroupKind.Container),
            new("home_area", "Home", GraphGroupKind.Container),
            new("shop", "Shop", GraphGroupKind.Container),
            new("checkout_area", "Checkout", GraphGroupKind.Container),
            new("account", "Account", GraphGroupKind.Container),
            new("support", "Support", GraphGroupKind.Container),
            new("external", "External", GraphGroupKind.Container),
            new("primary_path", "Main path", GraphGroupKind.Marker),
            new("alternate_path", "Alternative path", GraphGroupKind.Marker),
            new("recovery_path", "Recovery", GraphGroupKind.Marker),
            new("popup_path", "Popup", GraphGroupKind.Marker)
        ];

        GraphNode[] nodes =
        [
            CreateNode("splash", "Splash", "entry", "primary_path", description: "Launch and restore session", size: GraphSize.Compact),
            CreateNode("login", "Login", "entry", "primary_path", description: "Authentication", size: GraphSize.Detail),
            CreateNode("register", "Register", "entry", "alternate_path", description: "Create account"),
            CreateNode("verify_email", "Verify email", "entry", "primary_path", description: "Account confirmation"),
            CreateNode("forgot_password", "Forgot password", "entry", "recovery_path", description: "Recover access"),
            CreateNode("onboarding", "Onboarding", "entry", "primary_path", description: "First-run setup"),
            CreateNode("home", "Home", "home_area", "primary_path", description: "Dashboard and hub", size: GraphSize.Detail),
            CreateNode("feed", "For you", "home_area", description: "Personalized content"),
            CreateNode("search", "Search", "home_area", "primary_path", description: "Find products"),
            CreateNode("categories", "Categories", "home_area", "alternate_path", description: "Browse departments"),
            CreateNode("campaign_landing", "Campaign landing", "home_area", "alternate_path", description: "Seasonal promotion"),
            CreateNode("wishlist", "Wishlist", "home_area", description: "Saved products"),
            CreateNode("product_list", "Product list", "shop", "primary_path", description: "Filtered results"),
            CreateNode("product_details", "Product details", "shop", "primary_path", description: "Conversion screen", size: GraphSize.Detail),
            CreateNode("product_images", "Product images", "shop", description: "Gallery"),
            CreateNode("reviews", "Reviews", "shop", description: "Ratings"),
            CreateNode("recommendations", "Recommendations", "shop", description: "Related products"),
            CreateNode("variant_selector", "Variant selector", "shop", "primary_path", description: "Configure product"),
            CreateNode("availability", "Availability", "shop", description: "Pickup and delivery"),
            CreateNode("cart", "Cart", "checkout_area", "primary_path", description: "Selected items"),
            CreateNode("coupon", "Coupon", "checkout_area", "alternate_path", description: "Promotion code"),
            CreateNode("address", "Address", "checkout_area", "primary_path", description: "Shipping address"),
            CreateNode("delivery", "Delivery", "checkout_area", "primary_path", description: "Delivery method"),
            CreateNode("payment", "Payment", "checkout_area", "primary_path", description: "Payment method"),
            CreateNode("payment_provider", "Payment provider", "external", kind: GraphNodeKind.External, description: "External authorization", size: GraphSize.Stub),
            CreateNode("order_review", "Order review", "checkout_area", "primary_path", description: "Final check"),
            CreateNode("order_confirmation", "Order confirmation", "checkout_area", "primary_path", description: "Completion", size: GraphSize.Detail),
            CreateNode("shipment_tracking", "Shipment tracking", "external", kind: GraphNodeKind.External, description: "Carrier tracking", size: GraphSize.Stub),
            CreateNode("profile", "Profile", "account", description: "Account overview"),
            CreateNode("orders", "Orders", "account", "primary_path", description: "Order history"),
            CreateNode("order_details", "Order details", "account", "primary_path", description: "Shipment and invoice"),
            CreateNode("returns", "Return request", "account", description: "Return item"),
            CreateNode("return_label", "Return label", "account", description: "Drop-off document"),
            CreateNode("invoices", "Invoices", "account", description: "Documents"),
            CreateNode("account_addresses", "Saved addresses", "account", description: "Address book"),
            CreateNode("payment_methods", "Payment methods", "account", description: "Saved payment options"),
            CreateNode("settings", "Settings", "account", description: "Preferences"),
            CreateNode("help_center", "Help center", "support", description: "Support hub"),
            CreateNode("faq", "FAQ", "support", description: "Self-service answers"),
            CreateNode("support_chat", "Support chat", "support", description: "Conversation"),
            CreateNode("contact_form", "Contact form", "support", description: "Structured request"),
            CreateNode("ticket_details", "Ticket details", "support", description: "Support case"),
            CreateNode("support_popup", "Support popup", "support", "popup_path", kind: GraphNodeKind.Popup, description: "Contextual help overlay", size: GraphSize.Popup)
        ];

        GraphLink[] links =
        [
            new("splash_login", "splash", "login", kind: GraphLinkKind.Primary, label: "Start"),
            new("splash_home_restore", "splash", "home", kind: GraphLinkKind.Secondary, label: "Restore session", isLayoutConstraint: false),
            new("login_home", "login", "home", kind: GraphLinkKind.Primary, label: "Sign in"),
            new("login_register", "login", "register", kind: GraphLinkKind.Secondary, label: "Create account"),
            new("register_verify_email", "register", "verify_email", kind: GraphLinkKind.Primary, label: "Verify"),
            new("verify_email_onboarding", "verify_email", "onboarding", kind: GraphLinkKind.Primary, label: "Continue"),
            new("onboarding_home", "onboarding", "home", kind: GraphLinkKind.Primary, label: "Finish setup"),
            new("login_forgot_password", "login", "forgot_password", kind: GraphLinkKind.Secondary, label: "Forgot password"),
            new("forgot_password_login", "forgot_password", "login", kind: GraphLinkKind.Back, label: "Back to login", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),

            new("home_feed", "home", "feed", kind: GraphLinkKind.Primary, label: "For you"),
            new("home_search", "home", "search", kind: GraphLinkKind.Primary, label: "Search"),
            new("home_categories", "home", "categories", kind: GraphLinkKind.Secondary, label: "Categories"),
            new("home_campaign", "home", "campaign_landing", kind: GraphLinkKind.Secondary, label: "Campaign"),
            new("home_wishlist", "home", "wishlist", kind: GraphLinkKind.Secondary, label: "Wishlist"),
            new("home_profile", "home", "profile", kind: GraphLinkKind.Secondary, label: "Profile"),
            new("home_help_center", "home", "help_center", kind: GraphLinkKind.Secondary, label: "Help"),

            new("feed_product_list", "feed", "product_list", kind: GraphLinkKind.Primary, label: "Open collection"),
            new("feed_product_details", "feed", "product_details", kind: GraphLinkKind.Secondary, label: "Open featured", isLayoutConstraint: false),
            new("search_product_list", "search", "product_list", kind: GraphLinkKind.Primary, label: "Show results"),
            new("categories_product_list", "categories", "product_list", kind: GraphLinkKind.Primary, label: "Open category"),
            new("campaign_product_list", "campaign_landing", "product_list", kind: GraphLinkKind.Primary, label: "Open offer list"),
            new("product_list_product_details", "product_list", "product_details", kind: GraphLinkKind.Primary, label: "Open product"),
            new("product_details_images", "product_details", "product_images", kind: GraphLinkKind.Secondary, label: "Images"),
            new("product_images_product_details", "product_images", "product_details", kind: GraphLinkKind.Back, label: "Back to product", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("product_details_reviews", "product_details", "reviews", kind: GraphLinkKind.Secondary, label: "Reviews"),
            new("reviews_product_details", "reviews", "product_details", kind: GraphLinkKind.Back, label: "Back to product", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("product_details_recommendations", "product_details", "recommendations", kind: GraphLinkKind.Secondary, label: "Related"),
            new("recommendations_product_details", "recommendations", "product_details", kind: GraphLinkKind.Reference, label: "Open related product", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("product_details_variant", "product_details", "variant_selector", kind: GraphLinkKind.Primary, label: "Choose variant"),
            new("variant_product_details", "variant_selector", "product_details", kind: GraphLinkKind.Back, label: "Apply variant", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("product_details_availability", "product_details", "availability", kind: GraphLinkKind.Secondary, label: "Check availability"),
            new("availability_cart", "availability", "cart", kind: GraphLinkKind.Primary, label: "Add available item"),
            new("product_details_wishlist", "product_details", "wishlist", kind: GraphLinkKind.Secondary, label: "Save"),
            new("wishlist_product_details", "wishlist", "product_details", kind: GraphLinkKind.Secondary, label: "Open saved product", isLayoutConstraint: false),
            new("product_details_cart", "product_details", "cart", kind: GraphLinkKind.Primary, label: "Add to cart"),
            new("product_details_product_list", "product_details", "product_list", kind: GraphLinkKind.Back, label: "Back to list", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),

            new("cart_product_details", "cart", "product_details", kind: GraphLinkKind.Back, label: "Edit item", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("cart_coupon", "cart", "coupon", kind: GraphLinkKind.Secondary, label: "Add coupon"),
            new("coupon_cart", "coupon", "cart", kind: GraphLinkKind.Back, label: "Apply discount", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("cart_address", "cart", "address", kind: GraphLinkKind.Primary, label: "Checkout"),
            new("address_delivery", "address", "delivery", kind: GraphLinkKind.Primary, label: "Continue"),
            new("delivery_payment", "delivery", "payment", kind: GraphLinkKind.Primary, label: "Payment"),
            new("payment_provider_external", "payment", "payment_provider", kind: GraphLinkKind.External, label: "Authorize payment", lineStyle: GraphLineStyle.Dashed),
            new("payment_order_review", "payment", "order_review", kind: GraphLinkKind.Primary, label: "Review"),
            new("order_review_cart", "order_review", "cart", kind: GraphLinkKind.Back, label: "Change cart", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("order_review_confirmation", "order_review", "order_confirmation", kind: GraphLinkKind.Primary, label: "Place order"),
            new("confirmation_orders", "order_confirmation", "orders", kind: GraphLinkKind.Secondary, label: "View order", isLayoutConstraint: false),
            new("confirmation_home", "order_confirmation", "home", kind: GraphLinkKind.Secondary, label: "Back home", isLayoutConstraint: false),

            new("profile_orders", "profile", "orders", kind: GraphLinkKind.Primary, label: "Orders"),
            new("orders_order_details", "orders", "order_details", kind: GraphLinkKind.Primary, label: "Details"),
            new("order_details_tracking", "order_details", "shipment_tracking", kind: GraphLinkKind.External, label: "Track shipment", lineStyle: GraphLineStyle.Dashed),
            new("order_details_returns", "order_details", "returns", kind: GraphLinkKind.Secondary, label: "Return item"),
            new("returns_return_label", "returns", "return_label", kind: GraphLinkKind.Primary, label: "Create label"),
            new("return_label_orders", "return_label", "orders", kind: GraphLinkKind.Back, label: "Back to orders", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("profile_invoices", "profile", "invoices", kind: GraphLinkKind.Secondary, label: "Invoices"),
            new("invoices_order_details", "invoices", "order_details", kind: GraphLinkKind.Reference, label: "Open order", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("profile_addresses", "profile", "account_addresses", kind: GraphLinkKind.Secondary, label: "Addresses"),
            new("addresses_profile", "account_addresses", "profile", kind: GraphLinkKind.Back, label: "Back to profile", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("profile_payment_methods", "profile", "payment_methods", kind: GraphLinkKind.Secondary, label: "Payment methods"),
            new("payment_methods_profile", "payment_methods", "profile", kind: GraphLinkKind.Back, label: "Back to profile", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("profile_settings", "profile", "settings", kind: GraphLinkKind.Secondary, label: "Settings"),
            new("settings_profile", "settings", "profile", kind: GraphLinkKind.Back, label: "Back to profile", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),

            new("help_faq", "help_center", "faq", kind: GraphLinkKind.Primary, label: "FAQ"),
            new("faq_help", "faq", "help_center", kind: GraphLinkKind.Back, label: "Back to help", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("help_chat", "help_center", "support_chat", kind: GraphLinkKind.Secondary, label: "Chat"),
            new("chat_ticket_details", "support_chat", "ticket_details", kind: GraphLinkKind.Primary, label: "Create ticket"),
            new("help_contact_form", "help_center", "contact_form", kind: GraphLinkKind.Secondary, label: "Contact"),
            new("contact_ticket_details", "contact_form", "ticket_details", kind: GraphLinkKind.Primary, label: "Submit"),
            new("ticket_help", "ticket_details", "help_center", kind: GraphLinkKind.Back, label: "Back to help", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("help_support_popup", "help_center", "support_popup", kind: GraphLinkKind.PopupOpen, label: "Quick help", lineStyle: GraphLineStyle.Dashed),
            new("support_popup_help", "support_popup", "help_center", kind: GraphLinkKind.PopupClose, label: "Close", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("product_details_support_popup", "product_details", "support_popup", kind: GraphLinkKind.PopupOpen, label: "Need help?", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("cart_support_popup", "cart", "support_popup", kind: GraphLinkKind.PopupOpen, label: "Checkout help", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false)
        ];

        return new GraphDocument("uix-product-demo", nodes, links, groups: groups);
    }

    private static GraphNode CreateNode(
        string id,
        string title,
        string containerGroupId,
        string? markerGroupId = null,
        GraphNodeKind kind = GraphNodeKind.Standard,
        string? description = null,
        GraphSize? size = null)
    {
        string[] groups = markerGroupId is null
            ? [containerGroupId]
            : [containerGroupId, markerGroupId];

        return new GraphNode(
            id,
            title,
            description,
            kind,
            size ?? GraphSize.Standard,
            groupMemberships: groups);
    }

    private sealed record DemoScreenInfo(string Title, string Subtitle, string PathHint, string Area, string Role);

    private sealed class GraphvizDemoLayoutEngine : IGraphLayoutEngine
    {
        public GraphLayoutResult Layout(GraphDocument document, GraphLayoutOptions options)
        {
            return GraphvizLayoutEngine.Layout(document, options);
        }
    }
}
