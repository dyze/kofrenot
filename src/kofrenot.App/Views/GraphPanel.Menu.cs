using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// The right-click menus of a graph view. On a card: Open, and New node, linked to it through one
/// of the view's link types, either way, of the view's node type when it keeps only one. On a line:
/// Remove link, once confirmed (<see cref="DeletionPlanner.Link"/>). Nothing is changed in a vault read only.
/// </summary>
public partial class GraphPanel
{
    private void Surface_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (CardUnder(e.OriginalSource) is { } node && _graph?.FindNode(node) is { } card)
        {
            Select(node);
            ShowMenu(CardMenu(card));
            e.Handled = true;
        }
        else if (e.OriginalSource is FrameworkElement { Tag: GraphEdge edge })
        {
            ShowMenu(LineMenu(edge));
            e.Handled = true;
        }
    }

    private ContextMenu CardMenu(Node card)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuItems.Of("Open", "Enter", () => Open(card.Id)));
        var newNode = new MenuItem { Header = "New node", IsEnabled = !IsReadOnly() && CreateNode is not null };
        IReadOnlyList<LinkVerb> verbs = _graph is null || _definition is null ? [] : new GraphLinkPlanner(_graph).Verbs(_definition);
        foreach (var verb in verbs)
        {
            newNode.Items.Add(MenuItems.Of($"{card.Name} {verb.Name} …", string.Empty, () => AddNode(card, verb)));
        }

        newNode.IsEnabled &= newNode.Items.Count > 0;
        menu.Items.Add(newNode);
        menu.Closed += (_, _) => FocusContent();
        return menu;
    }

    private ContextMenu LineMenu(GraphEdge edge)
    {
        var menu = new ContextMenu();
        menu.Items.Add(MenuItems.Of("Remove link…", string.Empty, () => RemoveLink(edge.Link), isEnabled: !IsReadOnly()));
        menu.Closed += (_, _) => FocusContent();
        return menu;
    }

    /// <summary>A node added on the creation screen, linked to the card; selected, or said not shown.</summary>
    private void AddNode(Node card, LinkVerb verb)
    {
        if (_graph is null || _definition is null || CreateNode is null)
        {
            return;
        }

        Log.Info("Action", "New node from a graph view");
        var type = _definition.NodeTypes is [var only] ? _graph.Types.FindNodeType(only) : null;
        if (CreateNode(type, new ParentLink(card, verb.Type.Id, verb.IsReverse))?.NewNodes is not [var added, ..])
        {
            return;
        }

        if (_cards.ContainsKey(added.Id))
        {
            Reveal(added.Id);
        }
        else
        {
            MessageDialog.Inform(Window.GetWindow(this), $"“{added.Name}” is added, but this graph does not show it: its type is not one the view keeps, or it is past the view's depth.");
        }
    }

    private void RemoveLink(Link link)
    {
        if (_graph is null)
        {
            return;
        }

        var removal = new DeletionPlanner(_graph).Link(link, link.From);
        if (!MessageDialog.Confirm(Window.GetWindow(this), removal.Question, "Remove"))
        {
            Log.Info("Action", "Link removal cancelled");
            return;
        }

        Write?.Invoke([removal.Change], "Link removed in a graph view");
    }
}
