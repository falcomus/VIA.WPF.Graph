using System.Collections.ObjectModel;

namespace VIA.WPF.Graph.Core.Requests;

/// <summary>
/// Describes which neutral graph interactions and edit operations a host is willing to handle.
/// The renderer uses this information only as a contract; the host remains owner of state and mutations.
/// </summary>
public sealed record GraphHostCapabilities
{
    private static readonly GraphRequestKind[] DefaultSupportedRequestKinds =
    [
        GraphRequestKind.SelectNode,
        GraphRequestKind.SelectLink,
        GraphRequestKind.SelectGroup,
        GraphRequestKind.ClearSelection,
        GraphRequestKind.OpenNode,
        GraphRequestKind.OpenLink,
        GraphRequestKind.OpenGroup,
        GraphRequestKind.ReturnToOverview,
        GraphRequestKind.SetGroupCollapsed
    ];

    public GraphHostCapabilities(
        GraphHostEditMode editMode = GraphHostEditMode.ReadOnly,
        IEnumerable<GraphRequestKind>? supportedRequestKinds = null,
        bool canCreateNodes = false,
        bool canCreateLinks = false,
        bool canRetargetLinks = false,
        bool canDeleteNodes = false,
        bool canDeleteLinks = false,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (!Enum.IsDefined(typeof(GraphHostEditMode), editMode))
        {
            throw new ArgumentOutOfRangeException(nameof(editMode), editMode, "Unsupported graph host edit mode.");
        }

        EditMode = editMode;
        SupportedRequestKinds = CopyRequestKinds(supportedRequestKinds ?? DefaultSupportedRequestKinds);
        CanCreateNodes = canCreateNodes;
        CanCreateLinks = canCreateLinks;
        CanRetargetLinks = canRetargetLinks;
        CanDeleteNodes = canDeleteNodes;
        CanDeleteLinks = canDeleteLinks;
        Metadata = CopyMetadata(metadata);

        if (IsReadOnly && HasGraphMutationCapabilities)
        {
            throw new ArgumentException("A read-only graph host must not expose graph mutation capabilities.");
        }
    }

    public GraphHostEditMode EditMode { get; }

    public IReadOnlyList<GraphRequestKind> SupportedRequestKinds { get; }

    public bool CanCreateNodes { get; }

    public bool CanCreateLinks { get; }

    public bool CanRetargetLinks { get; }

    public bool CanDeleteNodes { get; }

    public bool CanDeleteLinks { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public bool IsReadOnly => EditMode == GraphHostEditMode.ReadOnly;

    public bool IsEditable => EditMode == GraphHostEditMode.Editable;

    public bool HasGraphMutationCapabilities => CanCreateNodes
        || CanCreateLinks
        || CanRetargetLinks
        || CanDeleteNodes
        || CanDeleteLinks;

    public static GraphHostCapabilities ReadOnly(
        IEnumerable<GraphRequestKind>? supportedRequestKinds = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new GraphHostCapabilities(
            GraphHostEditMode.ReadOnly,
            supportedRequestKinds,
            metadata: metadata);
    }

    public static GraphHostCapabilities Editable(
        IEnumerable<GraphRequestKind>? supportedRequestKinds = null,
        bool canCreateNodes = true,
        bool canCreateLinks = true,
        bool canRetargetLinks = true,
        bool canDeleteNodes = true,
        bool canDeleteLinks = true,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new GraphHostCapabilities(
            GraphHostEditMode.Editable,
            supportedRequestKinds,
            canCreateNodes,
            canCreateLinks,
            canRetargetLinks,
            canDeleteNodes,
            canDeleteLinks,
            metadata);
    }

    public bool Supports(GraphRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Supports(request.Kind);
    }

    public bool Supports(GraphRequestKind requestKind)
    {
        if (!Enum.IsDefined(typeof(GraphRequestKind), requestKind))
        {
            return false;
        }

        return SupportedRequestKinds.Contains(requestKind);
    }

    private static IReadOnlyList<GraphRequestKind> CopyRequestKinds(IEnumerable<GraphRequestKind> requestKinds)
    {
        GraphRequestKind[] copiedRequestKinds = requestKinds
            .Select(requestKind =>
            {
                if (!Enum.IsDefined(typeof(GraphRequestKind), requestKind))
                {
                    throw new ArgumentOutOfRangeException(nameof(requestKinds), requestKind, "Unsupported graph request kind.");
                }

                return requestKind;
            })
            .Distinct()
            .ToArray();

        return copiedRequestKinds;
    }

    private static IReadOnlyDictionary<string, string> CopyMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        }

        return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata, StringComparer.Ordinal));
    }
}
