using System.Collections.ObjectModel;

namespace VIA.WPF.Graph.Core.Model;

/// <summary>
/// Describes a neutral graph group in a host-provided graph snapshot.
/// </summary>
public sealed record GraphGroup
{
    public GraphGroup(
        string id,
        string title,
        GraphGroupKind kind,
        string? description = null,
        string? parentGroupId = null,
        string? visualStyleKey = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Id = RequireText(id, nameof(id));
        Title = RequireText(title, nameof(title));
        Kind = kind;
        Description = description;
        ParentGroupId = string.IsNullOrWhiteSpace(parentGroupId) ? null : parentGroupId;
        VisualStyleKey = visualStyleKey;
        Metadata = CopyMetadata(metadata);
    }

    public string Id { get; }

    public string Title { get; }

    public GraphGroupKind Kind { get; }

    public string? Description { get; }

    /// <summary>
    /// Optional parent group identifier for hierarchical container groups.
    /// It is ignored for marker groups until validation rules are added.
    /// </summary>
    public string? ParentGroupId { get; }

    public string? VisualStyleKey { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null, empty or whitespace.", parameterName);
        }

        return value;
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
