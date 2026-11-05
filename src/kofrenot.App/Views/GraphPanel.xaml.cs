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
/// the graph when another one is. A right-drag pans. Its cards selected are in GraphPanel.Selection,
/// its right-click menus in GraphPanel.Menu, its links drawn by hand in GraphPanel.Link.
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
    private Matrix _matrix = Matrix.Identity;
    private bool _needsFit;

    // A left press on a card selected: the cards selected, from where they were (a drag once the mouse goes far enough).
    private Point _pressedAt;
    private Dictionary<Guid, NodePosition>? _pressedCards;
    private bool _isDragging;

    // A right press: the board as it was, panned once the mouse goes far enough (a right-click otherwise).
    private Matrix? _panFrom;
    private bool _hasPanned;

    public GraphPanel()
    {
        InitializeComponent();
        _lines = new GraphLines(Lines);
        Minimap.CentreOn += CentreOn;
    }

    /// <summary>Asks to open a node's details.</summary>
    public event Action<Node>? OpenNode;

    /// <summary>Asks to open a link's details, a click on its line: its qualifiers unfolded.</summary>
    public event Action<Link>? OpenLink;

    /// <summary>Writes changes (a move, a reset of the layout, a link), logging the action; false when it could not.</summary>
    public Func<IReadOnlyList<Change>, string, bool>? Write { get; set; }

    /// <summary>Adds a node on the creation screen, of its type when given, linked to a card or to none; the plan added, or null.</summary>
    public Func<NodeType?, ParentLink?, AddPlan?>? CreateNode { get; set; }

    public Func<bool> IsReadOnly { get; set; } = () => false;

    public ILog Log { get; set; } = NullLog.Instance;

    /// <summary>Some nodes were moved by hand: <see cref="ResetLayout"/> has something to give back.</summary>
    public bool CanResetLayout { get; private set; }

    /// <summary>Shows the graph, the name of each link written on its line when <paramref name="showsLinkNames"/>; false when its root is not there (removed, or not synced yet).</summary>
    public bool Show(KnowledgeGraph graph, Node view, GraphDefinition definition, bool showsLinkNames)
    {
        var shown = new GraphView(graph, definition, view.Id);
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
        _selection.IntersectWith(_positions.Keys);
        Cards.Children.Clear();
        _cards.Clear();
        foreach (var node in content.Nodes)
        {
            var card = GraphCard.Of(graph, node, shown.CardLinesOf(node), _selection.Contains(node.Id));
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
        if (node == Selected)
        {
            ShowLinkHandle();
        }
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
        if (_hasPanned)
        {
            return;
        }

        Surface.Focus();
        _pressedAt = e.GetPosition(Surface);
        if (IsLinkHandle(e.OriginalSource) && Selected is { } linked)
        {
            StartLink(linked);
        }
        else if (CardUnder(e.OriginalSource) is { } node)
        {
            if (e.ClickCount == 2)
            {
                Select(node);
                Open(node);
                e.Handled = true;
                return;
            }

            PressCard(node);
        }
        else if (e.OriginalSource is FrameworkElement { Tag: GraphEdge edge })
        {
            OpenLink?.Invoke(edge.Link);
            e.Handled = true;
            return;
        }
        else
        {
            StartBox(OnBoard(_pressedAt), adds: Keyboard.Modifiers == ModifierKeys.Control);
        }

        Surface.CaptureMouse();
        e.Handled = true;
    }

    // Not captured yet: a right-click that does not move stays a click, its menu shown over what it is on.
    private void Surface_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!Surface.IsMouseCaptured)
        {
            Surface.Focus();
            _pressedAt = e.GetPosition(Surface);
            _panFrom = _matrix;
        }
    }

    private void Surface_MouseMove(object sender, MouseEventArgs e)
    {
        var offset = e.GetPosition(Surface) - _pressedAt;
        if (e.RightButton == MouseButtonState.Pressed && _panFrom is { } from)
        {
            Pan(from, offset);
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            FollowMouse(OnBoard(e.GetPosition(Surface)));
            return;
        }

        if (_linkFrom is not null)
        {
            DragLink(OnBoard(e.GetPosition(Surface)));
        }
        else if (_boxFrom is not null)
        {
            DragBox(OnBoard(e.GetPosition(Surface)));
        }
        else if (_pressedCards is { } starts && !IsReadOnly())
        {
            _isDragging |= offset.Length >= DragStart;
            if (_isDragging)
            {
                foreach (var (node, start) in starts)
                {
                    _positions[node] = start with { X = start.X + (offset.X / _matrix.M11), Y = start.Y + (offset.Y / _matrix.M11) };
                    Place(node);
                }

                _lines.Move(starts.Keys, _positions);
            }
        }
    }

    private void Pan(Matrix from, Vector offset)
    {
        if (!_hasPanned && offset.Length >= DragStart)
        {
            _hasPanned = true;
            Surface.Cursor = Cursors.SizeAll;
            Surface.CaptureMouse();
        }

        if (_hasPanned)
        {
            var panned = from;
            panned.Translate(offset.X, offset.Y);
            Transform(panned);
        }
    }

    private void Surface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var moved = _isDragging && _pressedCards is { } starts ? starts.Keys.Select(node => _positions[node]).ToList() : null;
        var linked = _linkFrom is { } from ? (From: from, To: CardAt(e.GetPosition(Surface))) : ((Guid From, Guid? To)?)null;
        var alone = _isDragging ? null : _pressedInSelection;
        Surface.ReleaseMouseCapture();
        if (moved is not null)
        {
            Keep(moved);
        }
        else if (linked is { To: { } to } link)
        {
            EndLink(link.From, to);
        }
        else if (alone is { } node)
        {
            Select(node);
        }
    }

    private void Surface_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _pressedCards = null;
        _pressedInSelection = null;
        _isDragging = false;
        _panFrom = null;
        _hasPanned = false;
        Surface.ClearValue(CursorProperty);
        StopBox();
        StopLink();
    }

    /// <summary>Writes where the cards were dropped, as one placement; the vault's change shows the graph again.</summary>
    private void Keep(IReadOnlyList<NodePosition> moved)
    {
        if (_graph is null || _view is null || new GraphPlacer(_graph).Move(_view.Id, moved) is not { } placed)
        {
            return;
        }

        Write?.Invoke([placed], moved.Count == 1 ? "Node placed in a graph view" : $"Nodes placed in a graph view ({moved.Count})");
    }

    private void Surface_KeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = (e.Key, Keyboard.Modifiers) switch
        {
            (Key.Enter, ModifierKeys.None) when Selected is { } node => Run(() => Open(node)),
            (Key.Home, ModifierKeys.None) => Run(Fit),
            (Key.Escape, ModifierKeys.None) when _selection.Count > 0 => Run(() => Select(null)),
            (Key.A, ModifierKeys.Control) => Run(() => SelectOnly(_cards.Keys)),
            _ => false,
        };
    }

    private static bool Run(Action action)
    {
        action();
        return true;
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
