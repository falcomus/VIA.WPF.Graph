using System.Collections.ObjectModel;

namespace VIA.WPF.Graph.Core.Model;

/// <summary>
/// Represents a neutral graph snapshot supplied by a host application.
/// </summary>
public sealed record GraphDocument
{
    public GraphDocument(
        string id,
        IEnumerable<GraphNode>? nodes = null,
        IEnumerable<GraphLink>? links = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Id = RequireText(id, nameof(id));
        Nodes = CopyItems(nodes);
        Links = CopyItems(links);
        Metadata = CopyMetadata(metadata);
    }

    public string Id { get; }

    public IReadOnlyList<GraphNode> Nodes { get; }

    public IReadOnlyList<GraphLink> Links { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null, empty or whitespace.", parameterName);
        }

        return value;
    }

    private static IReadOnlyList<T> CopyItems<T>(IEnumerable<T>? items)
    {
        return items?.ToArray() ?? Array.Empty<T>();
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
