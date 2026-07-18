using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Core.Projections;
using VIA.WPF.Graph.Core.Requests;
using VIA.WPF.Graph.Wpf.Controls;
using VIA.WPF.Graph.Wpf.Tests.Support;

namespace VIA.WPF.Graph.Wpf.Tests.Controls;

public sealed class GraphNavigationPathTreeTests
{
    [Fact]
    public void Document_CreatesContainerGroupRootsWithNativeTreeViewItems()
    {
        StaTestRunner.Run(() =>
        {
            GraphDocument document = CreateGroupedDocument();
            GraphNavigationPathTree tree = new()
            {
                Document = document,
                Projection = GraphTreeProjectionBuilder.Build(document)
            };

            Assert.IsAssignableFrom<TreeView>(tree);
            Assert.Equal(2, tree.Items.Count);

            tree.Measure(new Size(420d, 640d));
            tree.Arrange(new Rect(0d, 0d, 420d, 640d));
            tree.ApplyTemplate();
            tree.UpdateLayout();

            Assert.IsType<TreeViewItem>(tree.ItemContainerGenerator.ContainerFromIndex(0));
        });
    }

    [Fact]
    public void GroupedDocument_ShowsEachDocumentNodeOnceWhenProjectionContainsReferences()
    {
        StaTestRunner.Run(() =>
        {
            GraphDocument document = CreateGroupedDocumentWithReferences();
            GraphNavigationPathTree tree = new()
            {
                Document = document,
                Projection = GraphTreeProjectionBuilder.Build(document)
            };

            IReadOnlyList<object> navigationItems = FlattenNavigationItems(tree);
            string[] visibleNodeIds = navigationItems
                .Select(GetNavigationItemNodeId)
                .Where(nodeId => nodeId is not null)
                .Select(nodeId => nodeId!)
                .ToArray();

            Assert.Equal(document.Nodes.Count, visibleNodeIds.Length);
            Assert.Equal(document.Nodes.Count, visibleNodeIds.Distinct(StringComparer.Ordinal).Count());
            Assert.All(document.Nodes, node =>
                Assert.Equal(1, visibleNodeIds.Count(nodeId => StringComparer.Ordinal.Equals(nodeId, node.Id))));
            Assert.False(navigationItems.Any(IsNavigationReference));

            object[] groupItems = tree.Items.Cast<object>().ToArray();
            Assert.Equal(2, groupItems.Length);
            Assert.All(groupItems, groupItem => Assert.Equal("2 items", GetNavigationItemSubtitle(groupItem)));
        });
    }

    [Fact]
    public void GroupedDocument_KeepsMissingTargetAndCountsVisibleCards()
    {
        StaTestRunner.Run(() =>
        {
            GraphDocument document = new(
                "grouped-tree-with-missing-target",
                nodes: [new GraphNode("start", "Start", groupMemberships: ["entry"])],
                links: [new GraphLink("start_missing", "start", "missing", kind: GraphLinkKind.Secondary)],
                groups: [new GraphGroup("entry", "Entry", GraphGroupKind.Container)]);
            GraphNavigationPathTree tree = new()
            {
                Document = document,
                Projection = GraphTreeProjectionBuilder.Build(document)
            };

            object groupItem = Assert.Single(tree.Items.Cast<object>());
            IReadOnlyList<object> groupChildren = GetNavigationItemChildren(groupItem);

            Assert.Equal(2, groupChildren.Count);
            Assert.Equal("2 items", GetNavigationItemSubtitle(groupItem));
            Assert.True(groupChildren.Any(IsNavigationMissingTarget));
        });
    }

    [Fact]
    public void CyclicContainerGroups_AreBuiltWithoutRecursiveFailure()
    {
        StaTestRunner.Run(() =>
        {
            GraphDocument document = new(
                "cyclic-groups",
                nodes: [new GraphNode("node", "Node", groupMemberships: ["group-a"])],
                groups:
                [
                    new GraphGroup("group-a", "Group A", GraphGroupKind.Container, parentGroupId: "group-b"),
                    new GraphGroup("group-b", "Group B", GraphGroupKind.Container, parentGroupId: "group-a")
                ]);
            GraphNavigationPathTree tree = new()
            {
                Document = document,
                Projection = GraphTreeProjectionBuilder.Build(document)
            };

            Assert.Single(tree.Items);
            Assert.True(tree.SelectTreeNode("group:group-a"));
        });
    }

    [Fact]
    public void DeepProjection_BuildsAndSelectsWithoutRecursiveStackTraversal()
    {
        StaTestRunner.Run(() =>
        {
            const int depth = 4096;
            GraphTreeNode current = new(
                $"tree:{depth - 1}",
                $"node-{depth - 1}",
                $"Node {depth - 1}",
                GraphTreeNodeKind.Branch);

            for (int index = depth - 2; index >= 0; index--)
            {
                current = new GraphTreeNode(
                    $"tree:{index}",
                    $"node-{index}",
                    $"Node {index}",
                    index == 0 ? GraphTreeNodeKind.Root : GraphTreeNodeKind.Branch,
                    children: [current]);
            }

            GraphNavigationPathTree tree = new()
            {
                Projection = new GraphTreeProjection([current])
            };

            Assert.Single(tree.Items);
            Assert.True(tree.SelectTreeNode($"tree:{depth - 1}"));
            Assert.Equal($"node-{depth - 1}", tree.SelectedNodeId);
        });
    }

    [Fact]
    public void Projection_RebuildsByReplacingTheCompleteItemsSourceSnapshot()
    {
        StaTestRunner.Run(() =>
        {
            GraphNavigationPathTree tree = new()
            {
                Projection = CreateSingleNodeProjection("First")
            };
            object? firstItemsSource = tree.ItemsSource;

            tree.Projection = CreateSingleNodeProjection("Second");
            object? secondItemsSource = tree.ItemsSource;

            Assert.NotNull(firstItemsSource);
            Assert.NotNull(secondItemsSource);
            Assert.NotSame(firstItemsSource, secondItemsSource);
            Assert.Single(tree.Items);
        });
    }

    [Fact]
    public void SelectTreeNode_SelectsGroupAndSendsGroupRequest()
    {
        StaTestRunner.Run(() =>
        {
            GraphDocument document = CreateGroupedDocument();
            RecordingGraphRequestCommand command = new();
            GraphNavigationPathTree tree = new()
            {
                Document = document,
                Projection = GraphTreeProjectionBuilder.Build(document),
                GraphRequestCommand = command
            };

            bool selected = tree.SelectTreeNode("group:entry");

            Assert.True(selected);
            Assert.Equal("group:entry", tree.SelectedTreeNodeId);
            Assert.Null(tree.SelectedNodeId);
            Assert.Null(tree.SelectedLinkId);
            GraphRequest request = Assert.Single(command.Requests);
            Assert.Equal(GraphRequestKind.SelectGroup, request.Kind);
            Assert.Equal("entry", request.GroupId);
        });
    }

    [Fact]
    public void GraphSelectedGroupIds_SelectsMatchingGroupWithoutRequest()
    {
        StaTestRunner.Run(() =>
        {
            GraphDocument document = CreateGroupedDocument();
            RecordingGraphRequestCommand command = new();
            GraphNavigationPathTree tree = new()
            {
                Document = document,
                Projection = GraphTreeProjectionBuilder.Build(document),
                GraphRequestCommand = command
            };

            tree.GraphSelectedNodeIds = ["catalog"];
            tree.GraphSelectedGroupIds = ["shop"];

            Assert.Equal("group:shop", tree.SelectedTreeNodeId);
            Assert.Null(tree.SelectedNodeId);
            Assert.Null(tree.SelectedLinkId);
            Assert.Empty(command.Requests);
        });
    }

    [Fact]
    public void SelectTreeNode_SelectsReferenceNodeAndSendsNodeRequest()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphRequestCommand command = new();
            GraphNavigationPathTree tree = new()
            {
                Projection = CreateProjection(),
                GraphRequestCommand = command
            };

            bool selected = tree.SelectTreeNode("root:start/link:main_finish/link:finish_start_back");

            Assert.True(selected);
            Assert.Equal("finish_start_back", tree.SelectedLinkId);
            Assert.Equal("start", tree.SelectedNodeId);
            Assert.Equal(GraphRequestKind.SelectNode, Assert.Single(command.Requests).Kind);
            Assert.Equal("start", command.Requests[0].NodeId);
        });
    }

    [Fact]
    public void GraphSelectedNodeIds_SelectsMatchingTreePathWithoutRequest()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphRequestCommand command = new();
            GraphNavigationPathTree tree = new()
            {
                Projection = CreateProjection(),
                GraphRequestCommand = command
            };

            tree.GraphSelectedNodeIds = ["finish"];

            Assert.Equal("root:start/link:main_finish", tree.SelectedTreeNodeId);
            Assert.Equal("finish", tree.SelectedNodeId);
            Assert.Equal("main_finish", tree.SelectedLinkId);
            Assert.Empty(command.Requests);
        });
    }

    [Fact]
    public void GraphSelectedLinkIds_SelectsMatchingTreePathWithLinkPrecedence()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphRequestCommand command = new();
            GraphNavigationPathTree tree = new()
            {
                Projection = CreateProjection(),
                GraphRequestCommand = command,
                GraphSelectedNodeIds = ["finish"],
                GraphSelectedLinkIds = ["finish_start_back"]
            };

            Assert.Equal("root:start/link:main_finish/link:finish_start_back", tree.SelectedTreeNodeId);
            Assert.Equal("start", tree.SelectedNodeId);
            Assert.Equal("finish_start_back", tree.SelectedLinkId);
            Assert.Empty(command.Requests);
        });
    }

    [Fact]
    public void ClearingGraphSelection_ClearsTreeSelectionWithoutRequest()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphRequestCommand command = new();
            GraphNavigationPathTree tree = new()
            {
                Projection = CreateProjection(),
                GraphRequestCommand = command,
                GraphSelectedNodeIds = ["finish"]
            };

            tree.GraphSelectedNodeIds = [];

            Assert.Null(tree.SelectedTreeNodeId);
            Assert.Null(tree.SelectedNodeId);
            Assert.Null(tree.SelectedLinkId);
            Assert.Empty(command.Requests);
        });
    }

    [Fact]
    public void SelectTreeNode_DoesNotSendDuplicateNodeRequestWhenNodeIsAlreadySelected()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphRequestCommand command = new();
            GraphNavigationPathTree tree = new()
            {
                Projection = CreateProjection(),
                GraphRequestCommand = command,
                GraphSelectedNodeIds = ["finish"]
            };

            bool selected = tree.SelectTreeNode("root:start/link:main_finish");

            Assert.True(selected);
            Assert.Empty(command.Requests);
        });
    }

    [Fact]
    public void SelectTreeNode_DoesNotSendDuplicateGroupRequestWhenGroupIsAlreadySelected()
    {
        StaTestRunner.Run(() =>
        {
            GraphDocument document = CreateGroupedDocument();
            RecordingGraphRequestCommand command = new();
            GraphNavigationPathTree tree = new()
            {
                Document = document,
                Projection = GraphTreeProjectionBuilder.Build(document),
                GraphRequestCommand = command,
                GraphSelectedGroupIds = ["shop"]
            };

            bool selected = tree.SelectTreeNode("group:shop");

            Assert.True(selected);
            Assert.Empty(command.Requests);
        });
    }

    [Fact]
    public void SelectTreeNode_DoesNotSendDuplicateLinkRequestWhenLinkIsAlreadySelected()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphRequestCommand command = new();
            GraphNavigationPathTree tree = new()
            {
                Projection = CreateProjection(),
                GraphRequestCommand = command,
                GraphSelectedLinkIds = ["start_missing"]
            };

            bool selected = tree.SelectTreeNode("root:start/link:start_missing");

            Assert.True(selected);
            Assert.Empty(command.Requests);
        });
    }

    [Fact]
    public void SynchronousGraphSelectionFeedback_SendsExactlyOneSelectionRequest()
    {
        StaTestRunner.Run(() =>
        {
            GraphNavigationPathTree tree = new()
            {
                Projection = CreateProjection()
            };
            List<GraphRequest> requests = [];
            tree.GraphRequestCommand = new CallbackCommand(parameter =>
            {
                GraphRequest request = Assert.IsType<GraphRequest>(parameter);
                requests.Add(request);
                tree.GraphSelectedNodeIds = request.NodeId is null ? [] : [request.NodeId];
            });

            bool selected = tree.SelectTreeNode("root:start/link:main_finish");

            Assert.True(selected);
            GraphRequest request = Assert.Single(requests);
            Assert.Equal(GraphRequestKind.SelectNode, request.Kind);
            Assert.Equal("finish", request.NodeId);
        });
    }

    [Fact]
    public void OpenTreeNode_ForMissingTargetSendsOpenLinkRequest()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphRequestCommand command = new();
            GraphNavigationPathTree tree = new()
            {
                Projection = CreateProjection(),
                GraphRequestCommand = command
            };

            bool opened = tree.OpenTreeNode("root:start/link:start_missing");

            Assert.True(opened);
            GraphRequest request = Assert.Single(command.Requests);
            Assert.Equal(GraphRequestKind.OpenLink, request.Kind);
            Assert.Equal("start_missing", request.LinkId);
        });
    }

    private sealed class CallbackCommand : ICommand
    {
        private readonly Action<object?> execute;

        public CallbackCommand(Action<object?> execute)
        {
            this.execute = execute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter)
        {
            return true;
        }

        public void Execute(object? parameter)
        {
            execute(parameter);
        }
    }

    private static IReadOnlyList<object> FlattenNavigationItems(GraphNavigationPathTree tree)
    {
        object[] roots = tree.Items.Cast<object>().ToArray();
        Stack<object> pendingItems = new();
        for (int index = roots.Length - 1; index >= 0; index--)
        {
            pendingItems.Push(roots[index]);
        }

        List<object> result = [];
        while (pendingItems.Count > 0)
        {
            object item = pendingItems.Pop();
            result.Add(item);

            IReadOnlyList<object> children = GetNavigationItemChildren(item);
            object[] childItems = children.ToArray();
            for (int index = childItems.Length - 1; index >= 0; index--)
            {
                pendingItems.Push(childItems[index]);
            }
        }

        return result;
    }

    private static IReadOnlyList<object> GetNavigationItemChildren(object item)
    {
        object? childrenValue = item.GetType().GetProperty("Children")?.GetValue(item);
        return childrenValue is IEnumerable children
            ? children.Cast<object>().ToArray()
            : Array.Empty<object>();
    }

    private static string? GetNavigationItemNodeId(object item)
    {
        return item.GetType().GetProperty("NodeId")?.GetValue(item) as string;
    }

    private static string? GetNavigationItemSubtitle(object item)
    {
        return item.GetType().GetProperty("Subtitle")?.GetValue(item) as string;
    }

    private static bool IsNavigationReference(object item)
    {
        return item.GetType().GetProperty("IsReference")?.GetValue(item) is true;
    }

    private static bool IsNavigationMissingTarget(object item)
    {
        return item.GetType().GetProperty("IsMissingTarget")?.GetValue(item) is true;
    }

    private static GraphDocument CreateGroupedDocumentWithReferences()
    {
        return new GraphDocument(
            "grouped-tree-with-references",
            nodes:
            [
                new GraphNode("start", "Start", groupMemberships: ["entry"]),
                new GraphNode("login", "Login", groupMemberships: ["entry"]),
                new GraphNode("home", "Home", groupMemberships: ["home-area"]),
                new GraphNode("wishlist", "Wishlist", groupMemberships: ["home-area"])
            ],
            links:
            [
                new GraphLink("start_login", "start", "login", kind: GraphLinkKind.Primary),
                new GraphLink("login_home", "login", "home", kind: GraphLinkKind.Primary),
                new GraphLink("home_wishlist", "home", "wishlist", kind: GraphLinkKind.Primary),
                new GraphLink("wishlist_home_back", "wishlist", "home", kind: GraphLinkKind.Back),
                new GraphLink("wishlist_login_reference", "wishlist", "login", kind: GraphLinkKind.Secondary)
            ],
            groups:
            [
                new GraphGroup("entry", "Entry", GraphGroupKind.Container),
                new GraphGroup("home-area", "Home", GraphGroupKind.Container)
            ]);
    }

    private static GraphDocument CreateGroupedDocument()
    {
        return new GraphDocument(
            "grouped-tree",
            nodes:
            [
                new GraphNode("start", "Start", groupMemberships: ["entry"]),
                new GraphNode("catalog", "Catalog", groupMemberships: ["shop"]),
                new GraphNode("details", "Product details", groupMemberships: ["shop"])
            ],
            links:
            [
                new GraphLink("start_catalog", "start", "catalog", kind: GraphLinkKind.Primary),
                new GraphLink("catalog_details", "catalog", "details", kind: GraphLinkKind.Primary)
            ],
            groups:
            [
                new GraphGroup("entry", "Entry", GraphGroupKind.Container),
                new GraphGroup("shop", "Shop", GraphGroupKind.Container)
            ]);
    }

    private static GraphTreeProjection CreateSingleNodeProjection(string title)
    {
        GraphTreeNode root = new(
            "root:single",
            "single",
            title,
            GraphTreeNodeKind.Root);

        return new GraphTreeProjection([root]);
    }

    private static GraphTreeProjection CreateProjection()
    {
        GraphTreeNode referenceToStart = new(
            "root:start/link:main_finish/link:finish_start_back",
            "start",
            "Start",
            GraphTreeNodeKind.Reference,
            "finish_start_back");

        GraphTreeNode finish = new(
            "root:start/link:main_finish",
            "finish",
            "Finish",
            GraphTreeNodeKind.Branch,
            "main_finish",
            children: [referenceToStart]);

        GraphTreeNode missing = new(
            "root:start/link:start_missing",
            "missing",
            "missing",
            GraphTreeNodeKind.MissingTarget,
            "start_missing");

        GraphTreeNode root = new(
            "root:start",
            "start",
            "Start",
            GraphTreeNodeKind.Root,
            children: [finish, missing]);

        return new GraphTreeProjection([root]);
    }
}
