namespace VIA.WPF.Graph.Core.Requests;

/// <summary>
/// Defines neutral requests emitted by graph UI components to a host.
/// </summary>
public enum GraphRequestKind
{
    SelectNode = 0,
    SelectLink = 1,
    SelectGroup = 2,
    ClearSelection = 3,
    OpenNode = 4,
    OpenLink = 5,
    OpenGroup = 6,
    ReturnToOverview = 7,
    SetGroupCollapsed = 8,
}
