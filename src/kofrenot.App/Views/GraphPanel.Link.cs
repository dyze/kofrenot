using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// A link drawn by hand: the card selected shows a handle on its edge nearest to the mouse; dragged to another card, it
/// links them through one of the view's link types (<see cref="GraphLinkPlanner"/>), the only one
/// offered, else the one chosen in a menu. Not in a vault read only, nor in a view without a link type known.
/// </summary>
public partial class GraphPanel
{
    private const double HandleSize = 14;

    private readonly Ellipse _linkHandle = CreateLinkHandle();

    // Where a link dragged would end, on the card under the mouse: shown only.
    private readonly Ellipse _targetHandle = CreateLinkHandle(isShownOnly: true);

    // The card a link is dragged from, the point of its edge, and the card it would go to, while it is.
    private Guid? _linkFrom;
    private Point _linkStart;
    private Guid? _linkTarget;

    // The mouse on the board, once it has moved there: the handle is on the edge nearest to it.
    private Point? _mouseOnBoard;

    // The right-click menu shown last, while it is open.
    private ContextMenu? _openMenu;

    private static Ellipse CreateLinkHandle(bool isShownOnly = false)
    {
        var handle = isShownOnly
            ? new Ellipse { Width = HandleSize, Height = HandleSize, StrokeThickness = 2, IsHitTestVisible = false }
            : new Ellipse { Width = HandleSize, Height = HandleSize, StrokeThickness = 2, Cursor = Cursors.Cross, ToolTip = "Drag to another card to link them" };
        handle.SetResourceReference(Shape.FillProperty, "CardBackgroundFillColorDefaultBrush");
        handle.SetResourceReference(Shape.StrokeProperty, "AccentFillColorDefaultBrush");
        return handle;
    }

    private bool IsLinkHandle(object source) => ReferenceEquals(source, _linkHandle);

    /// <summary>On the edge of the card selected nearest to the mouse, over the cards; none while a card is moved, nor when no link can be added.</summary>
    private void ShowLinkHandle()
    {
        Cards.Children.Remove(_linkHandle);
        if (_isDragging || _selected is not { } selected || !_positions.TryGetValue(selected, out var at) || !CanLink)
        {
            return;
        }

        PlaceLinkHandle(at);
        Cards.Children.Add(_linkHandle);
    }

    /// <summary>The mouse moved over the board, no button pressed: the handle follows it along the card's edge.</summary>
    private void FollowMouse(Point onBoard)
    {
        _mouseOnBoard = onBoard;
        if (_selected is { } selected && Cards.Children.Contains(_linkHandle) && _positions.TryGetValue(selected, out var at))
        {
            PlaceLinkHandle(at);
        }
    }

    // On the right edge until the mouse is seen.
    private void PlaceLinkHandle(NodePosition card)
    {
        var (x, y) = _mouseOnBoard is { } mouse ? GraphLayout.NearestBorderPoint(card, mouse.X, mouse.Y) : (card.X + (GraphLayout.CardWidth / 2), card.Y);
        PlaceHandle(_linkHandle, new Point(x, y));
    }

    private bool CanLink => !IsReadOnly() && _graph is not null && _definition is not null && new GraphLinkPlanner(_graph).Verbs(_definition).Count > 0;

    // From where the handle is.
    private void StartLink(Guid from)
    {
        _linkFrom = from;
        _linkStart = new Point(Canvas.GetLeft(_linkHandle) + (HandleSize / 2), Canvas.GetTop(_linkHandle) + (HandleSize / 2));
        LinkBand.Visibility = Visibility.Visible;
        DragLink(_linkStart);
    }

    /// <summary>
    /// Over another card, the link as it will be: that card marked, a handle on its border, the line from border to
    /// border, as straight lines are drawn (a rank's bend comes once the link type is known). Elsewhere, to the mouse.
    /// </summary>
    private void DragLink(Point to)
    {
        var target = GraphLayout.CardAt(CardsInOrder, to.X, to.Y) is { } over && over != _linkFrom ? over : (Guid?)null;
        MarkLinkTarget(target);
        if (target is not { } card)
        {
            Cards.Children.Remove(_targetHandle);
            PlaceHandle(_linkHandle, _linkStart);
            LinkBand.Data = new LineGeometry(_linkStart, to);
            return;
        }

        var (from, onto) = (_positions[_linkFrom!.Value], _positions[card]);
        var start = GraphLayout.BorderPoint(from, onto.X, onto.Y);
        var end = GraphLayout.BorderPoint(onto, from.X, from.Y);
        PlaceHandle(_linkHandle, new Point(start.X, start.Y));
        PlaceHandle(_targetHandle, new Point(end.X, end.Y));
        if (!Cards.Children.Contains(_targetHandle))
        {
            Cards.Children.Add(_targetHandle);
        }

        LinkBand.Data = new LineGeometry(new Point(start.X, start.Y), new Point(end.X, end.Y));
    }

    // The card a link would go to, ringed as the selected one is.
    private void MarkLinkTarget(Guid? target)
    {
        if (_linkTarget == target)
        {
            return;
        }

        if (_linkTarget is { } previous && previous != _selected && _cards.TryGetValue(previous, out var was))
        {
            GraphCard.Mark(was, isSelected: false);
        }

        _linkTarget = target;
        if (target is { } marked && _cards.TryGetValue(marked, out var card))
        {
            GraphCard.Mark(card, isSelected: true);
        }
    }

    private static void PlaceHandle(Ellipse handle, Point centre)
    {
        Canvas.SetLeft(handle, centre.X - (HandleSize / 2));
        Canvas.SetTop(handle, centre.Y - (HandleSize / 2));
    }

    private void StopLink()
    {
        MarkLinkTarget(null);
        Cards.Children.Remove(_targetHandle);
        _linkFrom = null;
        LinkBand.Visibility = Visibility.Collapsed;
        ShowLinkHandle();
    }

    /// <summary>Dropped on a card: links them, the only way the view offers, else the way chosen in a menu.</summary>
    private void EndLink(Guid from, Guid to)
    {
        if (_graph is null || _definition is null)
        {
            return;
        }

        var planner = new GraphLinkPlanner(_graph);
        var choices = planner.Choices(_definition, from, to);
        if (choices.Count == 0)
        {
            return;
        }

        if (choices.All(choice => choice.Exists))
        {
            Log.Info("Action", "Link drawn in a graph view: already there");
            MessageDialog.Inform(Window.GetWindow(this), "These nodes are already linked so.");
            return;
        }

        if (choices is [var only])
        {
            AddLink(planner, only);
            return;
        }

        var menu = new ContextMenu();
        foreach (var choice in choices)
        {
            var item = new MenuItem { Header = choice.Exists ? $"{choice.Label} (linked already)" : choice.Label, IsEnabled = !choice.Exists };
            item.Click += (_, _) => AddLink(planner, choice);
            menu.Items.Add(item);
        }

        ShowMenu(menu);
    }

    private void AddLink(GraphLinkPlanner planner, GraphLinkChoice choice)
    {
        if (planner.Add(choice) is { } added)
        {
            Write?.Invoke([added], "Link added in a graph view");
        }
    }

    // A menu at the mouse. Closed, it gives the keyboard back to the graph, unless another menu is open by then:
    // a right-click elsewhere closes one and opens the next, and the first one's Closed comes after.
    private void ShowMenu(ContextMenu menu)
    {
        _openMenu = menu;
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(_openMenu, menu))
            {
                _openMenu = null;
                FocusContent();
            }
        };
        menu.PlacementTarget = Surface;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }
}
