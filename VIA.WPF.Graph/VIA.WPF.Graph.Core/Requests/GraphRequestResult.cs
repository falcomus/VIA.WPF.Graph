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
        IReadOnlyDictionary<string, string>? metadata = null,
        IEnumerable<GraphRequestFeedbackIssue>? feedbackIssues = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(typeof(GraphRequestResultStatus), status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported graph request result status.");
        }

        Request = request;
        Status = status;
        Message = NormalizeOptionalText(message);
        AffectedNodeIds = CopyTextList(affectedNodeIds);
        AffectedLinkIds = CopyTextList(affectedLinkIds);
        AffectedGroupIds = CopyTextList(affectedGroupIds);
        Metadata = CopyMetadata(metadata);
        FeedbackIssues = CopyFeedbackIssues(feedbackIssues);
    }

    public GraphRequest Request { get; }

    public GraphRequestResultStatus Status { get; }

    public string? Message { get; }

    public IReadOnlyList<string> AffectedNodeIds { get; }

    public IReadOnlyList<string> AffectedLinkIds { get; }

    public IReadOnlyList<string> AffectedGroupIds { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public IReadOnlyList<GraphRequestFeedbackIssue> FeedbackIssues { get; }

    public bool Succeeded => Status == GraphRequestResultStatus.Succeeded;

    public bool HasFeedback => FeedbackIssues.Count > 0;

    public static GraphRequestResult Success(
        GraphRequest request,
        string? message = null,
        IEnumerable<string>? affectedNodeIds = null,
        IEnumerable<string>? affectedLinkIds = null,
        IEnumerable<string>? affectedGroupIds = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        IEnumerable<GraphRequestFeedbackIssue>? feedbackIssues = null)
    {
        return new GraphRequestResult(
            request,
            GraphRequestResultStatus.Succeeded,
            message,
            affectedNodeIds,
            affectedLinkIds,
            affectedGroupIds,
            metadata,
            feedbackIssues);
    }

    public static GraphRequestResult Rejected(
        GraphRequest request,
        string? message = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        IEnumerable<GraphRequestFeedbackIssue>? feedbackIssues = null)
    {
        return new GraphRequestResult(
            request,
            GraphRequestResultStatus.Rejected,
            message,
            metadata: metadata,
            feedbackIssues: feedbackIssues ?? [CreateDefaultFeedbackIssue(
                request,
                GraphRequestFeedbackIssueCode.RequestRejected,
                message,
                "The host rejected the graph request.")]);
    }

    public static GraphRequestResult NotSupported(
        GraphRequest request,
        string? message = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        IEnumerable<GraphRequestFeedbackIssue>? feedbackIssues = null)
    {
        return new GraphRequestResult(
            request,
            GraphRequestResultStatus.NotSupported,
            message,
            metadata: metadata,
            feedbackIssues: feedbackIssues ?? [CreateDefaultFeedbackIssue(
                request,
                GraphRequestFeedbackIssueCode.RequestKindNotSupported,
                message,
                "The host does not support the graph request.")]);
    }

    public static GraphRequestResult Failed(
        GraphRequest request,
        string? message = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        IEnumerable<GraphRequestFeedbackIssue>? feedbackIssues = null)
    {
        return new GraphRequestResult(
            request,
            GraphRequestResultStatus.Failed,
            message,
            metadata: metadata,
            feedbackIssues: feedbackIssues ?? [CreateDefaultFeedbackIssue(
                request,
                GraphRequestFeedbackIssueCode.RequestFailed,
                message,
                "The host failed while handling the graph request.")]);
    }

    public static GraphRequestResult FromValidation(
        GraphRequest request,
        GraphRequestValidationResult validationResult,
        string? message = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(validationResult);

        if (validationResult.IsValid)
        {
            return Success(request, message, metadata: metadata, feedbackIssues: validationResult.Issues);
        }

        string resolvedMessage = NormalizeOptionalText(message)
            ?? validationResult.Issues.FirstOrDefault(issue => issue.Severity == GraphRequestFeedbackSeverity.Error)?.Message
            ?? "The graph request is not valid for the current host.";

        bool isNotSupported = validationResult.Issues.Any(issue => issue.Code == GraphRequestFeedbackIssueCode.RequestKindNotSupported);
        return new GraphRequestResult(
            request,
            isNotSupported ? GraphRequestResultStatus.NotSupported : GraphRequestResultStatus.Rejected,
            resolvedMessage,
            metadata: metadata,
            feedbackIssues: validationResult.Issues);
    }

    private static GraphRequestFeedbackIssue CreateDefaultFeedbackIssue(
        GraphRequest request,
        GraphRequestFeedbackIssueCode code,
        string? message,
        string fallbackMessage)
    {
        return new GraphRequestFeedbackIssue(
            code,
            GraphRequestFeedbackSeverity.Error,
            NormalizeOptionalText(message) ?? fallbackMessage,
            request.NodeId ?? request.LinkId ?? request.GroupId);
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

    private static IReadOnlyList<GraphRequestFeedbackIssue> CopyFeedbackIssues(IEnumerable<GraphRequestFeedbackIssue>? issues)
    {
        return issues?.ToArray() ?? Array.Empty<GraphRequestFeedbackIssue>();
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
