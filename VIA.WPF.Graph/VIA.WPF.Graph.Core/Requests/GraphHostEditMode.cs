namespace VIA.WPF.Graph.Core.Requests;

/// <summary>
/// Defines whether a host exposes only read-only interaction or graph-editing capabilities.
/// </summary>
public enum GraphHostEditMode
{
    /// <summary>
    /// The host allows visualization and non-mutating interaction only.
    /// </summary>
    ReadOnly = 0,

    /// <summary>
    /// The host may accept graph mutation requests according to its explicit capabilities.
    /// </summary>
    Editable = 1,
}
