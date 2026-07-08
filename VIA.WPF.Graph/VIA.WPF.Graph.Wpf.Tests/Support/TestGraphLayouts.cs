using VIA.WPF.Graph.Core.Layout;

namespace VIA.WPF.Graph.Wpf.Tests.Support;

internal static class TestGraphLayouts
{
    public static GraphLayoutResult CreateBasicLayout()
    {
        return new GraphLayoutResult(
            "test-document",
            graphBounds: new GraphRect(0d, 0d, 400d, 240d),
            nodes:
            [
                new GraphLayoutNode("start", new GraphRect(20d, 40d, 120d, 60d)),
                new GraphLayoutNode("main", new GraphRect(220d, 120d, 140d, 70d))
            ],
            groups:
            [
                new GraphLayoutGroup("entry", new GraphRect(0d, 20d, 180d, 120d)),
                new GraphLayoutGroup("work", new GraphRect(190d, 90d, 190d, 130d))
            ],
            edges:
            [
                new GraphLayoutEdge("start_main", [new GraphPoint(140d, 70d), new GraphPoint(220d, 155d)]),
                new GraphLayoutEdge("missing_geometry", [], usesFallbackGeometry: true)
            ]);
    }
}
