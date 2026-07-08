namespace VIA.WPF.Graph.Core.Requests;

/// <summary>
/// Validates neutral graph requests against host capabilities before host-side handling.
/// </summary>
public static class GraphRequestValidator
{
    public static GraphRequestValidationResult Validate(GraphRequest request, GraphHostCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(capabilities);

        if (capabilities.Supports(request))
        {
            return GraphRequestValidationResult.Success;
        }

        GraphRequestFeedbackIssue issue = new(
            GraphRequestFeedbackIssueCode.RequestKindNotSupported,
            GraphRequestFeedbackSeverity.Error,
            $"The host does not support graph request kind '{request.Kind}'.",
            GetSubjectId(request));

        return new GraphRequestValidationResult([issue]);
    }

    private static string? GetSubjectId(GraphRequest request)
    {
        return request.NodeId ?? request.LinkId ?? request.GroupId;
    }
}
