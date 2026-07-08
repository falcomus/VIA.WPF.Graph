namespace VIA.WPF.Graph.Core.Model;

/// <summary>
/// Defines how a neutral graph group may be interpreted by projections and renderers.
/// </summary>
public enum GraphGroupKind
{
    /// <summary>
    /// A disjoint or hierarchical group that may later be used as a layout container.
    /// </summary>
    Container = 0,

    /// <summary>
    /// An overlapping marker group used for filtering, highlighting or selection.
    /// </summary>
    Marker = 1,
}
