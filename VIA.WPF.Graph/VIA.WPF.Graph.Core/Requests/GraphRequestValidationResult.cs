namespace VIA.WPF.Graph.Core.Requests;

/// <summary>
/// Represents neutral validation feedback for a graph request before host-side handling.
/// </summary>
public sealed record GraphRequestValidationResult
{
    public GraphRequestValidationResult(IEnumerable<GraphRequestFeedbackIssue>? issues = null)
    {
        Issues = issues?.ToArray() ?? Array.Empty<GraphRequestFeedbackIssue>();
    }

    public IReadOnlyList<GraphRequestFeedbackIssue> Issues { get; }

    public bool IsValid => !Issues.Any(issue => issue.Severity == GraphRequestFeedbackSeverity.Error);

    public bool HasIssues => Issues.Count > 0;

    public static GraphRequestValidationResult Success { get; } = new();
}
