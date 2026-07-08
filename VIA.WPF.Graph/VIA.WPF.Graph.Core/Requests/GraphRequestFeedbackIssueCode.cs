namespace VIA.WPF.Graph.Core.Requests;

/// <summary>
/// Defines stable codes for neutral graph request feedback.
/// </summary>
public enum GraphRequestFeedbackIssueCode
{
    RequestKindNotSupported = 0,
    RequestRejected = 1,
    RequestFailed = 2,
    RequestValidationFailed = 3,
}
