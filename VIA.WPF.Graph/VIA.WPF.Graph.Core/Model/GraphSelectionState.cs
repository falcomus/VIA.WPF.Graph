namespace VIA.WPF.Graph.Core.Model;

/// <summary>
/// Represents the neutral graph selection owned by a host view model.
/// </summary>
public sealed record GraphSelectionState
{
    public GraphSelectionState(
        IEnumerable<string>? selectedNodeIds = null,
        IEnumerable<string>? selectedLinkIds = null,
        IEnumerable<string>? selectedGroupIds = null)
    {
        SelectedNodeIds = CopyTextList(selectedNodeIds);
        SelectedLinkIds = CopyTextList(selectedLinkIds);
        SelectedGroupIds = CopyTextList(selectedGroupIds);
    }

    public IReadOnlyList<string> SelectedNodeIds { get; }

    public IReadOnlyList<string> SelectedLinkIds { get; }

    public IReadOnlyList<string> SelectedGroupIds { get; }

    public static GraphSelectionState Empty { get; } = new();

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
