using System.Collections.ObjectModel;

namespace VIA.WPF.Graph.Core.Model;

/// <summary>
/// Describes a neutral graph link in a host-provided graph snapshot.
/// </summary>
public sealed record GraphLink
{
    public GraphLink(
        string id,
        string sourceNodeId,
        string targetNodeId,
        GraphLinkDirection direction = GraphLinkDirection.Directed,
        GraphLinkKind kind = GraphLinkKind.Secondary,
        string? label = null,
        GraphLineStyle lineStyle = GraphLineStyle.Solid,
        double weight = 1,
        bool isLayoutConstraint = true,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Id = RequireText(id, nameof(id));
        SourceNodeId = RequireText(sourceNodeId, nameof(sourceNodeId));
        TargetNodeId = RequireText(targetNodeId, nameof(targetNodeId));
        ArgumentOutOfRangeException.ThrowIfNegative(weight);

        Direction = direction;
        Kind = kind;
        Label = label;
        LineStyle = lineStyle;
        Weight = weight;
        IsLayoutConstraint = isLayoutConstraint;
        Metadata = CopyMetadata(metadata);
    }

    public string Id { get; }

    public string SourceNodeId { get; }

    public string TargetNodeId { get; }

    public GraphLinkDirection Direction { get; }

    public GraphLinkKind Kind { get; }

    public string? Label { get; }

    public GraphLineStyle LineStyle { get; }

    public double Weight { get; }

    public bool IsLayoutConstraint { get; }

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
