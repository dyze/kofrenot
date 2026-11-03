using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// A graph view's content, read from the graph at each <see cref="Show"/> and laid out by
/// <see cref="GraphLayout"/>: a card per node, a line per link (<see cref="GraphLines"/>). A card dropped
/// elsewhere is kept there (<see cref="GraphPlacer"/>); the zoom and the pan stay while the same
/// view is shown again, and fit the graph when another one is.
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
    }

    /// <summary>Asks to open a node's details.</summary>
    public event Action<Node>? OpenNode;

    /// <summary>Writes changes (a move, a reset of the layout), logging the action; false when it could not.</summary>
    public Func<IReadOnlyList<Change>, string, bool>? Write { get; set; }

    public Func<bool> IsReadOnly { get; set; } = () => false;

    /// <summary>Shows the graph; false when its root is not there (removed, or not synced yet).</summary>
    public bool Show(KnowledgeGraph graph, Node view, GraphDefinition definition)
    {
        var shown = new GraphView(graph, definition);
        if (shown.Read() is not { } content)
        {
            return false;
        }

        _graph = graph;
        _view = view;
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

        _lines.Draw(content.Edges, _positions);
        CutNote.Text = $"The first {GraphView.MaxNodes} nodes reached: lower the depth to see the others.";
        CutNote.Visibility = content.IsCut ? Visibility.Visible : Visibility.Collapsed;
        ResetButton.IsEnabled = !IsReadOnly() && new GraphPlacer(graph).MovedCount(view.Id) > 0;
        FitIfNeeded();
        return true;
    }

    /// <summary>Gives the keyboard to the graph.</summary>
    public void FocusContent() => Surface.Focus();

    private void Place(Guid node)
    {
        var position = _positions[node];
        Canvas.SetLeft(_cards[node], position.X - (GraphLayout.CardWidth / 2));
        Canvas.SetTop(_cards[node], position.Y - (GraphLayout.CardHeight / 2));
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
    }

    private void Open(Guid node)
    {
        if (_graph?.FindNode(node) is { } found)
        {
            OpenNode?.Invoke(found);
        }
    }

    private void Fit_Click(object sender, RoutedEventArgs e)
    {
        Fit();
        FocusContent();
    }

    private void FitIfNeeded()
    {
        if (_needsFit && Surface.ActualWidth > 0 && Surface.ActualHeight > 0)
        {
            Fit();
        }
    }

    private void Surface_SizeChanged(object sender, SizeChangedEventArgs e) => FitIfNeeded();

    /// <summary>Zooms to show every card (never above 1), centred.</summary>
    private void Fit()
    {
        _needsFit = false;
        if (_positions.Count == 0)
        {
            return;
        }

        var (left, right) = (_positions.Values.Min(at => at.X) - (GraphLayout.CardWidth / 2), _positions.Values.Max(at => at.X) + (GraphLayout.CardWidth / 2));
        var (top, bottom) = (_positions.Values.Min(at => at.Y) - (GraphLayout.CardHeight / 2), _positions.Values.Max(at => at.Y) + (GraphLayout.CardHeight / 2));
        var zoom = Math.Clamp(Math.Min((Surface.ActualWidth - (2 * FitMargin)) / (right - left), (Surface.ActualHeight - (2 * FitMargin)) / (bottom - top)), MinZoom, 1);
        _matrix = Matrix.Identity;
        _matrix.Scale(zoom, zoom);
        _matrix.Translate((Surface.ActualWidth / 2) - ((left + right) / 2 * zoom), (Surface.ActualHeight / 2) - ((top + bottom) / 2 * zoom));
        Board.RenderTransform = new MatrixTransform(_matrix);
    }

    private void Surface_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? ZoomStep : 1 / ZoomStep;
        var zoom = Math.Clamp(_matrix.M11 * factor, MinZoom, MaxZoom);
        var around = e.GetPosition(Surface);
        _matrix.ScaleAt(zoom / _matrix.M11, zoom / _matrix.M11, around.X, around.Y);
        Board.RenderTransform = new MatrixTransform(_matrix);
        e.Handled = true;
    }

    private void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Surface.Focus();
        _pressedAt = e.GetPosition(Surface);
        if (CardUnder(e.OriginalSource) is { } node)
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
        if (_panFrom is { } from)
        {
            _matrix = from;
            _matrix.Translate(offset.X, offset.Y);
            Board.RenderTransform = new MatrixTransform(_matrix);
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
        Surface.ReleaseMouseCapture();
        if (moved is not null)
        {
            Keep(moved);
        }
    }

    private void Surface_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _pressedCard = null;
        _pressedPosition = null;
        _isDragging = false;
        _panFrom = null;
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

    private void Reset_Click(object sender, RoutedEventArgs e)
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
}
