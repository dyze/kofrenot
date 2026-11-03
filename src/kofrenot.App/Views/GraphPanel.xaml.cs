using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// A graph view's content, read from the graph at each <see cref="Show"/> and laid out by
/// <see cref="GraphLayout"/>: a card per node, a line per link (<see cref="GraphLines"/>), a legend of
/// the link types, a minimap (<see cref="GraphMinimap"/>). A card dropped elsewhere is kept there
/// (<see cref="GraphPlacer"/>); the zoom and the pan stay while the same view is shown again, and fit
/// the graph when another one is. Its right-click menus are in GraphPanel.Menu, its links drawn by
/// hand in GraphPanel.Link.
/// </summary>
public partial class GraphPanel : UserControl
{
    private const double MinZoom = 0.15;
    private const double MaxZoom = 3;
    private const double ZoomStep = 1.15;
    private const double FitMargin = 24;

    // How far the mouse goes before a press on a card becomes a drag, not a click.
    private const double DragStart = 4;

    private readonly Dictionary<Guid, Border> _cards = [];
    private readonly GraphLines _lines;
    private KnowledgeGraph? _graph;
    private Node? _view;
    private GraphDefinition? _definition;
    private GraphContent? _content;
    private Guid? _laidOut;
    private Dictionary<Guid, NodePosition> _positions = [];
    private Guid? _selected;
    private Matrix _matrix = Matrix.Identity;
    private bool _needsFit;

    // A press on a card (a drag once the mouse goes far enough), or on the background (a pan).
    private Point _pressedAt;
    private Guid? _pressedCard;
    private NodePosition? _pressedPosition;
    private bool _isDragging;
    private Matrix? _panFrom;

    public GraphPanel()
    {
        InitializeComponent();
        _lines = new GraphLines(Lines);
        Minimap.CentreOn += CentreOn;
    }

    /// <summary>Asks to open a node's details.</summary>
    public event Action<Node>? OpenNode;

    /// <summary>Writes changes (a move, a reset of the layout, a link), logging the action; false when it could not.</summary>
    public Func<IReadOnlyList<Change>, string, bool>? Write { get; set; }

    /// <summary>Adds a node on the creation screen, of its type when given, linked to a card; the plan added, or null.</summary>
    public Func<NodeType?, ParentLink, AddPlan?>? CreateNode { get; set; }

    public Func<bool> IsReadOnly { get; set; } = () => false;

    public ILog Log { get; set; } = NullLog.Instance;

    /// <summary>Some nodes were moved by hand: <see cref="ResetLayout"/> has something to give back.</summary>
    public bool CanResetLayout { get; private set; }

    /// <summary>Shows the graph, the name of each link written on its line when <paramref name="showsLinkNames"/>; false when its root is not there (removed, or not synced yet).</summary>
    public bool Show(KnowledgeGraph graph, Node view, GraphDefinition definition, bool showsLinkNames)
    {
        var shown = new GraphView(graph, definition);
        if (shown.Read() is not { } content)
        {
            return false;
        }

        _graph = graph;
        _view = view;
        _definition = definition;
        _content = content;
        _positions = new Dictionary<Guid, NodePosition>(GraphLayout.Of(content, definition.Layout, graph.PositionsIn(view.Id)));
        _needsFit |= _laidOut != view.Id;
        _laidOut = view.Id;
        if (_selected is { } selected && !_positions.ContainsKey(selected))
        {
            _selected = null;
        }

        Cards.Children.Clear();
        _cards.Clear();
        foreach (var node in content.Nodes)
        {
            var card = GraphCard.Of(graph, node, shown.CardLinesOf(node), node.Id == _selected);
            _cards[node.Id] = card;
            Cards.Children.Add(card);
            Place(node.Id);
        }

        ShowLinkHandle();
        var names = content.Nodes.ToDictionary(node => node.Id, node => node.Name);
        _lines.Draw(content.Edges, _positions, id => names[id], showsLinkNames);
        ShowLegend(shown.Legend);
        Minimap.Draw(CardsInOrder);
        CutNote.Text = $"The first {GraphView.MaxNodes} nodes reached: lower the depth to see the others.";
        CutNote.Visibility = content.IsCut ? Visibility.Visible : Visibility.Collapsed;
        CanResetLayout = !IsReadOnly() && new GraphPlacer(graph).MovedCount(view.Id) > 0;
        FitIfNeeded();
        Transform(_matrix);
        return true;
    }

    /// <summary>Gives the keyboard to the graph.</summary>
    public void FocusContent() => Surface.Focus();

    /// <summary>The nodes shown whose name matches, in the order shown (<see cref="GraphContent.Find"/>).</summary>
    public IReadOnlyList<Node> Find(string text) => _content?.Find(text) ?? [];

    /// <summary>Selects a node shown, and centres the view on it.</summary>
    public void Reveal(Guid node)
    {
        if (_positions.TryGetValue(node, out var position))
        {
            Select(node);
            CentreOn(new Point(position.X, position.Y));
        }
    }

    /// <summary>Zooms to show every card (never above 1), centred.</summary>
    public void Fit()
    {
        _needsFit = false;
        if (GraphLayout.BoundsOf(_positions.Values) is not { } bounds)
        {
            return;
        }

        var zoom = Math.Clamp(Math.Min((Surface.ActualWidth - (2 * FitMargin)) / bounds.Width, (Surface.ActualHeight - (2 * FitMargin)) / bounds.Height), MinZoom, 1);
        var fitted = Matrix.Identity;
        fitted.Scale(zoom, zoom);
        fitted.Translate((Surface.ActualWidth / 2) - ((bounds.Left + bounds.Right) / 2 * zoom), (Surface.ActualHeight / 2) - ((bounds.Top + bounds.Bottom) / 2 * zoom));
        Transform(fitted);
    }

    /// <summary>Gives the nodes moved by hand back to the layout, once confirmed.</summary>
    public void ResetLayout()
    {
        if (_graph is null || _view is null)
        {
            return;
        }

        var placer = new GraphPlacer(_graph);
        var count = placer.MovedCount(_view.Id);
        if (placer.Reset(_view.Id) is { } reset
            && MessageDialog.Confirm(
                Window.GetWindow(this),
                $"Reset the layout of “{_view.Name}”?",
                "Reset",
                lines: [$"The places of {count} {(count == 1 ? "node" : "nodes")} moved by hand go: the layout places {(count == 1 ? "it" : "them")} again."]))
        {
            Write?.Invoke([reset], $"Graph layout reset ({count} nodes)");
        }

        FocusContent();
    }

    // In the order drawn: the last one is over the others.
    private IReadOnlyCollection<NodePosition> CardsInOrder => _cards.Keys.Select(node => _positions[node]).ToList();

    private void Place(Guid node)
    {
        var position = _positions[node];
        Canvas.SetLeft(_cards[node], position.X - (GraphLayout.CardWidth / 2));
        Canvas.SetTop(_cards[node], position.Y - (GraphLayout.CardHeight / 2));
        if (node == _selected)
        {
            ShowLinkHandle();
        }
    }

    private void Select(Guid? node)
    {
        if (_selected is { } previous && _cards.TryGetValue(previous, out var was))
        {
            GraphCard.Mark(was, isSelected: false);
        }

        _selected = node;
        if (node is { } selected && _cards.TryGetValue(selected, out var card))
        {
            GraphCard.Mark(card, isSelected: true);
        }

        ShowLinkHandle();
    }

    private void Open(Guid node)
    {
        if (_graph?.FindNode(node) is { } found)
        {
            OpenNode?.Invoke(found);
        }
    }

    // The link types and their colours; only when there are several to tell apart.
    private void ShowLegend(IReadOnlyList<GraphLegendItem> legend)
    {
        LegendRows.Children.Clear();
        foreach (var item in legend)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
            var swatch = new Line { X2 = 18, StrokeThickness = item.Role == GraphRole.Beside ? 3 : 2, Stroke = GraphPalette.Of(item.Color), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
            if (item.Role == GraphRole.Free)
            {
                swatch.StrokeDashArray = [3, 2];
            }

            row.Children.Add(swatch);
            row.Children.Add(new TextBlock { Text = item.Name, FontSize = 12 });
            LegendRows.Children.Add(row);
        }

        Legend.Visibility = legend.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FitIfNeeded()
    {
        if (_needsFit && Surface.ActualWidth > 0 && Surface.ActualHeight > 0)
        {
            Fit();
        }
    }

    private void Surface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        FitIfNeeded();
        Transform(_matrix);
    }

    /// <summary>Moves and zooms the board, and frames on the minimap the part now shown.</summary>
    private void Transform(Matrix matrix)
    {
        _matrix = matrix;
        Board.RenderTransform = new MatrixTransform(_matrix);
        if (_matrix.HasInverse)
        {
            var inverse = _matrix;
            inverse.Invert();
            Minimap.Frame(Rect.Transform(new Rect(0, 0, Surface.ActualWidth, Surface.ActualHeight), inverse));
        }
    }

    // A point of the graph, in the view's units, moved to the middle of the surface.
    private void CentreOn(Point point)
    {
        var at = _matrix.Transform(point);
        var centred = _matrix;
        centred.Translate((Surface.ActualWidth / 2) - at.X, (Surface.ActualHeight / 2) - at.Y);
        Transform(centred);
    }

    // A point of the surface, in the view's units.
    private Point OnBoard(Point onSurface)
    {
        var inverse = _matrix;
        inverse.Invert();
        return inverse.Transform(onSurface);
    }

    private void Surface_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? ZoomStep : 1 / ZoomStep;
        var zoom = Math.Clamp(_matrix.M11 * factor, MinZoom, MaxZoom);
        var around = e.GetPosition(Surface);
        var zoomed = _matrix;
        zoomed.ScaleAt(zoom / _matrix.M11, zoom / _matrix.M11, around.X, around.Y);
        Transform(zoomed);
        e.Handled = true;
    }

    private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Surface.Focus();
        _pressedAt = e.GetPosition(Surface);
        if (IsLinkHandle(e.OriginalSource) && _selected is { } linked)
        {
            StartLink(linked);
        }
        else if (CardUnder(e.OriginalSource) is { } node)
        {
            Select(node);
            if (e.ClickCount == 2)
            {
                Open(node);
                e.Handled = true;
                return;
            }

            _pressedCard = node;
            _pressedPosition = _positions[node];
        }
        else
        {
            Select(null);
            _panFrom = _matrix;
        }

        Surface.CaptureMouse();
        e.Handled = true;
    }

    private void Surface_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var offset = e.GetPosition(Surface) - _pressedAt;
        if (_linkFrom is not null)
        {
            DragLink(OnBoard(e.GetPosition(Surface)));
        }
        else if (_panFrom is { } from)
        {
            var panned = from;
            panned.Translate(offset.X, offset.Y);
            Transform(panned);
        }
        else if (_pressedCard is { } node && _pressedPosition is { } start && !IsReadOnly())
        {
            _isDragging |= offset.Length >= DragStart;
            if (_isDragging)
            {
                _positions[node] = start with { X = start.X + (offset.X / _matrix.M11), Y = start.Y + (offset.Y / _matrix.M11) };
                Place(node);
                _lines.Move(node, _positions);
            }
        }
    }

    private void Surface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var moved = _isDragging && _pressedCard is { } node ? _positions[node] : null;
        var linked = _linkFrom is { } from ? (From: from, To: CardAt(e.GetPosition(Surface))) : ((Guid From, Guid? To)?)null;
        Surface.ReleaseMouseCapture();
        if (moved is not null)
        {
            Keep(moved);
        }
        else if (linked is { To: { } to } link)
        {
            EndLink(link.From, to);
        }
    }

    private void Surface_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _pressedCard = null;
        _pressedPosition = null;
        _isDragging = false;
        _panFrom = null;
        StopLink();
    }

    /// <summary>Writes where a card was dropped; the vault's change shows the graph again.</summary>
    private void Keep(NodePosition moved)
    {
        if (_graph is null || _view is null || new GraphPlacer(_graph).Move(_view.Id, [moved]) is not { } placed)
        {
            return;
        }

        Write?.Invoke([placed], "Node placed in a graph view");
    }

    private void Surface_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && _selected is { } node)
        {
            Open(node);
            e.Handled = true;
        }
        else if (e.Key == Key.Home && Keyboard.Modifiers == ModifierKeys.None)
        {
            Fit();
            e.Handled = true;
        }
    }

    // The node of the card under the mouse, or null over the background.
    private Guid? CardUnder(object source)
    {
        for (var element = source as DependencyObject; element is not null && element != Board; element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
        {
            if (element is Border { Tag: Node node } && _cards.ContainsKey(node.Id))
            {
                return node.Id;
            }
        }

        return null;
    }

    // The node of the card at a point of the surface, while the mouse is captured (no source to walk up from).
    private Guid? CardAt(Point onSurface)
    {
        var at = OnBoard(onSurface);
        return GraphLayout.CardAt(CardsInOrder, at.X, at.Y);
    }
}
