using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// Drag and drop in a tree: a node dropped on another moves there (as Ctrl+X, Ctrl+V), or is copied
/// there with Ctrl held (as Ctrl+C, Ctrl+V). The node under the pointer is highlighted, and the
/// cursor says when a move is refused. Only inside kofrenot: the data is the dragged item.
/// </summary>
public partial class ViewPanel
{
    private const string DragFormat = "kofrenot.TreeItem";

    private Point? _dragStart;
    private TreeItem? _dragged;
    private Panel? _dropHeader;

    private void Tree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(Tree);
        _dragged = TreeViewItemAt(e.OriginalSource)?.Tag as TreeItem;
    }

    /// <summary>Starts the drag once the mouse has moved far enough, the button held.</summary>
    private void Tree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragStart is not { } start || _dragged is not { } dragged)
        {
            return;
        }

        var moved = e.GetPosition(Tree) - start;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragStart = null;
        Log.Debug("View", "Node dragged");
        DragDrop.DoDragDrop(Tree, new DataObject(DragFormat, dragged), DragDropEffects.Move | DragDropEffects.Copy);
        ShowDropTarget(null);
    }

    private void Tree_DragOver(object sender, DragEventArgs e)
    {
        var target = TreeViewItemAt(e.OriginalSource);
        e.Effects = EffectOf(e, target);
        ShowDropTarget(e.Effects == DragDropEffects.None ? null : target);
        e.Handled = true;
    }

    private void Tree_DragLeave(object sender, DragEventArgs e) => ShowDropTarget(null);

    private void Tree_Drop(object sender, DragEventArgs e)
    {
        var target = TreeViewItemAt(e.OriginalSource);
        var effect = EffectOf(e, target);
        ShowDropTarget(null);
        e.Handled = true;
        if (effect == DragDropEffects.None || e.Data.GetData(DragFormat) is not TreeItem dragged || target?.Tag is not TreeItem onto)
        {
            return;
        }

        SetExpanded(onto.Key, isExpanded: true);
        _selectedPath = onto.Key;
        if (effect == DragDropEffects.Copy)
        {
            _ = CopyUnder(dragged.Node, onto.Node);
        }
        else
        {
            Move(dragged, onto.Node);
        }
    }

    /// <summary>Copy with Ctrl held; otherwise a move, when it is allowed. None off a node, or for data from elsewhere.</summary>
    private DragDropEffects EffectOf(DragEventArgs e, TreeViewItem? target)
    {
        if (e.Data.GetData(DragFormat) is not TreeItem dragged || target?.Tag is not TreeItem onto || _graph is null || _definition is null)
        {
            return DragDropEffects.None;
        }

        if (e.KeyStates.HasFlag(DragDropKeyStates.ControlKey))
        {
            return DragDropEffects.Copy;
        }

        return new MovePlanner(_graph, _definition.LinkType).CanMove(dragged, onto.Node) == MoveProblem.None
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private void ShowDropTarget(TreeViewItem? target)
    {
        var header = target?.Header as Panel;
        if (header == _dropHeader)
        {
            return;
        }

        _dropHeader?.ClearValue(Panel.BackgroundProperty);
        header?.SetResourceReference(Panel.BackgroundProperty, "SubtleFillColorTertiaryBrush");
        _dropHeader = header;
    }

    /// <summary>The tree item an event comes from (its header, text, icon...), or null off the items.</summary>
    private static TreeViewItem? TreeViewItemAt(object source)
    {
        for (var element = source as DependencyObject;
             element is not null;
             element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
        {
            if (element is TreeViewItem item)
            {
                return item;
            }
        }

        return null;
    }
}
