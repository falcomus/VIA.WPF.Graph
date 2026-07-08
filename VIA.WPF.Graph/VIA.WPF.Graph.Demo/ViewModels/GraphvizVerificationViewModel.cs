using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Core.Validation;
using VIA.WPF.Graph.Demo.TestData;
using VIA.WPF.Graph.Graphviz.Layout;

namespace VIA.WPF.Graph.Demo.ViewModels;

public partial class GraphvizVerificationViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunLayoutCommand))]
    private bool isLayoutRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunLayoutCommand))]
    private GraphDemoTestSet? selectedTestSet;

    [ObservableProperty]
    private GraphLayoutDirection selectedDirection = GraphLayoutDirection.LeftToRight;

    [ObservableProperty]
    private GraphDocument? currentDocument;

    [ObservableProperty]
    private GraphLayoutResult? currentLayout;

    [ObservableProperty]
    private GraphViewMode activeViewMode = GraphViewMode.GroupOverview;

    [ObservableProperty]
    private double visualDensity = 1d;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomText))]
    private double zoom = 1d;

    [ObservableProperty]
    private double panX;

    [ObservableProperty]
    private double panY;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NavigationModeText))]
    private bool isFreeNavigationEnabled;

    [ObservableProperty]
    private int fitRequestVersion;

    [ObservableProperty]
    private int centerRequestVersion;

    [ObservableProperty]
    private string resultText = "Phase 5 testsets are ready.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyMarkerGroupSelectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(FocusMarkerGroupCommand))]
    private string? selectedMarkerGroupId;

    [ObservableProperty]
    private IReadOnlyList<string> selectedGroupIds = Array.Empty<string>();

    [ObservableProperty]
    private bool isMarkerGroupFilterEnabled;

    [ObservableProperty]
    private string? focusedGroupId;

    [ObservableProperty]
    private string markerGroupStatusText = "No marker groups in the current testset.";

    [ObservableProperty]
    private string technicalDetails = string.Empty;

    public GraphvizVerificationViewModel()
    {
        TestSets = new ObservableCollection<GraphDemoTestSet>(GraphDemoTestSetFactory.CreateAll());
        LayoutDirections =
        [
            GraphLayoutDirection.LeftToRight,
            GraphLayoutDirection.TopToBottom
        ];

        SelectedTestSet = TestSets.FirstOrDefault();
        RunLayoutCommand.Execute(null);
    }

    public ObservableCollection<GraphDemoTestSet> TestSets { get; }

    public ObservableCollection<string> MarkerGroupIds { get; } = [];

    public IReadOnlyList<GraphLayoutDirection> LayoutDirections { get; }

    public IReadOnlyList<GraphViewMode> ViewModes { get; } =
    [
        GraphViewMode.GroupOverview,
        GraphViewMode.Overview,
        GraphViewMode.Focus,
        GraphViewMode.Diagnostic
    ];

    public string NavigationModeText => IsFreeNavigationEnabled
        ? "Free Pan/Zoom: mouse wheel zooms, right or middle mouse button pans. Fit is not reapplied automatically."
        : "Fit mode: layout changes, view changes and window resizing keep the graph fitted to the visible area.";

    public string ZoomText => $"Zoom: {Zoom:P0}";

    partial void OnSelectedTestSetChanged(GraphDemoTestSet? value)
    {
        if (value is not null)
        {
            _ = RunLayoutCommand.ExecuteAsync(null);
        }
    }

    partial void OnSelectedDirectionChanged(GraphLayoutDirection value)
    {
        _ = RunLayoutCommand.ExecuteAsync(null);
    }

    private bool CanRunLayout()
    {
        return !IsLayoutRunning && SelectedTestSet is not null;
    }

    [RelayCommand(CanExecute = nameof(CanRunLayout))]
    private async Task RunLayoutAsync()
    {
        if (SelectedTestSet is not { } testSet)
        {
            return;
        }

        try
        {
            IsLayoutRunning = true;
            ResultText = $"{testSet.Name}: layout is running ...";
            TechnicalDetails = string.Empty;
            CurrentDocument = testSet.Document;
            CurrentLayout = null;
            UpdateMarkerGroups(testSet.Document);
            ActiveViewMode = testSet.DefaultViewMode;
            VisualDensity = testSet.DefaultVisualDensity;
            IsFreeNavigationEnabled = false;
            Zoom = 1d;
            PanX = 0d;
            PanY = 0d;

            GraphValidationResult validation = GraphDocumentValidator.Validate(testSet.Document);
            GraphLayoutOptions options = new(SelectedDirection, GraphEdgeRoutingStyle.Spline);
            Stopwatch stopwatch = Stopwatch.StartNew();
            GraphLayoutResult layoutResult = await Task.Run(() => GraphvizLayoutEngine.Layout(testSet.Document, options));
            stopwatch.Stop();

            CurrentLayout = layoutResult;
            RequestFit();
            TechnicalDetails = CreateTechnicalDetails(testSet, validation, layoutResult, stopwatch.Elapsed);
            ResultText = layoutResult.Succeeded
                ? $"{testSet.Name}: {testSet.NodeCount} nodes, {testSet.LinkCount} links, {testSet.GroupCount} groups laid out in {stopwatch.ElapsedMilliseconds} ms."
                : $"{testSet.Name}: controlled layout error; see technical details.";
        }
        catch (Exception exception)
        {
            CurrentLayout = null;
            ResultText = $"{testSet.Name}: layout failed.";
            TechnicalDetails = exception.ToString();
        }
        finally
        {
            IsLayoutRunning = false;
        }
    }

    [RelayCommand]
    private void ShowAreaOverview()
    {
        ActiveViewMode = GraphViewMode.GroupOverview;
        IsFreeNavigationEnabled = false;
        RequestFit();
    }

    [RelayCommand]
    private void ShowFullGraph()
    {
        ActiveViewMode = GraphViewMode.Overview;
        IsFreeNavigationEnabled = false;
        RequestFit();
    }

    [RelayCommand]
    private void UseFitMode()
    {
        IsFreeNavigationEnabled = false;
        RequestFit();
    }

    [RelayCommand]
    private void UseFreePanZoomMode()
    {
        IsFreeNavigationEnabled = true;
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
        Zoom = 1d;
        PanX = 0d;
        PanY = 0d;
    }

    [RelayCommand]
    private void CenterGraph()
    {
        IsFreeNavigationEnabled = true;
        CenterRequestVersion++;
    }

    private bool HasSelectedMarkerGroup()
    {
        return !string.IsNullOrWhiteSpace(SelectedMarkerGroupId);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedMarkerGroup))]
    private void ApplyMarkerGroupSelection()
    {
        if (string.IsNullOrWhiteSpace(SelectedMarkerGroupId))
        {
            return;
        }

        SelectedGroupIds = [SelectedMarkerGroupId];
        IsMarkerGroupFilterEnabled = true;
        FocusedGroupId = null;
        ActiveViewMode = GraphViewMode.Overview;
        IsFreeNavigationEnabled = false;
        MarkerGroupStatusText = $"Marker group '{SelectedMarkerGroupId}' is selected and used as filter.";
        RequestFit();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedMarkerGroup))]
    private void FocusMarkerGroup()
    {
        if (string.IsNullOrWhiteSpace(SelectedMarkerGroupId))
        {
            return;
        }

        SelectedGroupIds = [SelectedMarkerGroupId];
        IsMarkerGroupFilterEnabled = false;
        IsFreeNavigationEnabled = true;
        ActiveViewMode = GraphViewMode.Focus;
        FocusedGroupId = SelectedMarkerGroupId;
        MarkerGroupStatusText = $"Marker group '{SelectedMarkerGroupId}' is focused. Free navigation prevents automatic refit.";
    }

    [RelayCommand]
    private void ClearMarkerGroups()
    {
        SelectedGroupIds = Array.Empty<string>();
        IsMarkerGroupFilterEnabled = false;
        FocusedGroupId = null;
        ActiveViewMode = GraphViewMode.Overview;
        IsFreeNavigationEnabled = false;
        MarkerGroupStatusText = MarkerGroupIds.Count == 0
            ? "No marker groups in the current testset."
            : "Marker group selection is cleared.";
        RequestFit();
    }

    [RelayCommand]
    private void SetCompactDensity()
    {
        VisualDensity = 0.72d;
    }

    [RelayCommand]
    private void SetNormalDensity()
    {
        VisualDensity = 1d;
    }

    private void RequestFit()
    {
        FitRequestVersion++;
    }

    private void UpdateMarkerGroups(GraphDocument document)
    {
        MarkerGroupIds.Clear();
        foreach (GraphGroup markerGroup in document.Groups.Where(group => group.Kind == GraphGroupKind.Marker))
        {
            MarkerGroupIds.Add(markerGroup.Id);
        }

        SelectedMarkerGroupId = MarkerGroupIds.FirstOrDefault();
        SelectedGroupIds = Array.Empty<string>();
        IsMarkerGroupFilterEnabled = false;
        FocusedGroupId = null;
        MarkerGroupStatusText = MarkerGroupIds.Count == 0
            ? "No marker groups in the current testset."
            : $"{MarkerGroupIds.Count} marker groups available.";
    }

    private static string CreateTechnicalDetails(
        GraphDemoTestSet testSet,
        GraphValidationResult validation,
        GraphLayoutResult layoutResult,
        TimeSpan elapsed)
    {
        GraphDocument document = testSet.Document;
        int containerGroupCount = document.Groups.Count(group => group.Kind == GraphGroupKind.Container);
        int markerGroupCount = document.Groups.Count(group => group.Kind == GraphGroupKind.Marker);
        int popupNodeCount = document.Nodes.Count(node => node.Kind == GraphNodeKind.Popup);
        int externalNodeCount = document.Nodes.Count(node => node.Kind == GraphNodeKind.External);
        int backLinkCount = document.Links.Count(link => link.Kind == GraphLinkKind.Back);
        int externalLinkCount = document.Links.Count(link => link.Kind == GraphLinkKind.External);

        List<string> lines =
        [
            $"Testset: {testSet.Name}",
            testSet.Description,
            string.Empty,
            $"Document id: {document.Id}",
            $"Direction: {layoutResult.Options.Direction}",
            $"Nodes: {document.Nodes.Count}",
            $"Links: {document.Links.Count}",
            $"Groups: {document.Groups.Count} ({containerGroupCount} container, {markerGroupCount} marker)",
            $"Popup nodes: {popupNodeCount}",
            $"External nodes: {externalNodeCount}",
            $"Back links: {backLinkCount}",
            $"External links: {externalLinkCount}",
            string.Empty,
            $"Navigation: {(layoutResult.Succeeded ? "fit mode by default; free pan/zoom is optional" : "not available")}",
            $"Validation: {(validation.IsValid ? "valid" : "invalid")}",
            $"Validation issues: {validation.Issues.Count}",
            $"Layout succeeded: {layoutResult.Succeeded}",
            $"Layout time: {elapsed.TotalMilliseconds:0.0} ms"
        ];

        if (layoutResult.GraphBounds is { } bounds)
        {
            lines.Add($"Layout bounds: X={bounds.X:0.0}, Y={bounds.Y:0.0}, W={bounds.Width:0.0}, H={bounds.Height:0.0}");
        }

        if (layoutResult.Error is not null)
        {
            lines.Add(string.Empty);
            lines.Add("Layout error:");
            lines.Add(layoutResult.Error.Message);
            if (!string.IsNullOrWhiteSpace(layoutResult.Error.Details))
            {
                lines.Add(layoutResult.Error.Details);
            }
        }

        if (validation.Issues.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Validation issues:");
            foreach (GraphValidationIssue issue in validation.Issues)
            {
                lines.Add($"- {issue.Severity} {issue.Code}: {issue.Message}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
