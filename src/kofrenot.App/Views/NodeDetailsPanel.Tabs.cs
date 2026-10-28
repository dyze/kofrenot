using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>A tab of a node's details: a type (its content, or its fields), or the node's own fields.</summary>
/// <param name="IsContent">The type's content (a note's text, a dataset's file, a view's definition) shows in it.</param>
/// <param name="Fields">The fields it shows; none for a content's tab.</param>
public sealed record DetailsTab(string Name, bool IsContent, IReadOnlyList<NodeField> Fields)
{
    public bool HasConflict => Fields.Any(shown => shown.Conflict.Count > 0);
}

/// <summary>
/// A node of several types, or of a type and fields of its own, shows a tab per type, then "Its
/// own": a note's tab its editor, a dataset's its file, a view's what it shows, the others their
/// fields; links stay under them. The tab with content opens first, unless another was chosen:
/// kept by its name, from node to node. Leaving a tab writes what changed in it, as leaving the details.
/// </summary>
public partial class NodeDetailsPanel
{
    private const string OwnTab = "Its own";

    // The tab chosen, by its name: kept from node to node.
    private string? _chosenTab;

    // The tab shown; null when the node has one tab or none: then everything shows.
    private DetailsTab? _shownTab;

    /// <summary>Whether the node's content (note, dataset, view) shows: no tabs, or its tab is shown.</summary>
    private bool ContentShown => _shownTab is null or { IsContent: true };

    /// <summary>The fields shown: all of them without tabs, else the tab's.</summary>
    private IReadOnlyList<NodeField> ShowTabs(NodeDetails details)
    {
        var tabs = TabsOf(details);
        _shownTab = tabs.Count < 2 ? null
            : tabs.FirstOrDefault(tab => tab.Name == _chosenTab) ?? tabs.FirstOrDefault(tab => tab.IsContent) ?? tabs[0];
        DetailTabs.Children.Clear();
        DetailTabs.Visibility = _shownTab is null ? Visibility.Collapsed : Visibility.Visible;
        foreach (var tab in _shownTab is null ? [] : tabs)
        {
            DetailTabs.Children.Add(TabButton(tab, tab == _shownTab));
        }

        return _shownTab?.Fields ?? details.Fields;
    }

    private static List<DetailsTab> TabsOf(NodeDetails details)
    {
        var tabs = details.Types
            .Where(type => type != TypeCatalog.Generic)
            .Select(type => new DetailsTab(type.Name, type.HasContent, details.Fields.Where(field => field.Type == type).ToList()))
            .Where(tab => tab.IsContent || tab.Fields.Count > 0)
            .ToList();
        if (details.Fields.Where(field => field.IsOwn).ToList() is { Count: > 0 } own)
        {
            tabs.Add(new DetailsTab(OwnTab, IsContent: false, own));
        }

        return tabs;
    }

    // The tab shown underlined; one with a value in conflict marked.
    private Button TabButton(DetailsTab tab, bool isShown)
    {
        var button = new Button
        {
            Margin = new Thickness(0, 0, 4, 0),
            Padding = new Thickness(8, 2, 8, 2),
            Focusable = false,
            Style = (Style)FindResource("FlatButton"),
            Content = new TextBlock
            {
                Text = tab.HasConflict ? $"{tab.Name} •" : tab.Name,
                FontWeight = isShown ? FontWeights.SemiBold : FontWeights.Normal,
                TextDecorations = isShown ? TextDecorations.Underline : null,
            },
            ToolTip = tab.HasConflict ? "A value set on two devices: choose one" : null,
        };
        button.Click += (_, _) => ShowTab(tab);
        return button;
    }

    // Leaves the tab shown (its typed value written, its note saved), unless a file is being imported into its dataset.
    private void ShowTab(DetailsTab tab)
    {
        if (tab.Name == _shownTab?.Name || Browser.Details is not { } details)
        {
            return;
        }

        if (_shownTab is { IsContent: true } && DatasetBox.IsImporting(details.Node.Id))
        {
            MessageDialog.Inform(Window.GetWindow(this), "A file is being imported into this dataset: change tabs once it is done.");
            return;
        }

        if (LeaveFields())
        {
            LeaveNote();
            _chosenTab = tab.Name;
            Log.Info("Action", "Node details: tab shown");
            Show();
        }
    }

    /// <summary>Shows the tab of the node's own fields (one just added), when there are tabs.</summary>
    private void ShowOwnTab() => _chosenTab = OwnTab;
}
