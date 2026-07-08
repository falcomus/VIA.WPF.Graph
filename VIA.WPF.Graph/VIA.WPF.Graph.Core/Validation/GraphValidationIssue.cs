namespace VIA.WPF.Graph.Core.Validation;

/// <summary>
/// Describes one neutral graph validation issue without renderer or host dependencies.
/// </summary>
public sealed record GraphValidationIssue(
    GraphValidationIssueCode Code,
    GraphValidationSeverity Severity,
    string Message,
    string? SubjectId = null,
    string? RelatedId = null);
