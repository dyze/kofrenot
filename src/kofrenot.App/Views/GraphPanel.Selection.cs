using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// Several cards selected: a box drawn by dragging the background selects the cards it touches (Ctrl
/// held: added to those selected), Ctrl+click adds or takes out a card, Ctrl+A selects them all, Esc
/// none. Dragging one of them moves them all, kept as one placement (<see cref="GraphPlacer"/>). A
/// plain click on one of them selects it alone, on release, so a drag keeps them. The link handle,
/// Enter and Open act on one card.
/// </summary>
public partial class GraphPanel
{
    private readonly HashSet<Guid> _selection = [];

    // The box drawn, from where it started on the board, and the cards selected before it (Ctrl held).
    private Point? _boxFrom;
    private HashSet<Guid> _beforeBox = [];

    // A plain press on one of several cards selected: it alone is selected on release, unless they are dragged.
    private Guid? _pressedInSelection;

    /// <summary>The one card selected; null with none or several.</summary>
    private Guid? Selected => _selection.Count == 1 ? _selection.First() : null;

    private void Select(Guid? node) => SelectOnly(node is { } one ? [one] : []);

    /// <summary>Selects these cards, the others not: their rings changed, the link handle shown for one.</summary>
    private void SelectOnly(IEnumerable<Guid> nodes)
    {
        var chosen = nodes.Where(_cards.ContainsKey).ToHashSet();
        foreach (var node in _selection.Where(node => !chosen.Contains(node) && _cards.ContainsKey(node)))
        {
            GraphCard.Mark(_cards[node], isSelected: false);
        }

        foreach (var node in chosen.Where(node => !_selection.Contains(node)))
        {
            GraphCard.Mark(_cards[node], isSelected: true);
        }

        _selection.Clear();
        _selection.UnionWith(chosen);
        ShowLinkHandle();
    }

    /// <summary>A left press on a card: Ctrl adds or takes it out; the cards selected then may be dragged.</summary>
    private void PressCard(Guid node)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            SelectOnly(_selection.Contains(node) ? _selection.Where(selected => selected != node).ToList() : [.. _selection, node]);
        }
        else if (!_selection.Contains(node))
        {
            Select(node);
        }
        else if (_selection.Count > 1)
        {
            _pressedInSelection = node;
        }

        if (_selection.Contains(node))
        {
            _pressedCards = _selection.ToDictionary(selected => selected, selected => _positions[selected]);
        }
    }

    private void StartBox(Point onBoard, bool adds)
    {
        _boxFrom = onBoard;
        _beforeBox = adds ? [.. _selection] : [];
        if (!adds)
        {
            Select(null);
        }
    }

    // Shown once the mouse leaves the point pressed; the line as thin at every zoom.
    private void DragBox(Point onBoard)
    {
        if (_boxFrom is not { } from || (OnBoard(_pressedAt) - onBoard).Length * _matrix.M11 < DragStart)
        {
            return;
        }

        var box = new GraphBounds(Math.Min(from.X, onBoard.X), Math.Min(from.Y, onBoard.Y), Math.Max(from.X, onBoard.X), Math.Max(from.Y, onBoard.Y));
        Canvas.SetLeft(SelectionBox, box.Left);
        Canvas.SetTop(SelectionBox, box.Top);
        SelectionBox.Width = box.Width;
        SelectionBox.Height = box.Height;
        SelectionBox.StrokeThickness = 1 / _matrix.M11;
        SelectionBox.Visibility = Visibility.Visible;
        SelectOnly(_beforeBox.Concat(GraphLayout.CardsIn(CardsInOrder, box)));
    }

    private void StopBox()
    {
        _boxFrom = null;
        SelectionBox.Visibility = Visibility.Collapsed;
    }
}
