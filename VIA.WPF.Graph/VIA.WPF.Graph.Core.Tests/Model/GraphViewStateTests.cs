using VIA.WPF.Graph.Core.Model;

namespace VIA.WPF.Graph.Core.Tests.Model;

public sealed class GraphViewStateTests
{
    [Fact]
    public void GraphViewState_NormalizesOptionalIdsAndCopiesDistinctLists()
    {
        GraphViewState state = new(
            activeViewMode: GraphViewMode.Focus,
            activeNodeId: " ",
            activeGroupId: "group-a",
            selection: new GraphSelectionState(
                selectedNodeIds: ["node-a", "node-a", ""],
                selectedLinkIds: ["link-a"],
                selectedGroupIds: ["group-a", "group-a"]),
            viewport: new GraphViewportState(2d, 10d, -5d),
            collapsedContainerGroupIds: ["group-a", "group-a", ""],
            expandedTreeItemIds: ["tree-a"]);

        Assert.Equal(GraphViewMode.Focus, state.ActiveViewMode);
        Assert.Null(state.ActiveNodeId);
        Assert.Equal("group-a", state.ActiveGroupId);
        Assert.Equal(new[] { "node-a" }, state.Selection.SelectedNodeIds);
        Assert.Equal(new[] { "link-a" }, state.Selection.SelectedLinkIds);
        Assert.Equal(new[] { "group-a" }, state.Selection.SelectedGroupIds);
        Assert.Equal(2d, state.Viewport.Zoom);
        Assert.Equal(10d, state.Viewport.PanX);
        Assert.Equal(-5d, state.Viewport.PanY);
        Assert.Equal(new[] { "group-a" }, state.CollapsedContainerGroupIds);
        Assert.Equal(new[] { "tree-a" }, state.ExpandedTreeItemIds);
    }

    [Fact]
    public void GraphViewportState_RejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphViewportState(0d, 0d, 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphViewportState(double.NaN, 0d, 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphViewportState(1d, double.PositiveInfinity, 0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GraphViewportState(1d, 0d, double.NegativeInfinity));
    }
}
