namespace VIA.WPF.Graph.Core.Requests;

/// <summary>
/// Defines the host-side boundary for handling neutral graph requests.
/// Implementations belong to the host application and must not be provided by the graph renderer.
/// </summary>
public interface IGraphRequestHandler
{
    GraphHostCapabilities Capabilities { get; }

    ValueTask<GraphRequestResult> HandleAsync(GraphRequest request, CancellationToken cancellationToken = default);
}
