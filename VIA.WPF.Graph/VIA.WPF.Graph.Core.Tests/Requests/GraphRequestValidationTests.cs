using VIA.WPF.Graph.Core.Requests;

namespace VIA.WPF.Graph.Core.Tests.Requests;

public sealed class GraphRequestValidationTests
{
    [Fact]
    public void Validate_ReturnsSuccessForSupportedRequest()
    {
        GraphHostCapabilities capabilities = GraphHostCapabilities.ReadOnly(
            [GraphRequestKind.SelectNode]);

        GraphRequestValidationResult result = GraphRequestValidator.Validate(
            GraphRequest.SelectNode("node-1"),
            capabilities);

        Assert.True(result.IsValid);
        Assert.False(result.HasIssues);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_ReturnsFeedbackIssueForUnsupportedRequestKind()
    {
        GraphHostCapabilities capabilities = GraphHostCapabilities.ReadOnly(
            [GraphRequestKind.SelectNode]);

        GraphRequestValidationResult result = GraphRequestValidator.Validate(
            GraphRequest.OpenNode("node-1"),
            capabilities);

        Assert.False(result.IsValid);
        GraphRequestFeedbackIssue issue = Assert.Single(result.Issues);
        Assert.Equal(GraphRequestFeedbackIssueCode.RequestKindNotSupported, issue.Code);
        Assert.Equal(GraphRequestFeedbackSeverity.Error, issue.Severity);
        Assert.Equal("node-1", issue.SubjectId);
        Assert.Contains(nameof(GraphRequestKind.OpenNode), issue.Message);
    }

    [Fact]
    public void ValidationResult_CopiesIssues()
    {
        List<GraphRequestFeedbackIssue> issues =
        [
            new(
                GraphRequestFeedbackIssueCode.RequestValidationFailed,
                GraphRequestFeedbackSeverity.Warning,
                "warning")
        ];

        GraphRequestValidationResult result = new(issues);
        issues.Clear();

        Assert.True(result.IsValid);
        Assert.True(result.HasIssues);
        Assert.Single(result.Issues);
    }

    [Fact]
    public void FromValidation_ReturnsNotSupportedResultWithFeedback()
    {
        GraphRequest request = GraphRequest.OpenNode("node-1");
        GraphHostCapabilities capabilities = GraphHostCapabilities.ReadOnly(
            [GraphRequestKind.SelectNode]);

        GraphRequestValidationResult validationResult = GraphRequestValidator.Validate(request, capabilities);
        GraphRequestResult result = GraphRequestResult.FromValidation(request, validationResult);

        Assert.False(result.Succeeded);
        Assert.Equal(GraphRequestResultStatus.NotSupported, result.Status);
        Assert.True(result.HasFeedback);
        Assert.Equal(validationResult.Issues, result.FeedbackIssues);
        Assert.Equal(validationResult.Issues[0].Message, result.Message);
    }

    [Fact]
    public void NotSupportedFactory_AddsDefaultFeedbackIssue()
    {
        GraphRequest request = GraphRequest.OpenGroup("group-1");

        GraphRequestResult result = GraphRequestResult.NotSupported(request, "not available");

        Assert.False(result.Succeeded);
        Assert.Equal(GraphRequestResultStatus.NotSupported, result.Status);
        GraphRequestFeedbackIssue issue = Assert.Single(result.FeedbackIssues);
        Assert.Equal(GraphRequestFeedbackIssueCode.RequestKindNotSupported, issue.Code);
        Assert.Equal(GraphRequestFeedbackSeverity.Error, issue.Severity);
        Assert.Equal("group-1", issue.SubjectId);
        Assert.Equal("not available", issue.Message);
    }

    [Fact]
    public void Result_Constructor_CopiesFeedbackIssues()
    {
        List<GraphRequestFeedbackIssue> issues =
        [
            new(
                GraphRequestFeedbackIssueCode.RequestFailed,
                GraphRequestFeedbackSeverity.Error,
                "failed",
                "node-1")
        ];

        GraphRequestResult result = new(
            GraphRequest.SelectNode("node-1"),
            GraphRequestResultStatus.Failed,
            feedbackIssues: issues);
        issues.Clear();

        Assert.True(result.HasFeedback);
        Assert.Single(result.FeedbackIssues);
    }
}
