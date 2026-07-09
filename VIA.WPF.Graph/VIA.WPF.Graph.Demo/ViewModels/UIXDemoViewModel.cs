using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Core.Projections;
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
    [NotifyPropertyChangedFor(nameof(FooterText))]
    private GraphLayoutResult currentLayout;

    [ObservableProperty]
    private GraphTreeProjection treeProjection;

    public IReadOnlyList<ProductNavigationTreeItem> NavigationGroups { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeText))]
    [NotifyPropertyChangedFor(nameof(CurrentScopeText))]
    [NotifyPropertyChangedFor(nameof(FooterText))]
    private GraphViewMode activeViewMode = GraphViewMode.Focus;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DensityButtonText))]
    private double visualDensity = 1d;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomText))]
    [NotifyPropertyChangedFor(nameof(FooterText))]
    private double zoom = 1d;

    [ObservableProperty]
    private double panX;

    [ObservableProperty]
    private double panY;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeText))]
    private bool isFreeNavigationEnabled;

    [ObservableProperty]
    private int fitRequestVersion;

    [ObservableProperty]
    private int centerRequestVersion;

    [ObservableProperty]
    private int actualSizeRequestVersion;

    [ObservableProperty]
    private string? selectedTreeNodeId;

    [ObservableProperty]
    private string? selectedNodeId;

    [ObservableProperty]
    private string? selectedLinkId;

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

        CurrentLayout = GraphvizLayoutEngine.Layout(
            CurrentDocument,
            new GraphLayoutOptions(GraphLayoutDirection.LeftToRight, GraphEdgeRoutingStyle.Spline));

        TreeProjection = GraphTreeProjectionBuilder.Build(CurrentDocument, rootNodeId: "splash");
        NavigationGroups = CreateNavigationGroups();
        GraphRequestCommand = new RelayCommand<GraphRequest>(HandleGraphRequest);
        SelectedNodeIds = ["splash"];
        SelectedNodeId = "splash";
        FocusedNodeId = "splash";
        RequestFit();
    }

    public IRelayCommand<GraphRequest> GraphRequestCommand { get; }

    public string CountsText => $"{CurrentDocument.Nodes.Count} screens · {CurrentDocument.Links.Count} transitions · {CurrentDocument.Groups.Count} areas";

    public string ZoomText => $"Zoom {Zoom:P0}";

    public string DensityButtonText => VisualDensity < 1d ? "Comfort" : "Compact";

    public string CurrentScopeText => ActiveViewMode switch
    {
        GraphViewMode.GroupOverview => "Areas",
        GraphViewMode.Focus => "Focus",
        GraphViewMode.Overview => "Branch",
        _ => "Diagnostic"
    };

    public string ModeText => ActiveViewMode switch
    {
        GraphViewMode.GroupOverview => "Area view shows the app sections and their transitions.",
        GraphViewMode.Focus => "Focus view emphasizes the selected screen and its direct navigation context.",
        GraphViewMode.Overview => "Branch view shows the complete demo navigation with the selected path highlighted.",
        _ => "Diagnostic view shows the complete technical graph structure."
    };

    public string SelectedScreenTitle
    {
        get
        {
            DemoScreenInfo info = GetSelectedScreenInfo();
            return info.Title;
        }
    }

    public string SelectedScreenSubtitle
    {
        get
        {
            DemoScreenInfo info = GetSelectedScreenInfo();
            return info.Subtitle;
        }
    }

    public string SelectedPathText
    {
        get
        {
            DemoScreenInfo info = GetSelectedScreenInfo();
            return info.PathHint;
        }
    }

    public string SelectedScreenAreaText
    {
        get
        {
            DemoScreenInfo info = GetSelectedScreenInfo();
            return info.Area;
        }
    }

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

    public string FooterText => $"{CountsText} · Scope {CurrentScopeText} · {ZoomText} · Selection {SelectedScreenTitle}";

    public void SelectNavigationTreeItem(ProductNavigationTreeItem? treeItem)
    {
        if (treeItem is null)
        {
            return;
        }

        SelectedTreeNodeId = treeItem.TreeItemId;

        if (string.IsNullOrWhiteSpace(treeItem.NodeId))
        {
            return;
        }

        SelectNode(treeItem.NodeId, centerAfterSelection: true);
        SelectedLinkId = treeItem.LinkId;
    }

    [RelayCommand]
    private void ShowAreaOverview()
    {
        ActiveViewMode = GraphViewMode.GroupOverview;
        FocusedNodeId = null;
        IsFreeNavigationEnabled = false;
        RequestFit();
    }

    [RelayCommand]
    private void ShowGraphOverview()
    {
        ActiveViewMode = GraphViewMode.Overview;
        FocusedNodeId = SelectedNodeIds.FirstOrDefault();
        IsFreeNavigationEnabled = false;
        RequestFit();
    }

    [RelayCommand]
    private void ShowFocus()
    {
        ActiveViewMode = GraphViewMode.Focus;
        FocusedNodeId = SelectedNodeIds.FirstOrDefault() ?? "splash";
        IsFreeNavigationEnabled = false;
        RequestCenter();
    }

    [RelayCommand]
    private void FitToGraph()
    {
        IsFreeNavigationEnabled = false;
        RequestFit();
    }

    [RelayCommand]
    private void SetActualSize()
    {
        IsFreeNavigationEnabled = true;
        ActualSizeRequestVersion++;
    }

    [RelayCommand]
    private void CenterGraph()
    {
        IsFreeNavigationEnabled = true;
        RequestCenter();
    }

    [RelayCommand]
    private void ToggleDensity()
    {
        VisualDensity = VisualDensity < 1d ? 1.08d : 0.82d;
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
                SelectNode(request.NodeId, centerAfterSelection: true);
                if (request.Kind == GraphRequestKind.OpenNode)
                {
                    ActiveViewMode = GraphViewMode.Focus;
                    FocusedNodeId = request.NodeId;
                    RequestCenter();
                }

                break;

            case GraphRequestKind.SelectLink:
            case GraphRequestKind.OpenLink:
                SelectLink(request.LinkId);
                break;

            case GraphRequestKind.ClearSelection:
                SelectedNodeIds = Array.Empty<string>();
                SelectedLinkIds = Array.Empty<string>();
                SelectedNodeId = null;
                SelectedLinkId = null;
                FocusedNodeId = null;
                break;

            case GraphRequestKind.ReturnToOverview:
                ActiveViewMode = GraphViewMode.Overview;
                FocusedNodeId = null;
                RequestFit();
                break;
        }
    }

    private void SelectNode(string? nodeId, bool centerAfterSelection)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        SelectedNodeIds = [nodeId];
        SelectedLinkIds = Array.Empty<string>();
        SelectedNodeId = nodeId;
        SelectedLinkId = null;
        FocusedNodeId = nodeId;

        if (ActiveViewMode != GraphViewMode.GroupOverview)
        {
            ActiveViewMode = GraphViewMode.Focus;
        }

        if (centerAfterSelection)
        {
            RequestCenter();
        }
    }

    private void SelectLink(string? linkId)
    {
        if (string.IsNullOrWhiteSpace(linkId))
        {
            return;
        }

        SelectedNodeIds = Array.Empty<string>();
        SelectedLinkIds = [linkId];
        SelectedLinkId = linkId;
    }

    partial void OnSelectedNodeIdChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !SelectedNodeIds.Contains(value, StringComparer.Ordinal))
        {
            SelectedNodeIds = [value];
        }
    }

    partial void OnSelectedLinkIdChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !SelectedLinkIds.Contains(value, StringComparer.Ordinal))
        {
            SelectedLinkIds = [value];
        }
    }

    private void RequestFit()
    {
        FitRequestVersion++;
    }

    private void RequestCenter()
    {
        CenterRequestVersion++;
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
            "The graph preview follows the current selection.",
            "None",
            "No active screen");
    }

    private string? GetSelectedNodeIdOrNull()
    {
        return SelectedNodeIds.FirstOrDefault() ?? SelectedNodeId;
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

    private static IReadOnlyList<ProductNavigationTreeItem> CreateNavigationGroups()
    {
        return
        [
            Group(
                "group-entry",
                "Entry",
                "Launch, authentication and first-run setup",
                "6",
                true,
                Screen("splash", "Splash", "Launch and restore session", "Start", "splash", null, true,
                    Screen("login", "Login", "Authentication hub", "Main", "login", "splash_login", true,
                        Screen("register", "Register", "Create account", "Alt", "register", "login_register", true,
                            Screen("verify_email", "Verify email", "Confirm account", "Main", "verify_email", "register_verify_email", true,
                                Screen("onboarding", "Onboarding", "First-run setup", "Main", "onboarding", "verify_email_onboarding"),
                                Reference("home_from_onboarding", "Home", "Setup completion target", "Main", "home", "onboarding_home"))),
                        Screen("forgot_password", "Forgot password", "Recovery flow", "Alt", "forgot_password", "login_forgot_password",
                            false, Reference("login_from_forgot", "Login", "Recovery return", "Back", "login", "forgot_password_login")),
                        Reference("home_from_login", "Home", "Signed-in target", "Main", "home", "login_home")))),

            Group(
                "group-home",
                "Home",
                "Dashboard, discovery shortcuts and service entry points",
                "7",
                true,
                Screen("home_hub", "Home", "Personal dashboard and hub", "Hub", "home", null, true,
                    Screen("feed_from_home", "For you", "Personalized content", "Main", "feed", "home_feed", true,
                        Reference("product_list_from_feed", "Product list", "Promoted collection", "Main", "product_list", "feed_product_list")),
                    Screen("search_from_home", "Search", "Find products", "Main", "search", "home_search"),
                    Screen("categories_from_home", "Categories", "Browse departments", "Alt", "categories", "home_categories"),
                    Screen("campaign_from_home", "Campaign landing", "Seasonal offer page", "Alt", "campaign_landing", "home_campaign"),
                    Screen("wishlist_from_home", "Wishlist", "Saved products", "Alt", "wishlist", "home_wishlist"),
                    Screen("profile_from_home", "Profile", "Account hub", "Alt", "profile", "home_profile"),
                    Screen("help_from_home", "Help center", "Support entry", "Alt", "help_center", "home_help_center"))),

            Group(
                "group-shop",
                "Shop",
                "Search, browse, detail and conversion paths",
                "10",
                true,
                Screen("search", "Search", "Query, filters and suggestions", "Main", "search", "home_search",
                    false, Screen("product_list_from_search", "Product list", "Filtered results", "Main", "product_list", "search_product_list", true,
                        Screen("product_details_from_list", "Product details", "Conversion screen", "Main", "product_details", "product_list_product_details", true,
                            Screen("product_images", "Product images", "Gallery preview", "Alt", "product_images", "product_details_images",
                                false, Reference("product_details_from_images", "Product details", "Back to detail", "Back", "product_details", "product_images_product_details")),
                            Screen("reviews", "Reviews", "Ratings and comments", "Alt", "reviews", "product_details_reviews",
                                false, Reference("product_details_from_reviews", "Product details", "Back to detail", "Back", "product_details", "reviews_product_details")),
                            Screen("variant_selector", "Variant selector", "Size and color", "Main", "variant_selector", "product_details_variant",
                                false, Reference("product_details_from_variant", "Product details", "Apply selection", "Back", "product_details", "variant_product_details")),
                            Screen("availability", "Availability", "Pickup and delivery check", "Alt", "availability", "product_details_availability",
                                false, Reference("cart_from_availability", "Cart", "Available item target", "Main", "cart", "availability_cart")),
                            Screen("recommendations", "Recommendations", "Related products", "Alt", "recommendations", "product_details_recommendations",
                                false, Reference("product_details_from_recommendations", "Product details", "Open related product", "Ref", "product_details", "recommendations_product_details")),
                            Screen("wishlist_from_details", "Wishlist", "Save for later", "Alt", "wishlist", "product_details_wishlist",
                                false, Reference("product_details_from_wishlist", "Product details", "Open saved product", "Ref", "product_details", "wishlist_product_details")),
                            Reference("cart_from_details", "Cart", "Add-to-cart target", "Main", "cart", "product_details_cart")))),
                Screen("categories", "Categories", "Alternative browse path", "Alt", "categories", "home_categories",
                    false, Reference("product_list_from_categories", "Product list", "Category results", "Main", "product_list", "categories_product_list")),
                Screen("campaign_landing", "Campaign landing", "Seasonal products", "Alt", "campaign_landing", "home_campaign",
                    false, Reference("product_list_from_campaign", "Product list", "Campaign results", "Main", "product_list", "campaign_product_list"))),

            Group(
                "group-checkout",
                "Checkout",
                "Basket, promotion, shipping, payment and confirmation",
                "9",
                true,
                Screen("cart", "Cart", "Items, quantities and checkout", "Main", "cart", "product_details_cart", true,
                    Screen("coupon", "Coupon", "Discount code validation", "Alt", "coupon", "cart_coupon",
                        false, Reference("cart_from_coupon", "Cart", "Discount applied", "Back", "cart", "coupon_cart")),
                    Screen("address", "Address", "Shipping address", "Main", "address", "cart_address", true,
                        Screen("delivery", "Delivery", "Slot and method", "Main", "delivery", "address_delivery", true,
                            Screen("payment", "Payment", "Payment method", "Main", "payment", "delivery_payment", true,
                                External("payment_provider", "Payment provider", "External authorization", "Ext", "payment_provider", "payment_provider_external"),
                                Screen("order_review", "Order review", "Final check", "Main", "order_review", "payment_order_review", true,
                                    Reference("cart_from_review", "Cart", "Change cart", "Back", "cart", "order_review_cart"),
                                    Screen("order_confirmation", "Order confirmation", "Success and next steps", "Main", "order_confirmation", "order_review_confirmation", false,
                                        Reference("orders_from_confirmation", "Orders", "View order", "Alt", "orders", "confirmation_orders"),
                                        Reference("home_from_confirmation", "Home", "Back home", "Alt", "home", "confirmation_home")))))))),

            Group(
                "group-account",
                "Account",
                "Profile, order history, documents and preferences",
                "9",
                false,
                Screen("profile", "Profile", "Account overview", "Hub", "profile", "home_profile", true,
                    Screen("orders", "Orders", "Order history", "Main", "orders", "profile_orders", true,
                        Screen("order_details", "Order details", "Shipment and invoice", "Main", "order_details", "orders_order_details", true,
                            External("shipment_tracking", "Shipment tracking", "Carrier tracking", "Ext", "shipment_tracking", "order_details_tracking"),
                            Screen("returns", "Return request", "Return item", "Alt", "returns", "order_details_returns", true,
                                Screen("return_label", "Return label", "Drop-off document", "Main", "return_label", "returns_return_label",
                                    false, Reference("orders_from_return_label", "Orders", "Back to history", "Back", "orders", "return_label_orders"))))),
                    Screen("invoices", "Invoices", "PDF documents", "Alt", "invoices", "profile_invoices"),
                    Screen("account_addresses", "Saved addresses", "Manage addresses", "Alt", "account_addresses", "profile_addresses"),
                    Screen("payment_methods", "Payment methods", "Manage cards and wallets", "Alt", "payment_methods", "profile_payment_methods"),
                    Screen("settings", "Settings", "Preferences", "Alt", "settings", "profile_settings",
                        false, Reference("profile_from_settings", "Profile", "Back reference", "Back", "profile", "settings_profile")))),

            Group(
                "group-support",
                "Support",
                "Self service, chat, contact and contextual help",
                "7",
                false,
                Screen("help_center", "Help center", "Support hub", "Hub", "help_center", "home_help_center", true,
                    Screen("faq", "FAQ", "Self-service topics", "Main", "faq", "help_faq",
                        false, Reference("help_from_faq", "Help center", "Back to help", "Back", "help_center", "faq_help")),
                    Screen("support_chat", "Support chat", "Live or bot chat", "Alt", "support_chat", "help_chat", true,
                        Screen("ticket_details_from_chat", "Ticket details", "Case timeline", "Main", "ticket_details", "chat_ticket_details",
                            false, Reference("help_from_ticket_chat", "Help center", "Back to help", "Back", "help_center", "ticket_help"))),
                    Screen("contact_form", "Contact form", "Structured request", "Alt", "contact_form", "help_contact_form", true,
                        Screen("ticket_details_from_contact", "Ticket details", "Created support case", "Main", "ticket_details", "contact_ticket_details",
                            false, Reference("help_from_ticket_contact", "Help center", "Back to help", "Back", "help_center", "ticket_help"))),
                    Popup("support_popup", "Support popup", "Contextual overlay", "Popup", "support_popup", "help_support_popup",
                        false, Reference("help_from_popup", "Help center", "Close overlay", "Close", "help_center", "support_popup_help")))),

            Group(
                "group-external",
                "External",
                "Boundaries to systems outside the app",
                "2",
                false,
                External("external_payment_provider", "Payment provider", "Authorization boundary", "Ext", "payment_provider", "payment_provider_external"),
                External("external_shipment_tracking", "Shipment tracking", "Carrier tracking boundary", "Ext", "shipment_tracking", "order_details_tracking"))
        ];
    }

    private static ProductNavigationTreeItem Group(
        string treeItemId,
        string title,
        string subtitle,
        string badgeText,
        bool isExpanded,
        params ProductNavigationTreeItem[] children)
    {
        return new ProductNavigationTreeItem(
            treeItemId,
            title,
            subtitle,
            badgeText,
            "Group",
            null,
            null,
            true,
            isExpanded,
            children);
    }

    private static ProductNavigationTreeItem Screen(
        string treeItemId,
        string title,
        string subtitle,
        string badgeText,
        string nodeId,
        string? linkId,
        bool isExpanded = false,
        params ProductNavigationTreeItem[] children)
    {
        return new ProductNavigationTreeItem(
            treeItemId,
            title,
            subtitle,
            badgeText,
            "Screen",
            nodeId,
            linkId,
            false,
            isExpanded,
            children);
    }

    private static ProductNavigationTreeItem Reference(
        string treeItemId,
        string title,
        string subtitle,
        string badgeText,
        string nodeId,
        string? linkId)
    {
        return new ProductNavigationTreeItem(
            treeItemId,
            title,
            subtitle,
            badgeText,
            "Ref",
            nodeId,
            linkId,
            false,
            false,
            [],
            true);
    }

    private static ProductNavigationTreeItem Popup(
        string treeItemId,
        string title,
        string subtitle,
        string badgeText,
        string nodeId,
        string? linkId,
        bool isExpanded = false,
        params ProductNavigationTreeItem[] children)
    {
        return new ProductNavigationTreeItem(
            treeItemId,
            title,
            subtitle,
            badgeText,
            "Popup",
            nodeId,
            linkId,
            false,
            isExpanded,
            children,
            false,
            true);
    }

    private static ProductNavigationTreeItem External(
        string treeItemId,
        string title,
        string subtitle,
        string badgeText,
        string nodeId,
        string? linkId)
    {
        return new ProductNavigationTreeItem(
            treeItemId,
            title,
            subtitle,
            badgeText,
            "Ext",
            nodeId,
            linkId,
            false,
            false,
            [],
            false,
            false,
            true);
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
}

public sealed class ProductNavigationTreeItem : ObservableObject
{
    private bool isExpanded;
    private bool isSelected;

    public ProductNavigationTreeItem(
        string treeItemId,
        string title,
        string subtitle,
        string badgeText,
        string kind,
        string? nodeId,
        string? linkId,
        bool isGroup,
        bool isExpanded,
        IEnumerable<ProductNavigationTreeItem> children,
        bool isReference = false,
        bool isPopup = false,
        bool isExternal = false)
    {
        TreeItemId = RequireText(treeItemId, nameof(treeItemId));
        Title = RequireText(title, nameof(title));
        Subtitle = subtitle;
        BadgeText = badgeText;
        Kind = kind;
        IconGlyph = ResolveIconGlyph(title, kind, isGroup, isReference, isPopup, isExternal);
        NodeId = nodeId;
        LinkId = linkId;
        IsGroup = isGroup;
        this.isExpanded = isExpanded;
        Children = children.ToArray();
        IsReference = isReference;
        IsPopup = isPopup;
        IsExternal = isExternal;
    }

    public string TreeItemId { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public string BadgeText { get; }

    public string Kind { get; }

    public string IconGlyph { get; }

    public string? NodeId { get; }

    public string? LinkId { get; }

    public bool IsGroup { get; }

    public bool IsReference { get; }

    public bool IsPopup { get; }

    public bool IsExternal { get; }

    public IReadOnlyList<ProductNavigationTreeItem> Children { get; }

    public bool HasChildren => Children.Count > 0;

    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }

    private static string ResolveIconGlyph(
        string title,
        string kind,
        bool isGroup,
        bool isReference,
        bool isPopup,
        bool isExternal)
    {
        if (isPopup)
        {
            return "◱";
        }

        if (isExternal)
        {
            return "↗";
        }

        if (isReference)
        {
            return "↩";
        }

        if (isGroup)
        {
            return title switch
            {
                "Entry" => "⌁",
                "Home" => "⌂",
                "Shop" => "▦",
                "Checkout" => "✓",
                "Account" => "◉",
                "Support" => "?",
                "External" => "↗",
                _ => "▣"
            };
        }

        return title switch
        {
            "Splash" => "◆",
            "Login" => "▣",
            "Register" => "+",
            "Verify email" => "✓",
            "For you" => "✦",
            "Campaign landing" => "%",
            "Product images" => "▧",
            "Variant selector" => "◫",
            "Availability" => "⌁",
            "Coupon" => "%",
            "Payment provider" => "↗",
            "Shipment tracking" => "↗",
            "Return request" => "↩",
            "Return label" => "▤",
            "Invoices" => "▤",
            "Saved addresses" => "⌂",
            "Payment methods" => "◇",
            "Help center" => "?",
            "FAQ" => "?",
            "Support chat" => "◌",
            "Ticket details" => "●",
            "Support popup" => "◱",
            "Forgot password" => "⌕",
            "Home" => "⌂",
            "Search" => "⌕",
            "Categories" => "▦",
            "Product list" => "☰",
            "Product details" => "●",
            "Recommendations" => "✦",
            "Reviews" => "★",
            "Wishlist" => "♡",
            "Cart" => "▤",
            "Address" => "⌂",
            "Delivery" => "→",
            "Payment" => "◇",
            "Order review" => "✓",
            "Order confirmation" => "✓",
            "Profile" => "◉",
            "Orders" => "☰",
            "Order details" => "●",
            "Returns" => "↩",
            "Settings" => "⚙",
            "Help" => "?",
            "Contact form" => "✉",
            _ => kind.Length > 0 ? kind[0].ToString().ToUpperInvariant() : "●"
        };
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null, empty or whitespace.", parameterName);
        }

        return value;
    }
}
