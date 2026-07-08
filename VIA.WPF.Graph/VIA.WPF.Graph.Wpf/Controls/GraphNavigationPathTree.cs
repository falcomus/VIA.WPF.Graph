using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using VIA.WPF.Graph.Core.Projections;
using VIA.WPF.Graph.Core.Requests;

namespace VIA.WPF.Graph.Wpf.Controls;

/// <summary>
/// Minimal WPF navigation tree that renders a cycle-safe graph tree projection as mini cards.
/// </summary>
public sealed class GraphNavigationPathTree : FrameworkElement
{
    public static readonly DependencyProperty ProjectionProperty = DependencyProperty.Register(
        nameof(Projection),
        typeof(GraphTreeProjection),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
            OnProjectionChanged));

    public static readonly DependencyProperty SelectedTreeNodeIdProperty = DependencyProperty.Register(
        nameof(SelectedTreeNodeId),
        typeof(string),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedNodeIdProperty = DependencyProperty.Register(
        nameof(SelectedNodeId),
        typeof(string),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedLinkIdProperty = DependencyProperty.Register(
        nameof(SelectedLinkId),
        typeof(string),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GraphRequestCommandProperty = DependencyProperty.Register(
        nameof(GraphRequestCommand),
        typeof(ICommand),
        typeof(GraphNavigationPathTree),
        new FrameworkPropertyMetadata(null));

    private const double OuterPadding = 10d;
    private const double LevelIndent = 22d;
    private const double RowPitch = 58d;
    private const double CardWidth = 220d;
    private const double CardHeight = 44d;
    private const double CornerRadius = 5d;
    private const double TextSize = 11d;

    private static readonly Brush BackgroundBrush = CreateFrozenBrush(Color.FromRgb(250, 250, 250));
    private static readonly Brush RootFillBrush = CreateFrozenBrush(Color.FromRgb(255, 255, 255));
    private static readonly Brush BranchFillBrush = CreateFrozenBrush(Color.FromRgb(255, 255, 255));
    private static readonly Brush ReferenceFillBrush = CreateFrozenBrush(Color.FromRgb(255, 250, 225));
    private static readonly Brush TerminalFillBrush = CreateFrozenBrush(Color.FromRgb(242, 246, 248));
    private static readonly Brush MissingTargetFillBrush = CreateFrozenBrush(Color.FromRgb(255, 235, 238));
    private static readonly Brush TextBrush = CreateFrozenBrush(Color.FromRgb(24, 24, 24));
    private static readonly Brush MutedTextBrush = CreateFrozenBrush(Color.FromRgb(96, 96, 96));
    private static readonly Brush SelectionBrush = CreateFrozenBrush(Color.FromRgb(30, 115, 190));
    private static readonly Pen ConnectorPen = CreateFrozenPen(Color.FromRgb(150, 160, 168), 1d, DashStyles.Solid);
    private static readonly Pen CardPen = CreateFrozenPen(Color.FromRgb(120, 132, 142), 1d, DashStyles.Solid);
    private static readonly Pen ReferencePen = CreateFrozenPen(Color.FromRgb(156, 126, 42), 1.2d, DashStyles.Dash);
    private static readonly Pen MissingTargetPen = CreateFrozenPen(Color.FromRgb(190, 70, 70), 1.2d, DashStyles.Dash);
    private static readonly Pen SelectedPen = CreateFrozenPen(Color.FromRgb(30, 115, 190), 2d, DashStyles.Solid);
    private static readonly Typeface TextTypeface = new("Segoe UI");

    private IReadOnlyList<GraphNavigationTreeRow> rows = Array.Empty<GraphNavigationTreeRow>();

    public GraphNavigationPathTree()
    {
        Focusable = true;
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

    public ICommand? GraphRequestCommand
    {
        get => (ICommand?)GetValue(GraphRequestCommandProperty);
        set => SetValue(GraphRequestCommandProperty, value);
    }

    public bool SelectTreeNode(string treeNodeId)
    {
        GraphNavigationTreeRow? row = FindRow(treeNodeId);
        if (row is null)
        {
            return false;
        }

        SetSelectedRow(row);
        ExecuteSelectionRequest(row.Node);
        return true;
    }

    public bool OpenTreeNode(string treeNodeId)
    {
        GraphNavigationTreeRow? row = FindRow(treeNodeId);
        if (row is null)
        {
            return false;
        }

        SetSelectedRow(row);
        ExecuteOpenRequest(row.Node);
        return true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (rows.Count == 0)
        {
            return new Size(CardWidth + (OuterPadding * 2d), CardHeight + (OuterPadding * 2d));
        }

        double width = rows.Max(row => row.Bounds.Right) + OuterPadding;
        double height = rows[^1].Bounds.Bottom + OuterPadding;
        return new Size(width, height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        drawingContext.DrawRectangle(BackgroundBrush, null, new Rect(new Point(0d, 0d), RenderSize));
        DrawConnectors(drawingContext);

        foreach (GraphNavigationTreeRow row in rows)
        {
            DrawRow(drawingContext, row);
        }
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        Focus();
        GraphNavigationTreeRow? row = HitTestRow(e.GetPosition(this));
        if (row is null)
        {
            ClearSelection();
            ExecuteGraphRequest(GraphRequest.ClearSelection());
            e.Handled = true;
            return;
        }

        if (e.ClickCount > 1)
        {
            _ = OpenTreeNode(row.Node.TreeNodeId);
        }
        else
        {
            _ = SelectTreeNode(row.Node.TreeNodeId);
        }

        e.Handled = true;
    }

    private static void OnProjectionChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        GraphNavigationPathTree tree = (GraphNavigationPathTree)dependencyObject;
        tree.rows = BuildRows((GraphTreeProjection?)eventArgs.NewValue);
        tree.InvalidateMeasure();
        tree.InvalidateVisual();
    }

    private static IReadOnlyList<GraphNavigationTreeRow> BuildRows(GraphTreeProjection? projection)
    {
        if (projection is null || projection.Roots.Count == 0)
        {
            return Array.Empty<GraphNavigationTreeRow>();
        }

        List<GraphNavigationTreeRow> result = [];
        foreach (GraphTreeNode root in projection.Roots)
        {
            AppendRows(result, root, depth: 0, parentTreeNodeId: null);
        }

        return result;
    }

    private static void AppendRows(
        List<GraphNavigationTreeRow> rows,
        GraphTreeNode node,
        int depth,
        string? parentTreeNodeId)
    {
        double x = OuterPadding + (depth * LevelIndent);
        double y = OuterPadding + (rows.Count * RowPitch);
        Rect bounds = new(x, y, CardWidth, CardHeight);
        rows.Add(new GraphNavigationTreeRow(node, depth, parentTreeNodeId, bounds));

        foreach (GraphTreeNode child in node.Children)
        {
            AppendRows(rows, child, depth + 1, node.TreeNodeId);
        }
    }

    private void DrawConnectors(DrawingContext drawingContext)
    {
        Dictionary<string, GraphNavigationTreeRow> rowsById = rows.ToDictionary(row => row.Node.TreeNodeId, StringComparer.Ordinal);

        foreach (GraphNavigationTreeRow row in rows)
        {
            if (row.ParentTreeNodeId is null || !rowsById.TryGetValue(row.ParentTreeNodeId, out GraphNavigationTreeRow? parent))
            {
                continue;
            }

            Point start = new(parent.Bounds.Left + 10d, parent.Bounds.Bottom);
            Point end = new(row.Bounds.Left + 10d, row.Bounds.Top);
            Point corner = new(start.X, end.Y);
            StreamGeometry geometry = new();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(start, false, false);
                context.LineTo(corner, true, false);
                context.LineTo(end, true, false);
            }

            geometry.Freeze();
            drawingContext.DrawGeometry(null, ConnectorPen, geometry);
        }
    }

    private void DrawRow(DrawingContext drawingContext, GraphNavigationTreeRow row)
    {
        bool isSelected = StringComparer.Ordinal.Equals(SelectedTreeNodeId, row.Node.TreeNodeId);
        Brush fill = GetFillBrush(row.Node.Kind);
        Pen border = isSelected ? SelectedPen : GetBorderPen(row.Node.Kind);

        drawingContext.DrawRoundedRectangle(fill, border, row.Bounds, CornerRadius, CornerRadius);
        DrawText(drawingContext, row.Node.Title, row.Bounds, TextBrush, FontWeights.SemiBold, verticalOffset: 6d);
        DrawText(drawingContext, GetSubtitle(row.Node), row.Bounds, MutedTextBrush, FontWeights.Normal, verticalOffset: 24d);
    }

    private void DrawText(
        DrawingContext drawingContext,
        string text,
        Rect bounds,
        Brush brush,
        FontWeight fontWeight,
        double verticalOffset)
    {
        FormattedText formattedText = new(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            TextTypeface,
            TextSize,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(0d, bounds.Width - 16d),
            MaxTextHeight = 18d,
            Trimming = TextTrimming.CharacterEllipsis
        };
        formattedText.SetFontWeight(fontWeight);

        drawingContext.DrawText(formattedText, new Point(bounds.Left + 8d, bounds.Top + verticalOffset));
    }

    private GraphNavigationTreeRow? HitTestRow(Point point)
    {
        for (int index = rows.Count - 1; index >= 0; index--)
        {
            if (rows[index].Bounds.Contains(point))
            {
                return rows[index];
            }
        }

        return null;
    }

    private GraphNavigationTreeRow? FindRow(string treeNodeId)
    {
        string? normalizedTreeNodeId = NormalizeOptionalText(treeNodeId);
        return normalizedTreeNodeId is null
            ? null
            : rows.FirstOrDefault(row => StringComparer.Ordinal.Equals(row.Node.TreeNodeId, normalizedTreeNodeId));
    }

    private void SetSelectedRow(GraphNavigationTreeRow row)
    {
        SetCurrentValue(SelectedTreeNodeIdProperty, row.Node.TreeNodeId);
        SetCurrentValue(SelectedNodeIdProperty, row.Node.NodeId);
        SetCurrentValue(SelectedLinkIdProperty, NormalizeOptionalText(row.Node.LinkId));
        InvalidateVisual();
    }

    private void ClearSelection()
    {
        SetCurrentValue(SelectedTreeNodeIdProperty, null);
        SetCurrentValue(SelectedNodeIdProperty, null);
        SetCurrentValue(SelectedLinkIdProperty, null);
        InvalidateVisual();
    }

    private void ExecuteSelectionRequest(GraphTreeNode node)
    {
        if (node.Kind == GraphTreeNodeKind.MissingTarget && node.LinkId is not null)
        {
            ExecuteGraphRequest(GraphRequest.SelectLink(node.LinkId));
            return;
        }

        ExecuteGraphRequest(GraphRequest.SelectNode(node.NodeId));
    }

    private void ExecuteOpenRequest(GraphTreeNode node)
    {
        if (node.Kind == GraphTreeNodeKind.MissingTarget && node.LinkId is not null)
        {
            ExecuteGraphRequest(GraphRequest.OpenLink(node.LinkId));
            return;
        }

        ExecuteGraphRequest(GraphRequest.OpenNode(node.NodeId));
    }

    private void ExecuteGraphRequest(GraphRequest request)
    {
        ICommand? command = GraphRequestCommand;
        if (command is null || !command.CanExecute(request))
        {
            return;
        }

        command.Execute(request);
    }

    private static string GetSubtitle(GraphTreeNode node)
    {
        return node.Kind switch
        {
            GraphTreeNodeKind.Root => node.NodeId,
            GraphTreeNodeKind.Reference => $"Reference · {node.NodeId}",
            GraphTreeNodeKind.Terminal => $"Terminal · {node.NodeId}",
            GraphTreeNodeKind.MissingTarget => $"Missing target · {node.NodeId}",
            _ => node.LinkId is null ? node.NodeId : $"{node.LinkKind}: {node.LinkId}",
        };
    }

    private static Brush GetFillBrush(GraphTreeNodeKind kind)
    {
        return kind switch
        {
            GraphTreeNodeKind.Root => RootFillBrush,
            GraphTreeNodeKind.Reference => ReferenceFillBrush,
            GraphTreeNodeKind.Terminal => TerminalFillBrush,
            GraphTreeNodeKind.MissingTarget => MissingTargetFillBrush,
            _ => BranchFillBrush,
        };
    }

    private static Pen GetBorderPen(GraphTreeNodeKind kind)
    {
        return kind switch
        {
            GraphTreeNodeKind.Reference => ReferencePen,
            GraphTreeNodeKind.MissingTarget => MissingTargetPen,
            _ => CardPen,
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    private static Pen CreateFrozenPen(Color color, double thickness, DashStyle dashStyle)
    {
        Pen pen = new(CreateFrozenBrush(color), thickness)
        {
            DashStyle = dashStyle
        };
        pen.Freeze();
        return pen;
    }

    private sealed record GraphNavigationTreeRow(GraphTreeNode Node, int Depth, string? ParentTreeNodeId, Rect Bounds);
}
