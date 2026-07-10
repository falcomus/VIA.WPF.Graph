using VIA.WPF.Graph.Core.Model;

namespace VIA.WPF.Graph.Core.Layout;

/// <summary>
/// Defines the neutral boundary used by WPF graph orchestration controls to request layout geometry.
/// Implementations may wrap Graphviz, a test layout engine or another host-approved layout adapter.
/// </summary>
public interface IGraphLayoutEngine
{
    GraphLayoutResult Layout(GraphDocument document, GraphLayoutOptions options);
}
