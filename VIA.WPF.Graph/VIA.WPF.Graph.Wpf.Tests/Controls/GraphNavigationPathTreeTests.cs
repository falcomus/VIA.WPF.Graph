using System.Windows;
using VIA.WPF.Graph.Core.Projections;
using VIA.WPF.Graph.Core.Requests;
using VIA.WPF.Graph.Wpf.Controls;
using VIA.WPF.Graph.Wpf.Tests.Support;

namespace VIA.WPF.Graph.Wpf.Tests.Controls;

public sealed class GraphNavigationPathTreeTests
{
    [Fact]
    public void Projection_CreatesMeasurableMiniCardTree()
    {
        StaTestRunner.Run(() =>
        {
            GraphNavigationPathTree tree = new()
            {
                Projection = CreateProjection()
            };

            tree.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            Assert.True(tree.DesiredSize.Width > 220d);
            Assert.True(tree.DesiredSize.Height > 160d);
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
