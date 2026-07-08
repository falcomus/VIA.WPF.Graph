namespace VIA.WPF.Graph.Core.Validation;

/// <summary>
/// Represents the result of neutral graph validation.
/// </summary>
public sealed record GraphValidationResult
{
    public GraphValidationResult(IEnumerable<GraphValidationIssue>? issues = null)
    {
        Issues = issues?.ToArray() ?? Array.Empty<GraphValidationIssue>();
    }

    public IReadOnlyList<GraphValidationIssue> Issues { get; }

    public bool IsValid => !Issues.Any(issue => issue.Severity == GraphValidationSeverity.Error);

    public static GraphValidationResult Success { get; } = new();
}
