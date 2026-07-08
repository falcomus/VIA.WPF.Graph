namespace VIA.WPF.Graph.Core.Model;

/// <summary>
/// Represents host-owned neutral view state for a graph view.
/// </summary>
public sealed record GraphViewState
{
    public GraphViewState(
        GraphViewMode activeViewMode = GraphViewMode.Overview,
        string? activeNodeId = null,
        string? activeGroupId = null,
        GraphSelectionState? selection = null,
        GraphViewportState? viewport = null,
        IEnumerable<string>? collapsedContainerGroupIds = null,
        IEnumerable<string>? expandedTreeItemIds = null)
    {
        ActiveViewMode = activeViewMode;
        ActiveNodeId = NormalizeOptionalText(activeNodeId);
        ActiveGroupId = NormalizeOptionalText(activeGroupId);
        Selection = selection ?? GraphSelectionState.Empty;
        Viewport = viewport ?? GraphViewportState.Default;
        CollapsedContainerGroupIds = CopyTextList(collapsedContainerGroupIds);
        ExpandedTreeItemIds = CopyTextList(expandedTreeItemIds);
    }

    public GraphViewMode ActiveViewMode { get; }

    public string? ActiveNodeId { get; }

    public string? ActiveGroupId { get; }

    public GraphSelectionState Selection { get; }

    public GraphViewportState Viewport { get; }

    public IReadOnlyList<string> CollapsedContainerGroupIds { get; }

    public IReadOnlyList<string> ExpandedTreeItemIds { get; }

    public static GraphViewState Default { get; } = new();

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IReadOnlyList<string> CopyTextList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }
}
