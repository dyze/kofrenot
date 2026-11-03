using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// Unlinked (a toggle on a tree's bar, kept per view in <see cref="ViewStates"/>): the nodes without
/// a parent beside the tree (<see cref="UnlinkedPane"/>). Dropped on a node of the tree, they go
/// under it through the tree's link type (<see cref="UnlinkedNodes.PutUnder"/>).
/// </summary>
public partial class ViewPanel
{
    private const double UnlinkedWidth = 300;

    private bool ShowsUnlinked => _view is not null && States?.ShowsUnlinked(_vault, _view.Id) == true;

    private void Unlinked_Click(object sender, RoutedEventArgs e)
    {
        if (_view is null || States is null)
        {
            return;
        }

        var showsUnlinked = UnlinkedButton.IsChecked == true;
        States.SetShowsUnlinked(_vault, _view.Id, showsUnlinked);
        Log.Info("Action", showsUnlinked ? "Unlinked nodes shown beside a tree" : "Unlinked nodes hidden");
        ShowUnlinked();
    }

    /// <summary>Shows the unlinked nodes beside a tree when asked, read again from the graph; hides them otherwise.</summary>
    private void ShowUnlinked()
    {
        var shown = _definition is not null && Tree.Visibility == Visibility.Visible && ShowsUnlinked;
        UnlinkedButton.IsChecked = shown;
        Unlinked.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
        UnlinkedColumn.Width = new GridLength(shown ? UnlinkedWidth : 0);
        if (shown)
        {
            Unlinked.Show(_graph!);
        }
    }

    /// <summary>Unlinked nodes dragged onto a node of the tree: linked under it; none in a vault only read.</summary>
    private DragDropEffects UnlinkedEffectOf(DragEventArgs e, TreeViewItem? target) =>
        _graph is not null && _definition is not null && target?.Tag is TreeItem && !IsReadOnly() && UnlinkedPane.Dragged(e, _graph) is { Count: > 0 }
            ? DragDropEffects.Link
            : DragDropEffects.None;

    private void DropUnlinked(DragEventArgs e, TreeViewItem? target)
    {
        if (UnlinkedEffectOf(e, target) == DragDropEffects.None || target?.Tag is not TreeItem onto)
        {
            return;
        }

        var nodes = UnlinkedPane.Dragged(e, _graph!)!;
        var changes = new UnlinkedNodes(_graph!).PutUnder(nodes, new ParentLink(onto.Node, _definition!.LinkType));
        if (changes.Count == 0)
        {
            MessageDialog.Inform(Owner, $"Already under “{onto.Node.Name}”, or above it: nothing to put there.");
            return;
        }

        SetExpanded(onto.Key, isExpanded: true);
        _selectedPath = onto.Key;
        if (Write?.Invoke(changes, $"Unlinked nodes put in a tree ({changes.Count})") == true)
        {
            Refresh();
        }
    }
}
