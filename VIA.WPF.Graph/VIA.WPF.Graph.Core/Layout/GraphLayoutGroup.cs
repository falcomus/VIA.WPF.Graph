namespace VIA.WPF.Graph.Core.Layout;

/// <summary>
/// Represents the layout bounds for one graph group or cluster.
/// </summary>
public sealed record GraphLayoutGroup
{
    public GraphLayoutGroup(string groupId, GraphRect bounds)
    {
        GroupId = RequireText(groupId, nameof(groupId));
        Bounds = bounds;
    }

    public string GroupId { get; }

    public GraphRect Bounds { get; }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null, empty or whitespace.", parameterName);
        }

        return value;
    }
}
