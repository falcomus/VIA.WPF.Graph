namespace VIA.WPF.Graph.Wpf.Controls;

/// <summary>
/// Defines the presentation role of one item in the WPF navigation tree.
/// </summary>
internal enum GraphNavigationTreeItemKind
{
    Group = 0,
    Node = 1,
    Reference = 2,
    Terminal = 3,
    MissingTarget = 4,
}
