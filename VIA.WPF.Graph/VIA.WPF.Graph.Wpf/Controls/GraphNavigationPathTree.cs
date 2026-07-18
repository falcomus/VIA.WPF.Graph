using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Core.Projections;
using VIA.WPF.Graph.Core.Requests;

namespace VIA.WPF.Graph.Wpf.Controls;

/// <summary>
/// Native WPF navigation tree for grouped, cycle-safe graph projections.
/// The control uses TreeViewItem containers for selection, expansion, keyboard navigation and accessibility.
/// </summary>
public sealed class GraphNavigationPathTree : TreeView
{
    public static readonly DependencyProperty DocumentProperty = DependencyProperty.Register(
        nameof(Document),
        typeof(GraphDocument),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(null, OnNavigationInputChanged));

    public static readonly DependencyProperty ProjectionProperty = DependencyProperty.Register(
        nameof(Projection),
        typeof(GraphTreeProjection),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(null, OnNavigationInputChanged));

    public static readonly DependencyProperty SelectedTreeNodeIdProperty = DependencyProperty.Register(
        nameof(SelectedTreeNodeId),
        typeof(string),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnSelectedTreeNodeIdChanged));

    public static readonly DependencyProperty SelectedNodeIdProperty = DependencyProperty.Register(
        nameof(SelectedNodeId),
        typeof(string),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty SelectedLinkIdProperty = DependencyProperty.Register(
        nameof(SelectedLinkId),
        typeof(string),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty GraphSelectedNodeIdsProperty = DependencyProperty.Register(
        nameof(GraphSelectedNodeIds),
        typeof(IReadOnlyList<string>),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(
            Array.Empty<string>(),
            OnGraphSelectionChanged));

    public static readonly DependencyProperty GraphSelectedLinkIdsProperty = DependencyProperty.Register(
        nameof(GraphSelectedLinkIds),
        typeof(IReadOnlyList<string>),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(
            Array.Empty<string>(),
            OnGraphSelectionChanged));

    public static readonly DependencyProperty GraphSelectedGroupIdsProperty = DependencyProperty.Register(
        nameof(GraphSelectedGroupIds),
        typeof(IReadOnlyList<string>),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(
            Array.Empty<string>(),
            OnGraphSelectionChanged));

    public static readonly DependencyProperty GraphRequestCommandProperty = DependencyProperty.Register(
        nameof(GraphRequestCommand),
        typeof(ICommand),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(null));

    private const string NavigationTreeStyleKey = "GraphWorkspace.NavigationTreeStyle";
    private const int BringIntoViewMaxAttempts = 4;

    private IReadOnlyList<GraphNavigationTreeItem> navigationItems = Array.Empty<GraphNavigationTreeItem>();
    private IReadOnlyList<GraphNavigationTreeItem> flattenedItems = Array.Empty<GraphNavigationTreeItem>();
    private HashSet<string> collapsedGroupIds = new(StringComparer.Ordinal);
    private GraphNavigationTreeItem? selectedNavigationItem;
    private int selectionSynchronizationDepth;
    private bool isGraphSelectionBatchUpdate;
    private bool isExecutingGraphRequest;
    private bool isGraphSelectionApplyPending;
    private bool isNavigationRebuildSuppressed;
    private bool isNavigationRebuildScheduled;
    private bool isNavigationRebuildInProgress;
    private bool isNavigationRebuildRequested;
    private GraphNavigationTreeItem? pendingBringIntoViewItem;
    private bool isBringIntoViewPassScheduled;
    private int bringIntoViewRequestVersion;
    private int pendingBringIntoViewRequestVersion;
    private int pendingBringIntoViewAttempt;

    public GraphNavigationPathTree()
    {
        Focusable = true;
        SetResourceReference(StyleProperty, NavigationTreeStyleKey);

        SelectedItemChanged += OnSelectedItemChanged;
        PreviewMouseDoubleClick += OnPreviewMouseDoubleClick;
        AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(OnTreeItemExpansionChanged));
        AddHandler(TreeViewItem.CollapsedEvent, new RoutedEventHandler(OnTreeItemExpansionChanged));
    }

    public GraphDocument? Document
    {
        get => (GraphDocument?)GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public GraphTreeProjection? Projection
    {
        get => (GraphTreeProjection?)GetValue(ProjectionProperty);
        set => SetValue(ProjectionProperty, value);
    }

    public string? SelectedTreeNodeId
    {
        get => (string?)GetValue(SelectedTreeNodeIdProperty);
        set => SetValue(SelectedTreeNodeIdProperty, NormalizeOptionalText(value));
    }

    public string? SelectedNodeId
    {
        get => (string?)GetValue(SelectedNodeIdProperty);
        set => SetValue(SelectedNodeIdProperty, NormalizeOptionalText(value));
    }

    public string? SelectedLinkId
    {
        get => (string?)GetValue(SelectedLinkIdProperty);
        set => SetValue(SelectedLinkIdProperty, NormalizeOptionalText(value));
    }

    public IReadOnlyList<string> GraphSelectedNodeIds
    {
        get => (IReadOnlyList<string>?)GetValue(GraphSelectedNodeIdsProperty) ?? Array.Empty<string>();
        set => SetValue(GraphSelectedNodeIdsProperty, CopySelection(value));
    }

    public IReadOnlyList<string> GraphSelectedLinkIds
    {
        get => (IReadOnlyList<string>?)GetValue(GraphSelectedLinkIdsProperty) ?? Array.Empty<string>();
        set => SetValue(GraphSelectedLinkIdsProperty, CopySelection(value));
    }

    public IReadOnlyList<string> GraphSelectedGroupIds
    {
        get => (IReadOnlyList<string>?)GetValue(GraphSelectedGroupIdsProperty) ?? Array.Empty<string>();
        set => SetValue(GraphSelectedGroupIdsProperty, CopySelection(value));
    }

    public ICommand? GraphRequestCommand
    {
        get => (ICommand?)GetValue(GraphRequestCommandProperty);
        set => SetValue(GraphRequestCommandProperty, value);
    }

    public bool SelectTreeNode(string treeNodeId)
    {
        GraphNavigationTreeItem? item = FindItem(treeNodeId);
        if (item is null)
        {
            return false;
        }

        SetSelectedItem(item);
        ExecuteSelectionRequest(item);
        return true;
    }

    public bool OpenTreeNode(string treeNodeId)
    {
        GraphNavigationTreeItem? item = FindItem(treeNodeId);
        if (item is null)
        {
            return false;
        }

        SetSelectedItem(item);
        ExecuteOpenRequest(item);
        return true;
    }

    public bool BringSelectedTreeNodeIntoView()
    {
        GraphNavigationTreeItem? selectedItem = FindItem(SelectedTreeNodeId);
        if (selectedItem is null)
        {
            return false;
        }

        pendingBringIntoViewItem = selectedItem;
        pendingBringIntoViewRequestVersion = ++bringIntoViewRequestVersion;
        pendingBringIntoViewAttempt = 0;
        QueueBringIntoViewPass(DispatcherPriority.Loaded);
        return true;
    }

    internal void SetNavigationData(GraphDocument? document, GraphTreeProjection? projection)
    {
        if (ReferenceEquals(Document, document) && AreEquivalentProjections(Projection, projection))
        {
            return;
        }

        isNavigationRebuildSuppressed = true;
        try
        {
            SetCurrentValue(DocumentProperty, document);
            SetCurrentValue(ProjectionProperty, projection);
        }
        finally
        {
            isNavigationRebuildSuppressed = false;
        }

        ScheduleNavigationRebuild();
    }

    internal void ApplyGraphSelection(
        IReadOnlyList<string>? selectedNodeIds,
        IReadOnlyList<string>? selectedLinkIds,
        IReadOnlyList<string>? selectedGroupIds)
    {
        isGraphSelectionBatchUpdate = true;
        try
        {
            SetCurrentValue(GraphSelectedNodeIdsProperty, CopySelection(selectedNodeIds));
            SetCurrentValue(GraphSelectedLinkIdsProperty, CopySelection(selectedLinkIds));
            SetCurrentValue(GraphSelectedGroupIdsProperty, CopySelection(selectedGroupIds));
        }
        finally
        {
            isGraphSelectionBatchUpdate = false;
        }

        SynchronizeGraphSelectionFromGraphState();
    }

    internal void ApplyCollapsedGroupIds(IReadOnlyList<string> groupIds)
    {
        collapsedGroupIds = new HashSet<string>(CopySelection(groupIds), StringComparer.Ordinal);

        foreach (GraphNavigationTreeItem item in flattenedItems.Where(item => item.IsGroup && item.GroupId is not null))
        {
            item.IsExpanded = !collapsedGroupIds.Contains(item.GroupId!);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SelectedItem is GraphNavigationTreeItem item)
        {
            ExecuteOpenRequest(item);
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private static void OnNavigationInputChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphNavigationPathTree tree = (GraphNavigationPathTree)dependencyObject;
        if (!tree.isNavigationRebuildSuppressed)
        {
            tree.ScheduleNavigationRebuild();
        }
    }

    private static void OnSelectedTreeNodeIdChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphNavigationPathTree tree = (GraphNavigationPathTree)dependencyObject;
        if (tree.IsSynchronizingSelection)
        {
            return;
        }

        string? treeNodeId = NormalizeOptionalText((string?)eventArgs.NewValue);
        if (treeNodeId is null)
        {
            tree.ClearSelection();
            return;
        }

        GraphNavigationTreeItem? item = tree.FindItem(treeNodeId);
        if (item is not null)
        {
            tree.SetSelectedItem(item);
        }
    }

    private static void OnGraphSelectionChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphNavigationPathTree tree = (GraphNavigationPathTree)dependencyObject;
        if (!tree.isGraphSelectionBatchUpdate)
        {
            tree.SynchronizeGraphSelectionFromGraphState();
        }
    }

    private void ScheduleNavigationRebuild()
    {
        if (isNavigationRebuildInProgress)
        {
            isNavigationRebuildRequested = true;
            return;
        }

        if (!IsLoaded)
        {
            RebuildNavigationItems();
            return;
        }

        QueueNavigationRebuildPass();
    }

    private void QueueNavigationRebuildPass()
    {
        if (isNavigationRebuildScheduled)
        {
            return;
        }

        isNavigationRebuildScheduled = true;
        Dispatcher.BeginInvoke(new Action(ProcessNavigationRebuildPass), DispatcherPriority.ContextIdle);
    }

    private void ProcessNavigationRebuildPass()
    {
        isNavigationRebuildScheduled = false;

        if (IsAnyItemContainerGeneratorBusy())
        {
            QueueNavigationRebuildPass();
            return;
        }

        RebuildNavigationItems();
    }

    private void RebuildNavigationItems()
    {
        if (isNavigationRebuildInProgress)
        {
            isNavigationRebuildRequested = true;
            return;
        }

        isNavigationRebuildInProgress = true;
        try
        {
            IReadOnlyList<GraphNavigationTreeItem> nextItems = GraphNavigationTreeBuilder.Build(Document, Projection, collapsedGroupIds);
            IReadOnlyList<GraphNavigationTreeItem> nextFlattenedItems = FlattenItems(nextItems);

            selectionSynchronizationDepth++;
            try
            {
                navigationItems = nextItems;
                flattenedItems = nextFlattenedItems;
                selectedNavigationItem = null;
                SetCurrentValue(ItemsSourceProperty, navigationItems);
                ApplyGraphSelectionCore();
            }
            finally
            {
                selectionSynchronizationDepth--;
            }
        }
        finally
        {
            isNavigationRebuildInProgress = false;
            if (isNavigationRebuildRequested)
            {
                isNavigationRebuildRequested = false;
                QueueNavigationRebuildPass();
            }
        }
    }

    private bool IsAnyItemContainerGeneratorBusy()
    {
        Stack<ItemsControl> pending = new();
        pending.Push(this);

        while (pending.Count > 0)
        {
            ItemsControl current = pending.Pop();
            if (current.ItemContainerGenerator.Status == GeneratorStatus.GeneratingContainers)
            {
                return true;
            }

            for (int index = 0; index < current.Items.Count; index++)
            {
                if (current.ItemContainerGenerator.ContainerFromIndex(index) is TreeViewItem childContainer)
                {
                    pending.Push(childContainer);
                }
            }
        }

        return false;
    }

    private static IReadOnlyList<GraphNavigationTreeItem> FlattenItems(IEnumerable<GraphNavigationTreeItem> roots)
    {
        GraphNavigationTreeItem[] rootItems = roots.ToArray();
        List<GraphNavigationTreeItem> items = [];
        Stack<GraphNavigationTreeItem> pending = new();

        for (int index = rootItems.Length - 1; index >= 0; index--)
        {
            pending.Push(rootItems[index]);
        }

        while (pending.Count > 0)
        {
            GraphNavigationTreeItem item = pending.Pop();
            items.Add(item);

            for (int index = item.Children.Count - 1; index >= 0; index--)
            {
                pending.Push(item.Children[index]);
            }
        }

        return items;
    }

    private bool IsSynchronizingSelection => selectionSynchronizationDepth > 0;

    private void SynchronizeGraphSelectionFromGraphState()
    {
        if (isExecutingGraphRequest)
        {
            isGraphSelectionApplyPending = true;
            return;
        }

        if (IsSynchronizingSelection)
        {
            return;
        }

        selectionSynchronizationDepth++;
        try
        {
            ApplyGraphSelectionCore();
        }
        finally
        {
            selectionSynchronizationDepth--;
        }
    }

    private void ApplyGraphSelectionCore()
    {
        GraphNavigationTreeItem? selectedItem = ResolveGraphSelectedItem();
        SetSelectedItemCore(selectedItem);
    }

    private GraphNavigationTreeItem? ResolveGraphSelectedItem()
    {
        foreach (string selectedLinkId in GraphSelectedLinkIds)
        {
            GraphNavigationTreeItem? linkItem = flattenedItems.FirstOrDefault(item =>
                item.LinkId is not null
                && StringComparer.Ordinal.Equals(item.LinkId, selectedLinkId));
            if (linkItem is not null)
            {
                return linkItem;
            }
        }

        foreach (string selectedGroupId in GraphSelectedGroupIds)
        {
            GraphNavigationTreeItem? groupItem = flattenedItems.FirstOrDefault(item =>
                item.GroupId is not null
                && item.IsGroup
                && StringComparer.Ordinal.Equals(item.GroupId, selectedGroupId));
            if (groupItem is not null)
            {
                return groupItem;
            }
        }

        foreach (string selectedNodeId in GraphSelectedNodeIds)
        {
            GraphNavigationTreeItem? nodeItem = flattenedItems.FirstOrDefault(item =>
                    item.NodeId is not null
                    && StringComparer.Ordinal.Equals(item.NodeId, selectedNodeId)
                    && !item.IsReference
                    && !item.IsMissingTarget)
                ?? flattenedItems.FirstOrDefault(item =>
                    item.NodeId is not null
                    && StringComparer.Ordinal.Equals(item.NodeId, selectedNodeId));
            if (nodeItem is not null)
            {
                return nodeItem;
            }
        }

        return null;
    }

    private void SetSelectedItem(GraphNavigationTreeItem item)
    {
        selectionSynchronizationDepth++;
        try
        {
            SetSelectedItemCore(item);
        }
        finally
        {
            selectionSynchronizationDepth--;
        }
    }

    private void SetSelectedItemCore(GraphNavigationTreeItem? item)
    {
        if (!ReferenceEquals(selectedNavigationItem, item))
        {
            if (selectedNavigationItem is not null && selectedNavigationItem.IsSelected)
            {
                selectedNavigationItem.IsSelected = false;
            }

            selectedNavigationItem = item;
        }

        if (selectedNavigationItem is not null && !selectedNavigationItem.IsSelected)
        {
            selectedNavigationItem.IsSelected = true;
        }

        SetCurrentValue(SelectedTreeNodeIdProperty, item?.TreeNodeId);
        SetCurrentValue(SelectedNodeIdProperty, item?.NodeId);
        SetCurrentValue(SelectedLinkIdProperty, item?.LinkId);
    }

    private void ClearSelection()
    {
        selectionSynchronizationDepth++;
        try
        {
            SetSelectedItemCore(null);
        }
        finally
        {
            selectionSynchronizationDepth--;
        }
    }

    private void ClearSelectionCore()
    {
        SetSelectedItemCore(null);
    }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (IsSynchronizingSelection)
        {
            return;
        }

        if (e.NewValue is not GraphNavigationTreeItem item)
        {
            selectionSynchronizationDepth++;
            try
            {
                selectedNavigationItem = null;
                SetCurrentValue(SelectedTreeNodeIdProperty, null);
                SetCurrentValue(SelectedNodeIdProperty, null);
                SetCurrentValue(SelectedLinkIdProperty, null);
            }
            finally
            {
                selectionSynchronizationDepth--;
            }

            return;
        }

        bool isSameSelection = ReferenceEquals(selectedNavigationItem, item)
            && StringComparer.Ordinal.Equals(SelectedTreeNodeId, item.TreeNodeId);

        selectionSynchronizationDepth++;
        try
        {
            selectedNavigationItem = item;
            SetCurrentValue(SelectedTreeNodeIdProperty, item.TreeNodeId);
            SetCurrentValue(SelectedNodeIdProperty, item.NodeId);
            SetCurrentValue(SelectedLinkIdProperty, item.LinkId);
        }
        finally
        {
            selectionSynchronizationDepth--;
        }

        if (!isSameSelection)
        {
            ExecuteSelectionRequest(item);
        }
    }

    private void OnPreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (FindVisualAncestor<ToggleButton>(source) is not null)
        {
            return;
        }

        TreeViewItem? container = FindVisualAncestor<TreeViewItem>(source);
        if (container?.DataContext is not GraphNavigationTreeItem item)
        {
            return;
        }

        SetSelectedItem(item);
        ExecuteOpenRequest(item);
        e.Handled = true;
    }

    private void OnTreeItemExpansionChanged(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem container
            || container.DataContext is not GraphNavigationTreeItem item
            || !item.IsGroup
            || item.GroupId is null)
        {
            return;
        }

        bool isCollapsed = e.RoutedEvent == TreeViewItem.CollapsedEvent;
        bool wasCollapsed = collapsedGroupIds.Contains(item.GroupId);
        if (wasCollapsed == isCollapsed)
        {
            return;
        }

        if (isCollapsed)
        {
            collapsedGroupIds.Add(item.GroupId);
        }
        else
        {
            collapsedGroupIds.Remove(item.GroupId);
        }

        ExecuteGraphRequest(GraphRequest.SetGroupCollapsed(item.GroupId, isCollapsed));
    }

    private void ExecuteSelectionRequest(GraphNavigationTreeItem item)
    {
        if (item.IsGroup && item.GroupId is not null)
        {
            if (!GraphSelectedGroupIds.Contains(item.GroupId, StringComparer.Ordinal))
            {
                ExecuteGraphRequest(GraphRequest.SelectGroup(item.GroupId));
            }

            return;
        }

        if (item.IsMissingTarget && item.LinkId is not null)
        {
            if (!GraphSelectedLinkIds.Contains(item.LinkId, StringComparer.Ordinal))
            {
                ExecuteGraphRequest(GraphRequest.SelectLink(item.LinkId));
            }

            return;
        }

        if (item.NodeId is not null)
        {
            if (!GraphSelectedNodeIds.Contains(item.NodeId, StringComparer.Ordinal))
            {
                ExecuteGraphRequest(GraphRequest.SelectNode(item.NodeId));
            }

            return;
        }

        if (item.LinkId is not null
            && !GraphSelectedLinkIds.Contains(item.LinkId, StringComparer.Ordinal))
        {
            ExecuteGraphRequest(GraphRequest.SelectLink(item.LinkId));
        }
    }

    private void ExecuteOpenRequest(GraphNavigationTreeItem item)
    {
        if (item.IsGroup && item.GroupId is not null)
        {
            ExecuteGraphRequest(GraphRequest.OpenGroup(item.GroupId));
            return;
        }

        if (item.IsMissingTarget && item.LinkId is not null)
        {
            ExecuteGraphRequest(GraphRequest.OpenLink(item.LinkId));
            return;
        }

        if (item.NodeId is not null)
        {
            ExecuteGraphRequest(GraphRequest.OpenNode(item.NodeId));
            return;
        }

        if (item.LinkId is not null)
        {
            ExecuteGraphRequest(GraphRequest.OpenLink(item.LinkId));
        }
    }

    private void ExecuteGraphRequest(GraphRequest request)
    {
        ICommand? command = GraphRequestCommand;
        if (command is null || !command.CanExecute(request))
        {
            return;
        }

        if (isExecutingGraphRequest)
        {
            return;
        }

        isExecutingGraphRequest = true;
        try
        {
            command.Execute(request);
        }
        finally
        {
            isExecutingGraphRequest = false;

            if (isGraphSelectionApplyPending)
            {
                isGraphSelectionApplyPending = false;
                SynchronizeGraphSelectionFromGraphState();
            }
        }
    }

    private GraphNavigationTreeItem? FindItem(string? treeNodeId)
    {
        string? normalizedTreeNodeId = NormalizeOptionalText(treeNodeId);
        return normalizedTreeNodeId is null
            ? null
            : flattenedItems.FirstOrDefault(item =>
                StringComparer.Ordinal.Equals(item.TreeNodeId, normalizedTreeNodeId));
    }

    private void QueueBringIntoViewPass(DispatcherPriority priority)
    {
        if (isBringIntoViewPassScheduled)
        {
            return;
        }

        isBringIntoViewPassScheduled = true;
        Dispatcher.BeginInvoke(new Action(ProcessBringIntoViewPass), priority);
    }

    private void ProcessBringIntoViewPass()
    {
        isBringIntoViewPassScheduled = false;

        GraphNavigationTreeItem? item = pendingBringIntoViewItem;
        if (item is null
            || pendingBringIntoViewRequestVersion != bringIntoViewRequestVersion
            || !StringComparer.Ordinal.Equals(SelectedTreeNodeId, item.TreeNodeId))
        {
            ClearPendingBringIntoView();
            return;
        }

        if (!ExpandRealizableAncestors(item))
        {
            ClearPendingBringIntoView();
            return;
        }

        TreeViewItem? container = FindContainer(item);
        if (container is not null)
        {
            container.BringIntoView();
            ClearPendingBringIntoView();
            return;
        }

        pendingBringIntoViewAttempt++;
        if (pendingBringIntoViewAttempt < BringIntoViewMaxAttempts)
        {
            QueueBringIntoViewPass(DispatcherPriority.ContextIdle);
            return;
        }

        ClearPendingBringIntoView();
    }

    private void ClearPendingBringIntoView()
    {
        pendingBringIntoViewItem = null;
        pendingBringIntoViewAttempt = 0;
    }

    private bool ExpandRealizableAncestors(GraphNavigationTreeItem item)
    {
        Stack<GraphNavigationTreeItem> ancestors = new();
        for (GraphNavigationTreeItem? current = item.Parent; current is not null; current = current.Parent)
        {
            ancestors.Push(current);
        }

        foreach (GraphNavigationTreeItem ancestor in ancestors)
        {
            if (ancestor.IsGroup
                && ancestor.GroupId is not null
                && collapsedGroupIds.Contains(ancestor.GroupId))
            {
                return false;
            }

            ancestor.IsExpanded = true;
        }

        return true;
    }

    private TreeViewItem? FindContainer(GraphNavigationTreeItem item)
    {
        Stack<GraphNavigationTreeItem> path = new();
        for (GraphNavigationTreeItem? current = item; current is not null; current = current.Parent)
        {
            path.Push(current);
        }

        ItemsControl parent = this;
        while (path.Count > 0)
        {
            if (parent.ItemContainerGenerator.Status == GeneratorStatus.GeneratingContainers)
            {
                return null;
            }

            GraphNavigationTreeItem current = path.Pop();
            if (parent.ItemContainerGenerator.ContainerFromItem(current) is not TreeViewItem container)
            {
                return null;
            }

            parent = container;
        }

        return parent as TreeViewItem;
    }

    private static T? FindVisualAncestor<T>(DependencyObject source)
        where T : DependencyObject
    {
        DependencyObject? current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = GetVisualOrLogicalParent(current);
        }

        return null;
    }

    private static DependencyObject? GetVisualOrLogicalParent(DependencyObject current)
    {
        if (current is ContentElement contentElement)
        {
            return ContentOperations.GetParent(contentElement)
                ?? (contentElement as FrameworkContentElement)?.Parent;
        }

        if (current is Visual or Visual3D)
        {
            return VisualTreeHelper.GetParent(current);
        }

        return LogicalTreeHelper.GetParent(current);
    }

    private static bool AreEquivalentProjections(GraphTreeProjection? left, GraphTreeProjection? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Roots.Count != right.Roots.Count)
        {
            return false;
        }

        Stack<(GraphTreeNode Left, GraphTreeNode Right)> pending = new();
        for (int index = left.Roots.Count - 1; index >= 0; index--)
        {
            pending.Push((left.Roots[index], right.Roots[index]));
        }

        while (pending.Count > 0)
        {
            (GraphTreeNode leftNode, GraphTreeNode rightNode) = pending.Pop();
            if (!StringComparer.Ordinal.Equals(leftNode.TreeNodeId, rightNode.TreeNodeId)
                || !StringComparer.Ordinal.Equals(leftNode.NodeId, rightNode.NodeId)
                || !StringComparer.Ordinal.Equals(leftNode.Title, rightNode.Title)
                || leftNode.Kind != rightNode.Kind
                || !StringComparer.Ordinal.Equals(leftNode.LinkId, rightNode.LinkId)
                || leftNode.LinkKind != rightNode.LinkKind
                || leftNode.Children.Count != rightNode.Children.Count)
            {
                return false;
            }

            for (int index = leftNode.Children.Count - 1; index >= 0; index--)
            {
                pending.Push((leftNode.Children[index], rightNode.Children[index]));
            }
        }

        return true;
    }

    private static IReadOnlyList<string> CopySelection(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

}
