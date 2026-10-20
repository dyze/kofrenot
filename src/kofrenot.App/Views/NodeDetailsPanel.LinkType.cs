using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>A link's right-click menu: Change link type, each way every link type reads, the current one checked.</summary>
public partial class NodeDetailsPanel
{
    /// <summary>Writes changes planned here (a link changed) in their order, logging the action given; false when it could not.</summary>
    public Func<IReadOnlyList<Change>, string, bool>? WriteChanges { get; set; }

    private void Link_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LinkRow row } frame || Browser is not { Graph: { } graph, Details: { } details } || WriteChanges is null)
        {
            return;
        }

        if (Browser.Select(row))
        {
            Show();
        }

        var current = graph.Types.VerbOf(row.Link, details.Node.Id);
        var change = new MenuItem { Header = "Change link type" };
        foreach (var verb in graph.Types.Verbs)
        {
            var item = new MenuItem { Header = verb.Name, IsCheckable = false, IsChecked = current is not null && current.Type.Id == verb.Type.Id && current.IsReverse == verb.IsReverse };
            item.Click += (_, _) => ChangeLink(row.Link, details.Node, verb);
            change.Items.Add(item);
        }

        var menu = new ContextMenu { PlacementTarget = frame };
        menu.Items.Add(change);
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void ChangeLink(Link link, Node seenFrom, LinkVerb target)
    {
        if (Browser.Graph is not { } graph || WriteChanges is null)
        {
            return;
        }

        var plan = new LinkChangePlanner(graph).Plan(link, seenFrom.Id, target);
        if (plan.Problem == LinkChangeProblem.Exists)
        {
            MessageDialog.Inform(Window.GetWindow(this), $"These nodes are linked “{target.Name}” already.");
            return;
        }

        if (plan.Changes is not [.., LinkAdded { Link: var added }] || !WriteChanges(plan.Changes, $"Link type changed ({link.Type} → {target.Type.Id}{(target.IsReverse ? ", reverse" : string.Empty)})"))
        {
            return;
        }

        Show();
        if (Browser.Details?.Links.FirstOrDefault(row => row.Link.Id == added.Id) is { } row && Browser.Select(row))
        {
            Show();
        }
    }
}
