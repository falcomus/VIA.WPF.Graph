using System.Windows;
using VIA.WPF.Graph.Core.Layout;
using VIA.WPF.Graph.Core.Requests;
using VIA.WPF.Graph.Wpf.Controls;
using VIA.WPF.Graph.Wpf.Tests.Support;

namespace VIA.WPF.Graph.Wpf.Tests.Controls;

public sealed class GraphCanvasTests
{
    [Fact]
    public void LayoutResult_UpdatesLayoutBounds()
    {
        StaTestRunner.Run(() =>
        {
            GraphCanvas canvas = new()
            {
                LayoutResult = TestGraphLayouts.CreateBasicLayout()
            };

            Assert.Equal(new GraphRect(0d, 0d, 400d, 240d), canvas.LayoutBounds);
        });
    }

    [Fact]
    public void FitToGraph_UsesLayoutBoundsAndViewport()
    {
        StaTestRunner.Run(() =>
        {
            GraphCanvas canvas = new()
            {
                LayoutResult = TestGraphLayouts.CreateBasicLayout()
            };

            canvas.FitToGraph(new Size(800d, 600d), padding: 40d);

            AssertClose(1.8d, canvas.Zoom);
            AssertClose(40d, canvas.PanX);
            AssertClose(84d, canvas.PanY);
        });
    }

    [Fact]
    public void FocusNode_SetsFocusedNodeAndCentersViewport()
    {
        StaTestRunner.Run(() =>
        {
            GraphCanvas canvas = CreateArrangedCanvas();

            bool focused = canvas.FocusNode("main");

            Assert.True(focused);
            Assert.Equal("main", canvas.FocusedNodeId);
            Assert.Null(canvas.FocusedLinkId);
            Assert.Null(canvas.FocusedGroupId);
            AssertClose(110d, canvas.PanX);
            AssertClose(145d, canvas.PanY);
        });
    }

    [Fact]
    public void FocusGroup_SetsFocusedGroupAndFitsGroupBounds()
    {
        StaTestRunner.Run(() =>
        {
            GraphCanvas canvas = CreateArrangedCanvas();

            bool focused = canvas.FocusGroup("work");

            Assert.True(focused);
            Assert.Null(canvas.FocusedNodeId);
            Assert.Null(canvas.FocusedLinkId);
            Assert.Equal("work", canvas.FocusedGroupId);
            AssertClose(3.8736842105263158d, canvas.Zoom);
            AssertClose(-704d, canvas.PanX);
            AssertClose(-300.42105263157896d, canvas.PanY);
        });
    }

    [Fact]
    public void FocusedNodeId_SetFromBinding_CentersViewport()
    {
        StaTestRunner.Run(() =>
        {
            GraphCanvas canvas = CreateArrangedCanvas();

            canvas.FocusedNodeId = "main";

            Assert.Equal("main", canvas.FocusedNodeId);
            AssertClose(110d, canvas.PanX);
            AssertClose(145d, canvas.PanY);
        });
    }

    [Fact]
    public void FocusedGroupId_SetFromBinding_FitsGroupBounds()
    {
        StaTestRunner.Run(() =>
        {
            GraphCanvas canvas = CreateArrangedCanvas();

            canvas.FocusedGroupId = "work";

            Assert.Equal("work", canvas.FocusedGroupId);
            AssertClose(3.8736842105263158d, canvas.Zoom);
            AssertClose(-704d, canvas.PanX);
            AssertClose(-300.42105263157896d, canvas.PanY);
        });
    }

    [Fact]
    public void FocusFirstMatch_FocusesNodeGroupAndLinkById()
    {
        StaTestRunner.Run(() =>
        {
            GraphCanvas canvas = CreateArrangedCanvas();

            Assert.True(canvas.FocusFirstMatch("main"));
            Assert.Equal("main", canvas.FocusedNodeId);

            Assert.True(canvas.FocusFirstMatch("entry"));
            Assert.Equal("entry", canvas.FocusedGroupId);

            Assert.True(canvas.FocusFirstMatch("start_main"));
            Assert.Equal("start_main", canvas.FocusedLinkId);
        });
    }

    [Fact]
    public void ReturnToOverview_ClearsFocusAndSearch()
    {
        StaTestRunner.Run(() =>
        {
            GraphCanvas canvas = CreateArrangedCanvas();
            Assert.True(canvas.FocusFirstMatch("main"));

            canvas.ReturnToOverview();

            Assert.Null(canvas.FocusedNodeId);
            Assert.Null(canvas.FocusedLinkId);
            Assert.Null(canvas.FocusedGroupId);
            Assert.Equal(string.Empty, canvas.SearchText);
        });
    }

    [Fact]
    public void SelectionProperties_NormalizeDistinctIds()
    {
        StaTestRunner.Run(() =>
        {
            GraphCanvas canvas = new()
            {
                SelectedNodeIds = ["node-a", "node-a", " ", "node-b"],
                SelectedLinkIds = ["link-a", "link-a"],
                SelectedGroupIds = ["group-a", "", "group-b"]
            };

            Assert.Equal(new[] { "node-a", "node-b" }, canvas.SelectedNodeIds);
            Assert.Equal(new[] { "link-a" }, canvas.SelectedLinkIds);
            Assert.Equal(new[] { "group-a", "group-b" }, canvas.SelectedGroupIds);
        });
    }

    [Fact]
    public void CollapsedGroupIds_NormalizeDistinctIds()
    {
        StaTestRunner.Run(() =>
        {
            GraphCanvas canvas = new()
            {
                CollapsedGroupIds = ["work", "work", " ", "entry"]
            };

            Assert.Equal(new[] { "work", "entry" }, canvas.CollapsedGroupIds);
        });
    }

    [Fact]
    public void SetGroupCollapsed_UpdatesStateAndSendsRequest()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphRequestCommand command = new();
            GraphCanvas canvas = CreateArrangedCanvas();
            canvas.GraphRequestCommand = command;

            bool collapsed = canvas.SetGroupCollapsed("work", isCollapsed: true);

            Assert.True(collapsed);
            Assert.Equal(new[] { "work" }, canvas.CollapsedGroupIds);
            GraphRequest request = Assert.Single(command.Requests);
            Assert.Equal(GraphRequestKind.SetGroupCollapsed, request.Kind);
            Assert.Equal("work", request.GroupId);
            Assert.True(request.IsGroupCollapsed);
        });
    }

    [Fact]
    public void ToggleGroupCollapsed_ExpandsExistingGroupAndSendsRequest()
    {
        StaTestRunner.Run(() =>
        {
            RecordingGraphRequestCommand command = new();
            GraphCanvas canvas = CreateArrangedCanvas();
            canvas.CollapsedGroupIds = ["work"];
            canvas.GraphRequestCommand = command;

            bool toggled = canvas.ToggleGroupCollapsed("work");

            Assert.True(toggled);
            Assert.Empty(canvas.CollapsedGroupIds);
            GraphRequest request = Assert.Single(command.Requests);
            Assert.Equal(GraphRequestKind.SetGroupCollapsed, request.Kind);
            Assert.Equal("work", request.GroupId);
            Assert.False(request.IsGroupCollapsed);
        });
    }

    [Fact]
    public void Zoom_IsCoercedToConfiguredLimits()
    {
        StaTestRunner.Run(() =>
        {
            GraphCanvas canvas = new()
            {
                MinZoom = 0.5d,
                MaxZoom = 2d,
                Zoom = 10d
            };

            AssertClose(2d, canvas.Zoom);

            canvas.Zoom = 0.1d;

            AssertClose(0.5d, canvas.Zoom);
        });
    }

    private static GraphCanvas CreateArrangedCanvas()
    {
        GraphCanvas canvas = new()
        {
            LayoutResult = TestGraphLayouts.CreateBasicLayout()
        };
        canvas.Measure(new Size(800d, 600d));
        canvas.Arrange(new Rect(0d, 0d, 800d, 600d));
        return canvas;
    }

    private static void AssertClose(double expected, double actual)
    {
        Assert.InRange(actual, expected - 0.000001d, expected + 0.000001d);
    }
}
