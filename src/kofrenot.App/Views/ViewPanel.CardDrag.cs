using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// A card dragged onto another takes its place, within its part (pinned or not): the other cards
/// shift, and the new order is written (<see cref="Kofrenot.Core.Views.CardOrder"/>), so it syncs.
/// While dragged the card is dimmed, and the one whose place it would take is ringed. Leaving the
/// window, it goes on as files (<see cref="DragInside"/>).
/// </summary>
public partial class ViewPanel
{
    private const string CardFormat = "kofrenot.Card";
    private const double DraggedCardOpacity = 0.5;

    private ListBoxItem? _cardDropTarget;

    private void DragCard(ListBoxItem card)
    {
        if (card.Tag is not Node node)
        {
            return;
        }

        Log.Debug("View", "Card dragged");
        card.Opacity = DraggedCardOpacity;
        try
        {
            DragInside(List, new DataObject(CardFormat, node.Id.ToString()), node, DragDropEffects.Move);
        }
        finally
        {
            card.Opacity = 1;
            ShowCardDropTarget(null);
        }
    }

    private void List_DragOver(object sender, DragEventArgs e)
    {
        var drop = CardDropOf(e);
        e.Effects = drop is null ? DragDropEffects.None : DragDropEffects.Move;
        ShowCardDropTarget(drop?.Target);
        e.Handled = true;
    }

    // Raised too when the pointer goes from one part of a card to another: cleared only once out of the list.
    private void List_DragLeave(object sender, DragEventArgs e)
    {
        var at = e.GetPosition(List);
        if (at.X < 0 || at.Y < 0 || at.X >= List.ActualWidth || at.Y >= List.ActualHeight)
        {
            ShowCardDropTarget(null);
        }
    }

    private void List_Drop(object sender, DragEventArgs e)
    {
        ShowCardDropTarget(null);
        e.Handled = true;
        if (CardDropOf(e) is not { } drop || _cardOrder?.Move(_cards, drop.From, drop.To) is not { } placed)
        {
            return;
        }

        if (Write?.Invoke([placed], "Card moved in a list view") == true)
        {
            _selectedNode = _cards[drop.From].Id;
            Refresh();
        }
    }

    /// <summary>The card dragged over another of its part, both by their place in the order shown; null otherwise.</summary>
    private (ListBoxItem Target, int From, int To)? CardDropOf(DragEventArgs e)
    {
        if (_cardOrder is null || IsReadOnly() || e.Data.GetData(CardFormat) is not string id || RowAt(e.OriginalSource) is not { Tag: Node onto } target)
        {
            return null;
        }

        var from = IndexOfCard(id);
        var to = IndexOfCard(onto.Id.ToString());
        return from >= 0 && to >= 0 && from != to && _cardOrder.IsPinned(_cards[from]) == _cardOrder.IsPinned(onto) ? (target, from, to) : null;
    }

    private int IndexOfCard(string id) => _cards.ToList().FindIndex(node => node.Id.ToString() == id);

    private void ShowCardDropTarget(ListBoxItem? target)
    {
        if (target == _cardDropTarget)
        {
            return;
        }

        _cardDropTarget?.ClearValue(BorderBrushProperty);
        _cardDropTarget = target;
        target?.SetResourceReference(BorderBrushProperty, "AccentFillColorDefaultBrush");
    }
}
