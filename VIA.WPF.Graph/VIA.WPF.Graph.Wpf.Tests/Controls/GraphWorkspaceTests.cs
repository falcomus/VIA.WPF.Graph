using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Core.Model;
using VIA.WPF.Graph.Core.Requests;
using VIA.WPF.Graph.Wpf.Controls;
using VIA.WPF.Graph.Wpf.Tests.Support;

namespace VIA.WPF.Graph.Wpf.Tests.Controls;

public sealed class GraphWorkspaceTests
{
    [Fact]
    public void Constructor_CreatesNavigationTreeAndGraphSurface()
    {
        StaTestRunner.Run(() =>
        {
            GraphWorkspace workspace = new();

            Assert.NotNull(workspace.NavigationTree);
            Assert.IsAssignableFrom<TreeView>(workspace.NavigationTree);
            Assert.NotNull(workspace.NavigationTree.ItemTemplate);
            Assert.NotNull(workspace.NavigationTree.ItemContainerStyle);
            Assert.NotNull(workspace.GraphSurface);
            Assert.NotNull(workspace.NavigationTree.GraphRequestCommand);
            Assert.NotNull(workspace.GraphSurface.GraphRequestCommand);
            Assert.Contains(workspace.GraphSurface, EnumerateVisualDescendants(workspace).OfType<SkiaGraphSurface>());
        });
    }

    [Fact]
    public void Document_BuildsTreeVisibleDocumentAndVisibleLayout()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphLayoutEngine layoutEngine = new();
            GraphWorkspace workspace = new()
            {
                LayoutEngine = layoutEngine,
                ViewState = new GraphViewState(GraphViewMode.Focus, activeNodeId: "start"),
                Document = TestGraphLayouts.CreateBasicDocument()
            };

            Assert.NotNull(workspace.TreeProjection);
            Assert.NotNull(workspace.VisibleDocument);
            Assert.NotNull(workspace.VisibleLayout);
            Assert.Same(workspace.VisibleDocument, workspace.GraphSurface.Document);
            Assert.Same(workspace.VisibleLayout, workspace.GraphSurface.LayoutResult);
            Assert.NotEmpty(layoutEngine.Requests);
            Assert.Contains(workspace.VisibleDocument.Nodes, node => node.Id == "start");
            Assert.Contains(workspace.VisibleDocument.Nodes, node => node.Id == "main");
        });
    }

    [Fact]
    public void ShowNavigationTree_CollapsesNavigationTreeWithoutRemovingGraphSurface()
    {
        StaTestRunner.Run(() =>
        {
            GraphWorkspace workspace = new()
            {
                ShowNavigationTree = false
            };

            Assert.Equal(Visibility.Collapsed, workspace.NavigationTree.Visibility);
            Assert.Equal(Visibility.Visible, workspace.GraphSurface.Visibility);
            Assert.Equal(0d, workspace.ColumnDefinitions[0].Width.Value);
        });
    }

    [Fact]
    public void EquivalentTreeProjection_DoesNotReplaceNavigationItemsDuringViewStateChanges()
    {
        StaTestRunner.Run(() =>
        {
            GraphWorkspace workspace = new()
            {
                LayoutEngine = new RecordingGraphLayoutEngine(),
                Document = TestGraphLayouts.CreateBasicDocument(),
                ViewState = GraphViewState.Default
            };
            object? initialItemsSource = workspace.NavigationTree.ItemsSource;
            Assert.NotNull(initialItemsSource);

            for (int index = 0; index < 64; index++)
            {
                string nodeId = index % 2 == 0 ? "main" : "start";
                GraphViewMode viewMode = index % 2 == 0 ? GraphViewMode.Focus : GraphViewMode.Tree;
                workspace.ViewState = new GraphViewState(
                    viewMode,
                    activeNodeId: nodeId,
                    selection: new GraphSelectionState(selectedNodeIds: [nodeId]));
            }

            workspace.ViewState = GraphViewState.Default;

            Assert.Same(initialItemsSource, workspace.NavigationTree.ItemsSource);
        });
    }

    [Fact]
    public void TreeSelection_UpdatesViewStateAndForwardsNeutralRequest()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphRequestCommand command = new();
            GraphWorkspace workspace = new()
            {
                LayoutEngine = new RecordingGraphLayoutEngine(),
                GraphRequestCommand = command,
                Document = TestGraphLayouts.CreateBasicDocument(),
                ViewState = GraphViewState.Default
            };

            bool selected = workspace.NavigationTree.SelectTreeNode("root:start/link:start_main");

            Assert.True(selected);
            Assert.Equal(GraphViewMode.Tree, workspace.ViewState?.ActiveViewMode);
            Assert.Equal("main", workspace.ViewState?.ActiveNodeId);
            Assert.Equal(new[] { "main" }, workspace.ViewState?.Selection.SelectedNodeIds);
            Assert.Equal(new[] { "main" }, workspace.GraphSurface.SelectedNodeIds);
            Assert.Equal("main", workspace.NavigationTree.SelectedNodeId);
            GraphRequest request = Assert.Single(command.Requests);
            Assert.Equal(GraphRequestKind.SelectNode, request.Kind);
            Assert.Equal("main", request.NodeId);
        });
    }
    [Fact]
    public void NativeTreeSelection_DoesNotReenterSelectionRequestPipeline()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphRequestCommand command = new();
            GraphWorkspace workspace = new()
            {
                LayoutEngine = new RecordingGraphLayoutEngine(),
                GraphRequestCommand = command,
                Document = new GraphDocument(
                    "selection-loop",
                    nodes: [new GraphNode("start", "Start"), new GraphNode("main", "Main")],
                    links: [new GraphLink("start_main", "start", "main", kind: GraphLinkKind.Primary)]),
                ViewState = GraphViewState.Default
            };

            workspace.Measure(new Size(1000d, 700d));
            workspace.Arrange(new Rect(0d, 0d, 1000d, 700d));
            workspace.ApplyTemplate();
            workspace.UpdateLayout();

            TreeViewItem rootContainer = Assert.IsType<TreeViewItem>(
                workspace.NavigationTree.ItemContainerGenerator.ContainerFromIndex(0));
            rootContainer.IsExpanded = true;
            rootContainer.ApplyTemplate();
            rootContainer.UpdateLayout();
            workspace.NavigationTree.UpdateLayout();
            workspace.UpdateLayout();

            TreeViewItem childContainer = Assert.IsType<TreeViewItem>(
                rootContainer.ItemContainerGenerator.ContainerFromIndex(0));

            const int selectionCount = 32;
            for (int index = 0; index < selectionCount; index++)
            {
                TreeViewItem target = index % 2 == 0 ? childContainer : rootContainer;
                target.IsSelected = true;
            }

            Assert.Equal(selectionCount, command.Requests.Count);
            Assert.All(command.Requests, request => Assert.Equal(GraphRequestKind.SelectNode, request.Kind));
        });
    }

    [Fact]
    public void ChildRequest_DuringWorkspaceRebuild_IsIgnoredByFullMethodGuard()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphLayoutEngine layoutEngine = new();
            RecordingGraphRequestCommand forwardedCommand = new();
            GraphWorkspace workspace = new()
            {
                LayoutEngine = layoutEngine,
                GraphRequestCommand = forwardedCommand,
                ViewState = GraphViewState.Default,
                Document = TestGraphLayouts.CreateBasicDocument()
            };

            ICommand childCommand = Assert.IsAssignableFrom<ICommand>(workspace.NavigationTree.GraphRequestCommand);
            GraphRequest outerRequest = GraphRequest.SelectNode("main");
            GraphRequest reentrantRequest = GraphRequest.SelectNode("start");

            layoutEngine.BeforeNextLayout = () =>
            {
                Assert.True(childCommand.CanExecute(reentrantRequest));
                childCommand.Execute(reentrantRequest);
            };

            Assert.True(childCommand.CanExecute(outerRequest));
            childCommand.Execute(outerRequest);

            Assert.Equal("main", workspace.ViewState?.ActiveNodeId);
            Assert.Equal(new[] { "main" }, workspace.ViewState?.Selection.SelectedNodeIds);
            Assert.Equal(2, layoutEngine.Requests.Count);
            Assert.Equal(outerRequest, Assert.Single(forwardedCommand.Requests));
        });
    }

    [Fact]
    public void SelectedNavigationGroupUsesLighterFill()
    {
        StaTestRunner.Run(() =>
        {
            GraphWorkspace workspace = new();
            SolidColorBrush normalBrush = Assert.IsType<SolidColorBrush>(
                workspace.FindResource("GraphWorkspace.NavigationGroupBackgroundBrush"));
            SolidColorBrush selectedBrush = Assert.IsType<SolidColorBrush>(
                workspace.FindResource("GraphWorkspace.NavigationGroupSelectedBackgroundBrush"));

            double normalLuminance = CalculateRelativeLuminance(normalBrush.Color);
            double selectedLuminance = CalculateRelativeLuminance(selectedBrush.Color);

            Assert.True(selectedLuminance > normalLuminance);
            Assert.Null(workspace.NavigationTree.FocusVisualStyle);
        });
    }

    [Fact]
    public void NavigationTree_GroupSelectionUpdatesViewStateAndForwardsNeutralRequest()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphRequestCommand command = new();
            GraphWorkspace workspace = new()
            {
                LayoutEngine = new RecordingGraphLayoutEngine(),
                GraphRequestCommand = command,
                Document = CreateGroupCompactStressDocument(),
                ViewState = GraphViewState.Default
            };

            bool selected = workspace.NavigationTree.SelectTreeNode("group:group-a");

            Assert.True(selected);
            Assert.Equal(GraphViewMode.Group, workspace.ViewState?.ActiveViewMode);
            Assert.Equal("group-a", workspace.ViewState?.ActiveGroupId);
            Assert.Equal(new[] { "group-a" }, workspace.ViewState?.Selection.SelectedGroupIds);
            Assert.Equal(new[] { "group-a" }, workspace.GraphSurface.SelectedGroupIds);
            GraphRequest request = Assert.Single(command.Requests);
            Assert.Equal(GraphRequestKind.SelectGroup, request.Kind);
            Assert.Equal("group-a", request.GroupId);
        });
    }

    [Fact]
    public void CollapsedContainerGroupStateCollapsesNativeGroupContainer()
    {
        StaTestRunner.Run(() =>
        {
            GraphWorkspace workspace = new()
            {
                LayoutEngine = new RecordingGraphLayoutEngine(),
                ViewState = new GraphViewState(
                    collapsedContainerGroupIds: ["group-a"]),
                Document = CreateGroupCompactStressDocument()
            };

            workspace.Measure(new Size(1000d, 700d));
            workspace.Arrange(new Rect(0d, 0d, 1000d, 700d));
            workspace.UpdateLayout();

            TreeViewItem groupContainer = Assert.IsType<TreeViewItem>(
                workspace.NavigationTree.ItemContainerGenerator.ContainerFromIndex(0));
            Assert.False(groupContainer.IsExpanded);
        });
    }

    [Fact]
    public void GraphSelection_UpdatesFocusModeAndTreeSelection()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphRequestCommand command = new();
            GraphWorkspace workspace = new()
            {
                LayoutEngine = new RecordingGraphLayoutEngine(),
                GraphRequestCommand = command,
                Document = TestGraphLayouts.CreateBasicDocument(),
                ViewState = GraphViewState.Default
            };

            GraphRequest request = GraphRequest.SelectNode("main");
            ICommand graphCommand = Assert.IsAssignableFrom<ICommand>(workspace.GraphSurface.GraphRequestCommand);
            Assert.True(graphCommand.CanExecute(request));
            graphCommand.Execute(request);

            Assert.Equal(GraphViewMode.Focus, workspace.ViewState?.ActiveViewMode);
            Assert.Equal("main", workspace.ViewState?.ActiveNodeId);
            Assert.Equal(new[] { "main" }, workspace.ViewState?.Selection.SelectedNodeIds);
            Assert.Equal("main", workspace.NavigationTree.SelectedNodeId);
            GraphRequest forwardedRequest = Assert.Single(command.Requests);
            Assert.Equal(GraphRequestKind.SelectNode, forwardedRequest.Kind);
            Assert.Equal("main", forwardedRequest.NodeId);
        });
    }

    [Fact]
    public void GroupCompact_KeepsSelectedNodeAndLimitsVisibleDocument()
    {
        StaTestRunner.Run(() =>
        {
            GraphWorkspace workspace = new()
            {
                LayoutEngine = new RecordingGraphLayoutEngine(),
                ViewState = new GraphViewState(
                    GraphViewMode.Group,
                    activeNodeId: "node_25",
                    activeGroupId: "group-a",
                    selection: new GraphSelectionState(
                        selectedNodeIds: ["node_25"],
                        selectedGroupIds: ["group-a"])),
                Document = CreateGroupCompactStressDocument()
            };

            Assert.NotNull(workspace.VisibleDocument);
            Assert.True(workspace.VisibleDocument.Nodes.Count <= 20);
            Assert.Contains(workspace.VisibleDocument.Nodes, node => node.Id == "node_25");
            Assert.Contains(workspace.VisibleDocument.Nodes, node => node.Id == "node_01");
        });
    }

    [Fact]
    public void NavigationTree_BringSelectedTreeNodeIntoView_ReturnsTrueForSelectedNode()
    {
        StaTestRunner.Run(() =>
        {
            GraphWorkspace workspace = new()
            {
                LayoutEngine = new RecordingGraphLayoutEngine(),
                Document = TestGraphLayouts.CreateBasicDocument(),
                ViewState = new GraphViewState(GraphViewMode.Focus, activeNodeId: "main", selection: new GraphSelectionState(selectedNodeIds: ["main"]))
            };

            Assert.True(workspace.NavigationTree.BringSelectedTreeNodeIntoView());
        });
    }




    private static IEnumerable<DependencyObject> EnumerateVisualDescendants(DependencyObject parent)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;

            foreach (DependencyObject descendant in EnumerateVisualDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static double CalculateRelativeLuminance(Color color)
    {
        return (0.2126d * color.R) + (0.7152d * color.G) + (0.0722d * color.B);
    }

    private static GraphDocument CreateGroupCompactStressDocument()
    {
        GraphGroup[] groups = [new("group-a", "Group A", GraphGroupKind.Container)];
        GraphNode[] nodes = Enumerable.Range(1, 25)
            .Select(index => new GraphNode($"node_{index:00}", $"Node {index:00}", groupMemberships: ["group-a"]))
            .ToArray();
        GraphLink[] links = Enumerable.Range(1, 24)
            .Select(index => new GraphLink($"node_{index:00}_node_{index + 1:00}", $"node_{index:00}", $"node_{index + 1:00}", kind: GraphLinkKind.Primary))
            .ToArray();

        return new GraphDocument("group-compact-stress", nodes, links, groups: groups);
    }

    private sealed class RecordingGraphLayoutEngine : IGraphLayoutEngine
    {
        private readonly List<(GraphDocument Document, GraphLayoutOptions Options)> requests = [];

        public IReadOnlyList<(GraphDocument Document, GraphLayoutOptions Options)> Requests => requests;

        public Action? BeforeNextLayout { get; set; }

        public GraphLayoutResult Layout(GraphDocument document, GraphLayoutOptions options)
        {
            Action? beforeNextLayout = BeforeNextLayout;
            BeforeNextLayout = null;
            beforeNextLayout?.Invoke();

            requests.Add((document, options));

            GraphLayoutNode[] nodes = document.Nodes
                .Select((node, index) => new GraphLayoutNode(node.Id, new GraphRect(40d + (index * 180d), 40d, node.DefaultSize.Width, node.DefaultSize.Height)))
                .ToArray();
            GraphLayoutEdge[] edges = document.Links
                .Select(link => new GraphLayoutEdge(link.Id, [new GraphPoint(40d, 70d), new GraphPoint(220d, 70d)]))
                .ToArray();
            GraphLayoutGroup[] groups = document.Groups
                .Where(group => group.Kind == GraphGroupKind.Container)
                .Select((group, index) => new GraphLayoutGroup(group.Id, new GraphRect(20d + (index * 180d), 20d, 160d, 130d)))
                .ToArray();

            return new GraphLayoutResult(
                document.Id,
                options,
                new GraphRect(0d, 0d, Math.Max(240d, document.Nodes.Count * 180d), 180d),
                nodes,
                groups,
                edges);
        }
    }
}

