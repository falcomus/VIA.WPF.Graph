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
    [NotifyCanExecuteChangedFor(nameof(RunLargeRebuildStressTestCommand))]
    private bool isLayoutRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunLayoutCommand))]
    [NotifyCanExecuteChangedFor(nameof(RunLargeRebuildStressTestCommand))]
    private bool isStressTestRunning;

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
    private int stressTestRebuildCount = 25;

    [ObservableProperty]
    private string stressTestStatusText = "Large rebuild stress test not run yet.";

    [ObservableProperty]
    private string technicalDetails = string.Empty;

    private bool suppressAutomaticLayout;

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
        if (!suppressAutomaticLayout && value is not null)
        {
            _ = RunLayoutCommand.ExecuteAsync(null);
        }
    }

    partial void OnSelectedDirectionChanged(GraphLayoutDirection value)
    {
        if (!suppressAutomaticLayout)
        {
            _ = RunLayoutCommand.ExecuteAsync(null);
        }
    }

    private bool CanRunLayout()
    {
        return !IsLayoutRunning && !IsStressTestRunning && SelectedTestSet is not null;
    }

    private bool CanRunLargeRebuildStressTest()
    {
        return !IsLayoutRunning
            && !IsStressTestRunning
            && TestSets.Any(testSet => string.Equals(testSet.Name, "Large", StringComparison.Ordinal));
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


    [RelayCommand(CanExecute = nameof(CanRunLargeRebuildStressTest))]
    private async Task RunLargeRebuildStressTestAsync()
    {
        GraphDemoTestSet? largeTestSet = TestSets.FirstOrDefault(testSet => string.Equals(testSet.Name, "Large", StringComparison.Ordinal));
        if (largeTestSet is null)
        {
            ResultText = "Large stress test cannot run because the Large testset is missing.";
            StressTestStatusText = "Large testset missing.";
            return;
        }

        int requestedRebuildCount = Math.Clamp(StressTestRebuildCount, 1, 200);
        List<(int Iteration, GraphLayoutDirection Direction, GraphViewMode ViewMode, double LayoutMilliseconds, bool Succeeded, int ValidationIssueCount, long MemoryBytes)> samples = [];
        string? failureMessage = null;
        string? selectedStressMarkerGroupId = null;

        try
        {
            IsStressTestRunning = true;
            ResultText = $"Large: {requestedRebuildCount} rebuilds are running ...";
            StressTestStatusText = "Running Large rebuild stress test ...";
            TechnicalDetails = string.Empty;
            IsFreeNavigationEnabled = false;
            Zoom = 1d;
            PanX = 0d;
            PanY = 0d;

            suppressAutomaticLayout = true;
            SelectedTestSet = largeTestSet;
            suppressAutomaticLayout = false;

            UpdateMarkerGroups(largeTestSet.Document);
            selectedStressMarkerGroupId = MarkerGroupIds.FirstOrDefault(groupId => string.Equals(groupId, "critical", StringComparison.Ordinal))
                ?? MarkerGroupIds.FirstOrDefault();
            SelectedMarkerGroupId = selectedStressMarkerGroupId;
            SelectedGroupIds = selectedStressMarkerGroupId is null ? Array.Empty<string>() : [selectedStressMarkerGroupId];
            IsMarkerGroupFilterEnabled = selectedStressMarkerGroupId is not null;
            FocusedGroupId = null;
            MarkerGroupStatusText = selectedStressMarkerGroupId is null
                ? "Large has no marker group available for rebuild selection preservation."
                : $"Marker group '{selectedStressMarkerGroupId}' is selected during rebuild stress test.";

            long memoryBeforeBytes = GC.GetTotalMemory(forceFullCollection: true);
            Stopwatch totalStopwatch = Stopwatch.StartNew();

            for (int iteration = 1; iteration <= requestedRebuildCount; iteration++)
            {
                GraphLayoutDirection direction = iteration % 2 == 0
                    ? GraphLayoutDirection.TopToBottom
                    : GraphLayoutDirection.LeftToRight;
                GraphViewMode viewMode = iteration % 3 == 0
                    ? GraphViewMode.GroupOverview
                    : GraphViewMode.Overview;

                suppressAutomaticLayout = true;
                SelectedDirection = direction;
                suppressAutomaticLayout = false;

                GraphDocument rebuildDocument = largeTestSet.Document;
                GraphValidationResult validation = GraphDocumentValidator.Validate(rebuildDocument);
                GraphLayoutOptions options = new(direction, GraphEdgeRoutingStyle.Spline);

                Stopwatch layoutStopwatch = Stopwatch.StartNew();
                GraphLayoutResult layoutResult = await Task.Run(() => GraphvizLayoutEngine.Layout(rebuildDocument, options));
                layoutStopwatch.Stop();

                CurrentDocument = rebuildDocument;
                CurrentLayout = null;
                ActiveViewMode = viewMode;
                VisualDensity = largeTestSet.DefaultVisualDensity;
                CurrentLayout = layoutResult;
                RequestFit();

                long memoryBytes = GC.GetTotalMemory(forceFullCollection: false);
                samples.Add((
                    iteration,
                    direction,
                    viewMode,
                    layoutStopwatch.Elapsed.TotalMilliseconds,
                    layoutResult.Succeeded,
                    validation.Issues.Count,
                    memoryBytes));

                StressTestStatusText = $"Large rebuild {iteration}/{requestedRebuildCount}: {layoutStopwatch.ElapsedMilliseconds} ms, {direction}, {viewMode}.";

                if (!layoutResult.Succeeded)
                {
                    failureMessage = layoutResult.Error?.Message ?? "Layout returned an unsuccessful result.";
                    break;
                }

                if (!validation.IsValid)
                {
                    failureMessage = "Large rebuild produced validation errors.";
                    break;
                }

                if (!IsMarkerSelectionPreserved(selectedStressMarkerGroupId))
                {
                    failureMessage = "Marker group selection was not preserved during rebuild.";
                    break;
                }

                await Task.Delay(1);
            }

            totalStopwatch.Stop();
            long memoryAfterBytes = GC.GetTotalMemory(forceFullCollection: true);
            TechnicalDetails = CreateStressTechnicalDetails(
                largeTestSet,
                samples,
                totalStopwatch.Elapsed,
                memoryBeforeBytes,
                memoryAfterBytes,
                selectedStressMarkerGroupId,
                failureMessage);

            bool succeeded = failureMessage is null && samples.Count == requestedRebuildCount;
            ResultText = succeeded
                ? $"Large stress passed: {samples.Count} rebuilds, 0 failures, avg {samples.Average(sample => sample.LayoutMilliseconds):0.0} ms."
                : $"Large stress failed after {samples.Count} rebuilds: {failureMessage}";
            StressTestStatusText = succeeded
                ? $"Passed: {samples.Count} Large rebuilds completed without layout, validation or selection failure."
                : $"Failed: {failureMessage}";
        }
        catch (Exception exception)
        {
            CurrentLayout = null;
            ResultText = "Large stress test failed with an exception.";
            StressTestStatusText = exception.Message;
            TechnicalDetails = exception.ToString();
        }
        finally
        {
            suppressAutomaticLayout = false;
            IsStressTestRunning = false;
        }
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



    private bool IsMarkerSelectionPreserved(string? markerGroupId)
    {
        return markerGroupId is null
            || (SelectedGroupIds.Contains(markerGroupId, StringComparer.Ordinal)
                && CurrentDocument?.Groups.Any(group => group.Kind == GraphGroupKind.Marker && string.Equals(group.Id, markerGroupId, StringComparison.Ordinal)) == true);
    }

    private static string CreateStressTechnicalDetails(
        GraphDemoTestSet testSet,
        IReadOnlyList<(int Iteration, GraphLayoutDirection Direction, GraphViewMode ViewMode, double LayoutMilliseconds, bool Succeeded, int ValidationIssueCount, long MemoryBytes)> samples,
        TimeSpan totalElapsed,
        long memoryBeforeBytes,
        long memoryAfterBytes,
        string? selectedMarkerGroupId,
        string? failureMessage)
    {
        List<string> lines =
        [
            "P5-004 Large rebuild stress acceptance",
            string.Empty,
            $"Testset: {testSet.Name}",
            testSet.Description,
            $"Nodes: {testSet.NodeCount}",
            $"Links: {testSet.LinkCount}",
            $"Groups: {testSet.GroupCount}",
            $"Rebuilds executed: {samples.Count}",
            $"Failures: {(failureMessage is null ? 0 : 1)}",
            $"Total elapsed: {totalElapsed.TotalMilliseconds:0.0} ms",
            $"Memory before full GC: {FormatBytes(memoryBeforeBytes)}",
            $"Memory after full GC: {FormatBytes(memoryAfterBytes)}",
            $"Memory delta after full GC: {FormatBytes(memoryAfterBytes - memoryBeforeBytes)}",
            $"Marker selection preserved: {(failureMessage == "Marker group selection was not preserved during rebuild." ? "no" : "yes")}",
            $"Selected marker group: {selectedMarkerGroupId ?? "none"}"
        ];

        if (samples.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add($"Layout min: {samples.Min(sample => sample.LayoutMilliseconds):0.0} ms");
            lines.Add($"Layout avg: {samples.Average(sample => sample.LayoutMilliseconds):0.0} ms");
            lines.Add($"Layout max: {samples.Max(sample => sample.LayoutMilliseconds):0.0} ms");
            lines.Add($"LeftToRight runs: {samples.Count(sample => sample.Direction == GraphLayoutDirection.LeftToRight)}");
            lines.Add($"TopToBottom runs: {samples.Count(sample => sample.Direction == GraphLayoutDirection.TopToBottom)}");
        }

        if (failureMessage is not null)
        {
            lines.Add(string.Empty);
            lines.Add("Failure:");
            lines.Add(failureMessage);
        }

        lines.Add(string.Empty);
        lines.Add("Samples:");
        foreach ((int iteration, GraphLayoutDirection direction, GraphViewMode viewMode, double layoutMilliseconds, bool succeeded, int validationIssueCount, long memoryBytes) in samples)
        {
            lines.Add($"- #{iteration:00}: {layoutMilliseconds,7:0.0} ms | {direction,-12} | {viewMode,-13} | success={succeeded} | validation issues={validationIssueCount} | memory={FormatBytes(memoryBytes)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatBytes(long bytes)
    {
        double megabytes = bytes / 1024d / 1024d;
        return $"{megabytes:0.00} MB";
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
