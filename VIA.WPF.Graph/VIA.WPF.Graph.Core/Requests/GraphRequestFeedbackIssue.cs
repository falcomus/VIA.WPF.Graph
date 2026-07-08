namespace VIA.WPF.Graph.Core.Requests;

/// <summary>
/// Describes one neutral validation or error feedback item for a graph request.
/// </summary>
public sealed record GraphRequestFeedbackIssue
{
    public GraphRequestFeedbackIssue(
        GraphRequestFeedbackIssueCode code,
        GraphRequestFeedbackSeverity severity,
        string message,
        string? subjectId = null)
    {
        if (!Enum.IsDefined(typeof(GraphRequestFeedbackIssueCode), code))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Unsupported graph request feedback issue code.");
        }

        if (!Enum.IsDefined(typeof(GraphRequestFeedbackSeverity), severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unsupported graph request feedback severity.");
        }

        Code = code;
        Severity = severity;
        Message = RequireText(message, nameof(message));
        SubjectId = NormalizeOptionalText(subjectId);
    }

    public GraphRequestFeedbackIssueCode Code { get; }

    public GraphRequestFeedbackSeverity Severity { get; }

    public string Message { get; }

    public string? SubjectId { get; }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null, empty or whitespace.", parameterName);
        }

        return value;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
