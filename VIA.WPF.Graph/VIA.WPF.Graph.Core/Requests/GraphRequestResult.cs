using System.Collections.ObjectModel;

namespace VIA.WPF.Graph.Core.Requests;

/// <summary>
/// Represents the neutral result of a graph request after host-side handling.
/// </summary>
public sealed record GraphRequestResult
{
    public GraphRequestResult(
        GraphRequest request,
        GraphRequestResultStatus status,
        string? message = null,
        IEnumerable<string>? affectedNodeIds = null,
        IEnumerable<string>? affectedLinkIds = null,
        IEnumerable<string>? affectedGroupIds = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        Request = request;
        Status = status;
        Message = NormalizeOptionalText(message);
        AffectedNodeIds = CopyTextList(affectedNodeIds);
        AffectedLinkIds = CopyTextList(affectedLinkIds);
        AffectedGroupIds = CopyTextList(affectedGroupIds);
        Metadata = CopyMetadata(metadata);
    }

    public GraphRequest Request { get; }

    public GraphRequestResultStatus Status { get; }

    public string? Message { get; }

    public IReadOnlyList<string> AffectedNodeIds { get; }

    public IReadOnlyList<string> AffectedLinkIds { get; }

    public IReadOnlyList<string> AffectedGroupIds { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public bool Succeeded => Status == GraphRequestResultStatus.Succeeded;

    public static GraphRequestResult Success(
        GraphRequest request,
        string? message = null,
        IEnumerable<string>? affectedNodeIds = null,
        IEnumerable<string>? affectedLinkIds = null,
        IEnumerable<string>? affectedGroupIds = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new GraphRequestResult(
            request,
            GraphRequestResultStatus.Succeeded,
            message,
            affectedNodeIds,
            affectedLinkIds,
            affectedGroupIds,
            metadata);
    }

    public static GraphRequestResult Rejected(
        GraphRequest request,
        string? message = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new GraphRequestResult(
            request,
            GraphRequestResultStatus.Rejected,
            message,
            metadata: metadata);
    }

    public static GraphRequestResult NotSupported(
        GraphRequest request,
        string? message = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new GraphRequestResult(
            request,
            GraphRequestResultStatus.NotSupported,
            message,
            metadata: metadata);
    }

    public static GraphRequestResult Failed(
        GraphRequest request,
        string? message = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new GraphRequestResult(
            request,
            GraphRequestResultStatus.Failed,
            message,
            metadata: metadata);
    }

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

    private static IReadOnlyDictionary<string, string> CopyMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        }

        return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata, StringComparer.Ordinal));
    }
}
