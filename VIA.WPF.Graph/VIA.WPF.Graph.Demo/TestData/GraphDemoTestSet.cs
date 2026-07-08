using VIA.WPF.Graph.Core.Model;

namespace VIA.WPF.Graph.Demo.TestData;

public sealed record GraphDemoTestSet
{
    public GraphDemoTestSet(
        string name,
        string description,
        GraphDocument document,
        GraphViewMode defaultViewMode = GraphViewMode.GroupOverview,
        double defaultVisualDensity = 1d)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Value must not be null, empty or whitespace.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Value must not be null, empty or whitespace.", nameof(description));
        }

        ArgumentNullException.ThrowIfNull(document);

        Name = name;
        Description = description;
        Document = document;
        DefaultViewMode = defaultViewMode;
        DefaultVisualDensity = defaultVisualDensity;
    }

    public string Name { get; }

    public string Description { get; }

    public GraphDocument Document { get; }

    public GraphViewMode DefaultViewMode { get; }

    public double DefaultVisualDensity { get; }

    public int NodeCount => Document.Nodes.Count;

    public int LinkCount => Document.Links.Count;

    public int GroupCount => Document.Groups.Count;

    public override string ToString()
    {
        return Name;
    }
}
