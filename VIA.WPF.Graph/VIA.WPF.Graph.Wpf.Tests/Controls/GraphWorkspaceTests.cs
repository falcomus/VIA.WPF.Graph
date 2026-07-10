using System.Windows;
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
            Assert.NotNull(workspace.GraphSurface);
            Assert.NotNull(workspace.NavigationTree.GraphRequestCommand);
            Assert.NotNull(workspace.GraphSurface.GraphRequestCommand);
            Assert.Contains(workspace.GraphSurface, workspace.Children.OfType<UIElement>());
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
            Assert.Equal(GraphViewMode.Focus, workspace.ViewState?.ActiveViewMode);
            Assert.Equal("main", workspace.ViewState?.ActiveNodeId);
            Assert.Equal(new[] { "main" }, workspace.ViewState?.Selection.SelectedNodeIds);
            Assert.Equal(new[] { "main" }, workspace.GraphSurface.SelectedNodeIds);
            GraphRequest request = Assert.Single(command.Requests);
            Assert.Equal(GraphRequestKind.SelectNode, request.Kind);
            Assert.Equal("main", request.NodeId);
        });
    }


    private sealed class RecordingGraphLayoutEngine : IGraphLayoutEngine
    {
        private readonly List<(GraphDocument Document, GraphLayoutOptions Options)> requests = [];

        public IReadOnlyList<(GraphDocument Document, GraphLayoutOptions Options)> Requests => requests;

        public GraphLayoutResult Layout(GraphDocument document, GraphLayoutOptions options)
        {
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
