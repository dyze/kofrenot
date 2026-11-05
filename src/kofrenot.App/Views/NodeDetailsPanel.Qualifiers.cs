using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.History;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// A link's qualifiers: the chevron at the right of its row, before its remove button, unfolds them under it, edited as the node's fields are (Enter or leaving saves,
/// Esc puts back, empty clears; × takes one out, asked first). Add qualifier… adds one of the link's own, History…
/// lists their values to restore one. The row shows the first with a value after the other node's name.
/// </summary>
public partial class NodeDetailsPanel
{
    // The links whose qualifiers are unfolded, kept from node to node.
    private readonly HashSet<Guid> _unfolded = [];

    // Marks the panel of a link's qualifiers, to tell keys typed in it from the links list's.
    private static readonly object QualifiersTag = new();

    /// <summary>Whether the keyboard is in a link's qualifiers (a box, a list, Add qualifier…).</summary>
    private bool IsInQualifiers
    {
        get
        {
            for (var element = Keyboard.FocusedElement as DependencyObject; element is not null; element = VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element))
            {
                if (element is FrameworkElement { Tag: var tag } && tag == QualifiersTag)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Selects a link of the node shown, its qualifiers unfolded: a click on its line in a graph.</summary>
    public void UnfoldLink(Link link)
    {
        if (Browser.Details?.Links.FirstOrDefault(row => row.Link.Id == link.Id) is not { } row)
        {
            return;
        }

        _unfolded.Add(link.Id);
        Browser.Select(row);
        Log.Info("Action", "Link qualifiers shown");
        Show();
    }

    private void UnfoldQualifiers_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: LinkRow row } button)
        {
            var unfolded = _unfolded.Contains(row.Link.Id);
            button.Content = new IconView { Kind = unfolded ? "chevron-down" : "chevron-right", Size = (double)FindResource("RowIconSize") };
            button.ToolTip = unfolded ? "Hide the qualifiers of this link"
                : row.HasQualifiers ? "Show the qualifiers of this link"
                : "Add a qualifier to this link: a date, a source, a place";
        }
    }

    private void UnfoldQualifiers_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: LinkRow row } || !Leave(ask: true))
        {
            return;
        }

        var unfolded = _unfolded.Add(row.Link.Id) || !_unfolded.Remove(row.Link.Id);
        Log.Info("Action", unfolded ? "Link qualifiers shown" : "Link qualifiers hidden");
        Show();
    }

    private void QualifierHost_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ContentControl { DataContext: LinkRow row } host)
        {
            return;
        }

        var unfolded = _unfolded.Contains(row.Link.Id) && Browser.Graph is not null;
        host.Content = unfolded ? QualifiersOf(Browser.Graph!, row.Link) : null;
        host.Visibility = unfolded ? Visibility.Visible : Visibility.Collapsed;
    }

    private StackPanel QualifiersOf(KnowledgeGraph graph, Link link)
    {
        var canEdit = WriteChanges is not null;
        var panel = new StackPanel { Tag = QualifiersTag };
        var fields = NodeFields.OfLink(graph, link);
        foreach (var field in fields)
        {
            panel.Children.Add(FieldBlockOf(field, fields, canEdit, link));
        }

        if (fields.Count == 0)
        {
            panel.Children.Add(new TextBlock { Margin = new Thickness(0, 2, 0, 2), Opacity = 0.7, Text = "No qualifiers" });
        }

        var actions = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        var form = AddQualifierForm(link);
        if (canEdit)
        {
            var add = ActionButton("Add qualifier…", "Add a qualifier of this link's own: a date, a source, a place");
            add.Click += (_, _) =>
            {
                form.Visibility = Visibility.Visible;
                add.Visibility = Visibility.Collapsed;
                ((TextBox)form.Children[0]).Focus();
            };
            actions.Children.Add(add);
        }

        var history = ActionButton("History…", "The values of its qualifiers, to restore one");
        history.Click += async (_, _) => await ShowQualifierHistory(link);
        actions.Children.Add(history);
        panel.Children.Add(actions);
        panel.Children.Add(form);
        return panel;
    }

    private static Button ActionButton(string text, string tip) =>
        new() { Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 2, 10, 2), Focusable = false, Content = text, ToolTip = tip };

    // Add qualifier…: its name, its kind and its first value; Enter adds it, Esc closes.
    private WrapPanel AddQualifierForm(Link link)
    {
        var name = new TextBox { Width = 140, ToolTip = "Its name: Source, Place…" };
        var kind = new ComboBox { Margin = new Thickness(6, 0, 0, 0), ItemsSource = FieldPlanner.OwnKinds, SelectedIndex = 0 };
        var value = new TextBox { Margin = new Thickness(6, 0, 0, 0), Width = 180, ToolTip = "Its value" };
        var add = new Button { Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(10, 2, 10, 2), Content = "Add" };
        var cancel = new Button { Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(10, 2, 10, 2), Content = "Cancel" };
        var form = new WrapPanel { Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed, Children = { name, kind, value, add, cancel } };

        void Add()
        {
            if (kind.SelectedItem is FieldKind chosen
                && WriteFields(planner => planner.AddOwn(link, name.Text, chosen, value.Text), "Qualifier of its own added", holder: link))
            {
                Show();
            }
        }

        add.Click += (_, _) => Add();
        cancel.Click += (_, _) => Show();
        form.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Add();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                HideStatus();
                Show();
                e.Handled = true;
            }
        };
        return form;
    }

    private void RemoveQualifier(NodeField field, FieldHolder link)
    {
        if (MessageDialog.Confirm(Window.GetWindow(this), $"Take {field.Label} (“{field.Display}”) out of this link?", "Take out"))
        {
            WriteFields(planner => planner.Clear(link, field), "Qualifier taken out", holder: link);
        }
    }

    private async Task ShowQualifierHistory(Link link)
    {
        if (Browser is not { Graph: { } graph, Details: { } details } || !Leave(ask: true))
        {
            return;
        }

        Log.Info("Action", "Link history shown");
        var owner = Window.GetWindow(this);
        var title = $"{details.Name} {graph.Types.LinkName(link, details.Node.Id)} {graph.FindNode(link.Other(details.Node.Id))?.Name}";
        if (HistoryDialog.Ask(owner, title, NodeHistory.OfLink(graph, link)) is not { } entry || new RestorePlanner(graph).Plan(link, entry) is not { } plan)
        {
            return;
        }

        var row = new HistoryRow(entry);
        if (!MessageDialog.Confirm(owner, $"Restore the {row.What} of {row.When}: “{entry.Label}”?\n\nIt becomes the current one; the others stay in the history.", "Restore"))
        {
            Log.Info("Action", "Restore cancelled");
            return;
        }

        if (Restore is { } restore && await restore(plan))
        {
            Show();
        }
    }
}
