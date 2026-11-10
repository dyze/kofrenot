using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;

namespace Kofrenot.Phone.Views;

/// <summary>
/// Cards, as on Windows (a toggle on a list's bar, kept per view on this phone in <see cref="ViewStates"/>):
/// the list's nodes in columns as cards (<see cref="CardColumns"/>, <see cref="CardView"/>), the pinned
/// ones first, then as arranged on Windows (<see cref="CardOrder"/>).
/// </summary>
public sealed partial class HomePage
{
    private bool ShowsCards(Vault vault, Node view) => _shell.Services.ViewStates.ShowsCards(vault.Id, view.Id);

    private void Cards_Click(object? sender, RoutedEventArgs e)
    {
        if (Vault is { } vault && _tab.View is { } view)
        {
            var showsCards = !ShowsCards(vault, view);
            _shell.Services.ViewStates.SetShowsCards(vault.Id, view.Id, showsCards);
            _shell.Services.Log.Info("Action", showsCards ? "View cards shown" : "View cards hidden");
            ShowList(vault);
        }
    }

    /// <summary>Whether ▦ shows (a list view), and whether it is on: the list's items then in columns.</summary>
    private void ShowCardsToggle(bool isList, bool showsCards)
    {
        CardsButton.IsVisible = isList;
        CardsIcon.Classes.Set("on", showsCards);
        if (showsCards)
        {
            UseItemsPanel("CardsPanel");
        }
    }

    private static List<object> CardRows(Vault vault, Node view, IReadOnlyList<Node> nodes)
    {
        var graph = vault.Graph;
        var order = new CardOrder(graph, view.Id);
        return [.. order.Of(nodes).Select(node => new CardRow(
            NodeRow.Of(graph, node),
            order.IsPinned(node),
            () => NodeCard.Of(graph, node, vault.ReadNote),
            version => vault.StateOf(version).Summary))];
    }
}
