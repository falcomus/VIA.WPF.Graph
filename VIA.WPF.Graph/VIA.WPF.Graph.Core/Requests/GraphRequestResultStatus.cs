namespace VIA.WPF.Graph.Core.Requests;

/// <summary>
/// Defines the neutral outcome status for a graph request handled by a host.
/// </summary>
public enum GraphRequestResultStatus
{
    /// <summary>
    /// The host accepted and handled the request.
    /// </summary>
    Succeeded = 0,

    /// <summary>
    /// The request was valid, but the host deliberately rejected it.
    /// </summary>
    Rejected = 1,

    /// <summary>
    /// The host does not support this request kind or subject.
    /// </summary>
    NotSupported = 2,

    /// <summary>
    /// The host attempted to handle the request, but handling failed.
    /// </summary>
    Failed = 3,
}
