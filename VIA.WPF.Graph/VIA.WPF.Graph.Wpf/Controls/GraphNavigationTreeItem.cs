using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VIA.WPF.Graph.Wpf.Controls;

/// <summary>
/// Represents one immutable navigation subject with mutable WPF container state.
/// Instances are fully built before they are assigned to a TreeView ItemsSource.
/// </summary>
internal sealed class GraphNavigationTreeItem : INotifyPropertyChanged
{
    private IReadOnlyList<GraphNavigationTreeItem> children = Array.Empty<GraphNavigationTreeItem>();
    private bool isExpanded;
    private bool isSelected;

    public GraphNavigationTreeItem(
        string treeNodeId,
        string title,
        GraphNavigationTreeItemKind kind,
        string? subtitle = null,
        string? groupId = null,
        string? nodeId = null,
        string? linkId = null,
        bool isExpanded = false)
    {
        TreeNodeId = RequireText(treeNodeId, nameof(treeNodeId));
        Title = RequireText(title, nameof(title));
        Kind = kind;
        Subtitle = NormalizeOptionalText(subtitle);
        GroupId = NormalizeOptionalText(groupId);
        NodeId = NormalizeOptionalText(nodeId);
        LinkId = NormalizeOptionalText(linkId);
        this.isExpanded = isExpanded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TreeNodeId { get; }

    public string Title { get; }

    public string? Subtitle { get; }

    public GraphNavigationTreeItemKind Kind { get; }

    public string? GroupId { get; }

    public string? NodeId { get; }

    public string? LinkId { get; }

    public IReadOnlyList<GraphNavigationTreeItem> Children => children;

    public GraphNavigationTreeItem? Parent { get; private set; }

    public bool HasChildren => children.Count > 0;

    public bool IsGroup => Kind == GraphNavigationTreeItemKind.Group;

    public bool IsReference => Kind == GraphNavigationTreeItemKind.Reference;

    public bool IsTerminal => Kind == GraphNavigationTreeItemKind.Terminal;

    public bool IsMissingTarget => Kind == GraphNavigationTreeItemKind.MissingTarget;

    public bool IsExpanded
    {
        get => isExpanded;
        set => SetField(ref isExpanded, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetField(ref isSelected, value);
    }

    public void SetChildren(IEnumerable<GraphNavigationTreeItem>? values)
    {
        GraphNavigationTreeItem[] nextChildren = values?.ToArray() ?? Array.Empty<GraphNavigationTreeItem>();
        foreach (GraphNavigationTreeItem child in nextChildren)
        {
            child.Parent = this;
        }

        children = nextChildren;
        OnPropertyChanged(nameof(Children));
        OnPropertyChanged(nameof(HasChildren));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null, empty or whitespace.", parameterName);
        }

        return value;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
