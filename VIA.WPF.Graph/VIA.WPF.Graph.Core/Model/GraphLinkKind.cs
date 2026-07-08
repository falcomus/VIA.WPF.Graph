namespace VIA.WPF.Graph.Core.Model;

/// <summary>
/// Defines the neutral semantic category of a graph link.
/// </summary>
public enum GraphLinkKind
{
    Primary = 0,
    Secondary = 1,
    Back = 2,
    Cancel = 3,
    PopupOpen = 4,
    PopupClose = 5,
    External = 6,
    Reference = 7,
    Diagnostic = 8,
}
