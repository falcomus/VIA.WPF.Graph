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
    private GraphDocument currentDocument;

    [ObservableProperty]
    private GraphLayoutResult currentLayout;

    [ObservableProperty]
    private GraphTreeProjection treeProjection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeText))]
    private GraphViewMode activeViewMode = GraphViewMode.GroupOverview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DensityButtonText))]
    private double visualDensity = 0.92d;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomText))]
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
    private IReadOnlyList<string> selectedNodeIds = Array.Empty<string>();

    [ObservableProperty]
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

        TreeProjection = GraphTreeProjectionBuilder.Build(CurrentDocument, rootNodeId: "login");
        GraphRequestCommand = new RelayCommand<GraphRequest>(HandleGraphRequest);
        SelectedNodeIds = ["login"];
        SelectedNodeId = "login";
        FocusedNodeId = "login";
        RequestFit();
    }

    public IRelayCommand<GraphRequest> GraphRequestCommand { get; }

    public string CountsText => $"{CurrentDocument.Nodes.Count} screens · {CurrentDocument.Links.Count} transitions · {CurrentDocument.Groups.Count} areas";

    public string ZoomText => $"Zoom {Zoom:P0}";

    public string DensityButtonText => VisualDensity < 1d ? "Komfortabel" : "Kompakt";

    public string ModeText => ActiveViewMode switch
    {
        GraphViewMode.GroupOverview => "Bereichsübersicht: grobe App-Struktur mit gebündelten Übergängen.",
        GraphViewMode.Focus => "Fokusmodus: ausgewählter Screen und direkte Nachbarn stehen im Vordergrund.",
        GraphViewMode.Overview => "Flow: vollständige Navigation mit Screens, Popups, Rückwegen und Alternativen.",
        _ => "Diagnoseansicht: technische Gesamtsicht auf alle Knoten und Kanten."
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
        FocusedNodeId = null;
        IsFreeNavigationEnabled = false;
        RequestFit();
    }

    [RelayCommand]
    private void ShowFocus()
    {
        ActiveViewMode = GraphViewMode.Focus;
        FocusedNodeId = SelectedNodeIds.FirstOrDefault() ?? "login";
        IsFreeNavigationEnabled = false;
        RequestFit();
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
        CenterRequestVersion++;
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
                SelectNode(request.NodeId);
                if (request.Kind == GraphRequestKind.OpenNode)
                {
                    ActiveViewMode = GraphViewMode.Focus;
                    FocusedNodeId = request.NodeId;
                    RequestFit();
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

    private void SelectNode(string? nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        SelectedNodeIds = [nodeId];
        SelectedLinkIds = Array.Empty<string>();
        SelectedNodeId = nodeId;
        SelectedLinkId = null;
        FocusedNodeId = ActiveViewMode == GraphViewMode.Focus ? nodeId : FocusedNodeId;
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

    private DemoScreenInfo GetSelectedScreenInfo()
    {
        string? nodeId = SelectedNodeIds.FirstOrDefault() ?? SelectedNodeId;
        if (nodeId is not null && screenInfoByNodeId.TryGetValue(nodeId, out DemoScreenInfo? info))
        {
            return info;
        }

        return new DemoScreenInfo(
            "No screen selected",
            "Select a node in the tree or graph.",
            "The preview follows the current graph selection.");
    }

    private static DemoScreenInfo CreateScreenInfo(GraphNode node)
    {
        return node.Id switch
        {
            "login" => new DemoScreenInfo("Login", "Email/password entry, social sign-in and links to registration or reset.", "Entry → Login → Home or account recovery."),
            "register" => new DemoScreenInfo("Register", "New user onboarding with validation and account creation.", "Entry → Register → Home."),
            "forgot_password" => new DemoScreenInfo("Forgot password", "Recovery flow returning users safely to Login.", "Entry → Forgot password → Login."),
            "home" => new DemoScreenInfo("Home", "Personalized dashboard and main navigation hub.", "Home connects shopping, account and support areas."),
            "search" => new DemoScreenInfo("Search", "Product discovery with filters and recent searches.", "Home → Search → Product details."),
            "product_details" => new DemoScreenInfo("Product details", "Primary conversion screen with recommendations and add-to-cart.", "Search → Product details → Cart or Home."),
            "cart" => new DemoScreenInfo("Cart", "Review selected items, quantities and checkout readiness.", "Product details → Cart → Checkout."),
            "checkout" => new DemoScreenInfo("Checkout", "Address, payment and confirmation path.", "Cart → Checkout → Home."),
            "profile" => new DemoScreenInfo("Profile", "User profile, orders and account overview.", "Home → Profile → Settings."),
            "settings" => new DemoScreenInfo("Settings", "Notifications, privacy and app preferences.", "Profile → Settings → Home."),
            "support_popup" => new DemoScreenInfo("Support popup", "Lightweight overlay for help and contact options.", "Home → Support popup → Home."),
            _ => new DemoScreenInfo(node.Title, node.Description ?? "Demo screen.", node.Id)
        };
    }

    private static GraphDocument CreateDemoDocument()
    {
        GraphGroup[] groups =
        [
            new("entry", "Entry", GraphGroupKind.Container),
            new("shopping", "Shopping", GraphGroupKind.Container),
            new("checkout_area", "Checkout", GraphGroupKind.Container),
            new("account", "Account", GraphGroupKind.Container),
            new("support", "Support", GraphGroupKind.Container),
            new("primary_path", "Primary path", GraphGroupKind.Marker),
            new("recovery_path", "Recovery", GraphGroupKind.Marker)
        ];

        GraphNode[] nodes =
        [
            CreateNode("login", "Login", "entry", "primary_path", description: "Start and authentication", size: GraphSize.Detail),
            CreateNode("register", "Register", "entry", "primary_path", description: "Create account"),
            CreateNode("forgot_password", "Forgot password", "entry", "recovery_path", description: "Reset access"),
            CreateNode("home", "Home", "shopping", "primary_path", description: "Dashboard", size: GraphSize.Detail),
            CreateNode("search", "Search", "shopping", "primary_path", description: "Find products"),
            CreateNode("product_details", "Product details", "shopping", "primary_path", description: "Review product", size: GraphSize.Detail),
            CreateNode("cart", "Cart", "checkout_area", "primary_path", description: "Selected items"),
            CreateNode("checkout", "Checkout", "checkout_area", "primary_path", description: "Payment and confirmation"),
            CreateNode("profile", "Profile", "account", description: "Account overview"),
            CreateNode("settings", "Settings", "account", description: "Preferences"),
            CreateNode("support_popup", "Support popup", "support", kind: GraphNodeKind.Popup, description: "Help overlay", size: GraphSize.Popup)
        ];

        GraphLink[] links =
        [
            new("login_register", "login", "register", kind: GraphLinkKind.Secondary, label: "Create account"),
            new("register_home", "register", "home", kind: GraphLinkKind.Primary, label: "Complete sign-up"),
            new("login_forgot_password", "login", "forgot_password", kind: GraphLinkKind.Secondary, label: "Forgot password"),
            new("forgot_password_login", "forgot_password", "login", kind: GraphLinkKind.Back, label: "Back to login", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("login_home", "login", "home", kind: GraphLinkKind.Primary, label: "Sign in"),
            new("home_search", "home", "search", kind: GraphLinkKind.Primary, label: "Search"),
            new("search_product_details", "search", "product_details", kind: GraphLinkKind.Primary, label: "Open product"),
            new("product_details_cart", "product_details", "cart", kind: GraphLinkKind.Primary, label: "Add to cart"),
            new("cart_checkout", "cart", "checkout", kind: GraphLinkKind.Primary, label: "Checkout"),
            new("checkout_home", "checkout", "home", kind: GraphLinkKind.Secondary, label: "Done"),
            new("product_details_home", "product_details", "home", kind: GraphLinkKind.Back, label: "Continue browsing", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("home_profile", "home", "profile", kind: GraphLinkKind.Secondary, label: "Profile"),
            new("profile_settings", "profile", "settings", kind: GraphLinkKind.Secondary, label: "Settings"),
            new("settings_home", "settings", "home", kind: GraphLinkKind.Back, label: "Close settings", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false),
            new("home_support_popup", "home", "support_popup", kind: GraphLinkKind.PopupOpen, label: "Need help?", lineStyle: GraphLineStyle.Dashed),
            new("support_popup_home", "support_popup", "home", kind: GraphLinkKind.PopupClose, label: "Close", lineStyle: GraphLineStyle.Dashed, isLayoutConstraint: false)
        ];

        return new GraphDocument("uix-mobile-app-demo", nodes, links, groups: groups);
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

    private sealed record DemoScreenInfo(string Title, string Subtitle, string PathHint);
}
