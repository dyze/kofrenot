using System.Windows;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// The action bar over a view: New, and in a tree Cut, Copy, Paste and Delete, as their keys do.
/// New adds a node through the creation screen: in a tree under the node selected (the root when
/// none), linked through the tree's link type; in a list, of the list's type.
/// </summary>
public partial class ViewPanel
{
    // The type of the nodes a list shows, when the vault knows it; null for a tree.
    private NodeType? _listType;

    /// <summary>Adds a node on the creation screen: its fixed type, or null; the node it goes under, or null. The plan added, or null.</summary>
    public Func<NodeType?, ParentLink?, AddPlan?>? CreateNode { get; set; }

    /// <summary>Shows the bar for what the view shows: a tree, a list, or nothing (a message).</summary>
    private void ShowActions()
    {
        var isTree = Tree.Visibility == Visibility.Visible;
        var isList = _definition is null && _listType is not null;
        ActionBar.Visibility = isTree || isList ? Visibility.Visible : Visibility.Collapsed;
        foreach (var treeOnly in new UIElement[] { CutButton, CopyButton, PasteButton, DeleteButton })
        {
            treeOnly.Visibility = isTree ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateActions();
    }

    /// <summary>The operations need a node selected; Paste, a node cut or copied too.</summary>
    private void UpdateActions()
    {
        var hasSelection = SelectedItem is not null;
        CutButton.IsEnabled = hasSelection;
        CopyButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
        PasteButton.IsEnabled = hasSelection && _taken is not null;
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (_graph is null || _view is null || CreateNode is null)
        {
            return;
        }

        Log.Info("Action", "New node from a view");
        if (_definition is { } definition && (SelectedItem ?? _tree?.Root) is { } parent)
        {
            if (CreateNode(null, new ParentLink(parent.Node, definition.LinkType))?.NewNodes is [var added, ..])
            {
                SetExpanded(parent.Key, isExpanded: true);
                _selectedPath = $"{parent.Key}/{added.Id}";
            }
        }
        else if (_listType is { } type && CreateNode(type, null)?.NewNodes is [var added, ..])
        {
            _selectedNode = added.Id;
        }

        Refresh();
    }

    private void Cut_Click(object sender, RoutedEventArgs e) => RunOnSelected(() => Take(isCut: true));

    private void Copy_Click(object sender, RoutedEventArgs e) => RunOnSelected(() => Take(isCut: false));

    private void Paste_Click(object sender, RoutedEventArgs e) => RunOnSelected(Paste);

    private void Delete_Click(object sender, RoutedEventArgs e) => RunOnSelected(Delete);

    // A button takes the keyboard: it goes back to the tree, on the node selected.
    private void RunOnSelected(Func<bool> action)
    {
        action();
        FocusContent();
    }
}
