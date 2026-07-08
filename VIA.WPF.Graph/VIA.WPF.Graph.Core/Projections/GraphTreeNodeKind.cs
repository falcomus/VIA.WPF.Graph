namespace VIA.WPF.Graph.Core.Projections;

/// <summary>
/// Defines the neutral role of a node inside a navigation tree projection.
/// </summary>
public enum GraphTreeNodeKind
{
    Root = 0,
    Branch = 1,
    Reference = 2,
    Terminal = 3,
    MissingTarget = 4,
}
