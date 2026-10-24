using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// The action bar over a view: New, Edit and Rename, and in a tree Cut, Copy, Paste and Delete, as
/// their keys do; New, Edit and Rename are also first in the right-click menus. New adds a node through the
/// creation screen: in a tree under the node selected (the root when none), linked through the
/// tree's link type; in a list, of the list's type. Edit opens the node's details, as Enter does.
/// </summary>
public partial class ViewPanel
{
    // The type of the nodes a list shows, when the vault knows it; null for a tree.
    private NodeType? _listType;

    /// <summary>Adds a node on the creation screen: its fixed type, or null; the node it goes under, or null; a file dropped for a dataset, or null. The plan added, or null.</summary>
    public Func<NodeType?, ParentLink?, string?, AddPlan?>? CreateNode { get; set; }

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

        DetailsButton.Visibility = isTree || (isList && !ShowsCards) ? Visibility.Visible : Visibility.Collapsed;
        CardsButton.Visibility = isList ? Visibility.Visible : Visibility.Collapsed;
        CardsButton.IsChecked = ShowsCards;
        DetailsButton.IsChecked = ShowsDetails;
        DetailsHeader.Visibility = (isTree || (isList && !ShowsCards)) && ShowsDetails ? Visibility.Visible : Visibility.Collapsed;
        UpdateActions();
    }

    /// <summary>The operations need a node selected; Paste, a node cut or copied too.</summary>
    private void UpdateActions()
    {
        EditButton.IsEnabled = SelectedNode is not null;
        RenameButton.IsEnabled = SelectedNode is not null;
        var hasSelection = SelectedItem is not null;
        CutButton.IsEnabled = hasSelection;
        CopyButton.IsEnabled = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
        PasteButton.IsEnabled = hasSelection && _taken is not null;
    }

    private void New_Click(object sender, RoutedEventArgs e) => AddNode();

    private void Edit_Click(object sender, RoutedEventArgs e) => OpenSelected();

    private void Rename_Click(object sender, RoutedEventArgs e) => StartRename();

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActions();

    /// <summary>The list's menu: New… and Edit.</summary>
    private void List_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var menu = List.ContextMenu!;
        menu.Items.Clear();
        AddNodeItems(menu);
    }

    /// <summary>New…, Edit and Rename, first in a menu.</summary>
    private void AddNodeItems(ContextMenu menu)
    {
        menu.Items.Add(MenuItemOf("New…", string.Empty, AddNode, isEnabled: CreateNode is not null && (_definition is not null || _listType is not null)));
        menu.Items.Add(MenuItemOf("Edit", "Enter", () => OpenSelected(), isEnabled: SelectedNode is not null));
        // Once the menu has closed and given the keyboard back, so the box keeps it.
        menu.Items.Add(MenuItemOf("Rename", "F2", () => Dispatcher.BeginInvoke(StartRename, System.Windows.Threading.DispatcherPriority.Input), isEnabled: SelectedNode is not null));
    }

    /// <summary>New: a node on the creation screen, under the node selected in a tree, of the list's type in a list; then selected.</summary>
    private void AddNode()
    {
        if (_graph is null || _view is null || CreateNode is null)
        {
            return;
        }

        Log.Info("Action", "New node from a view");
        if (_definition is { } definition && (SelectedItem ?? _tree?.Root) is { } parent)
        {
            if (CreateNode(null, new ParentLink(parent.Node, definition.LinkType), null)?.NewNodes is [var added, ..])
            {
                SetExpanded(parent.Key, isExpanded: true);
                _selectedPath = $"{parent.Key}/{added.Id}";
            }
        }
        else if (_listType is { } type && CreateNode(type, null, null)?.NewNodes is [var added, ..])
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
