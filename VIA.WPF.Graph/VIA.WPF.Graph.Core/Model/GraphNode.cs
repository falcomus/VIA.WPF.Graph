using System.Collections.ObjectModel;

namespace VIA.WPF.Graph.Core.Model;

/// <summary>
/// Describes a neutral graph node in a host-provided graph snapshot.
/// </summary>
public sealed record GraphNode
{
    public GraphNode(
        string id,
        string title,
        string? description = null,
        GraphNodeKind kind = GraphNodeKind.Standard,
        GraphSize? defaultSize = null,
        string? visualStyleKey = null,
        IEnumerable<string>? groupMemberships = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Id = RequireText(id, nameof(id));
        Title = RequireText(title, nameof(title));
        Description = description;
        Kind = kind;
        DefaultSize = defaultSize ?? GraphSize.Standard;
        VisualStyleKey = visualStyleKey;
        GroupMemberships = CopyTextList(groupMemberships);
        Metadata = CopyMetadata(metadata);
    }

    public string Id { get; }

    public string Title { get; }

    public string? Description { get; }

    public GraphNodeKind Kind { get; }

    public GraphSize DefaultSize { get; }

    public string? VisualStyleKey { get; }

    public IReadOnlyList<string> GroupMemberships { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null, empty or whitespace.", parameterName);
        }

        return value;
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

    private static IReadOnlyDictionary<string, string> CopyMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        }

        return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata, StringComparer.Ordinal));
    }
}
