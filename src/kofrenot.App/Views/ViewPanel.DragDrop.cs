using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>
/// Drag and drop in a tree: a node dropped on another moves there (as Ctrl+X, Ctrl+V), or is copied
/// there with Ctrl held (as Ctrl+C, Ctrl+V). The node under the pointer is highlighted, and the
/// cursor says when a move is refused. Leaving the window, the node goes on as files to other
/// programs (a folder): a dataset its file, a node with children a folder of them; several nodes selected are dragged out together (<see cref="DragOut"/>).
/// A file dropped on a node from elsewhere becomes a new dataset under it (the creation screen),
/// several files or folders as many nodes (their preview first).
/// </summary>
public partial class ViewPanel
{
    private const string DragFormat = "kofrenot.TreeItem";

    /// <summary>The open vault's dataset files, to drag a dataset's file out when it is here; null when none is open.</summary>
    public Func<DatasetFiles?>? DatasetFiles { get; set; }

    /// <summary>Imports several files dropped as datasets under a node, once their preview is accepted.</summary>
    public Func<IReadOnlyList<string>, ParentLink, Task>? ImportFiles { get; set; }

    private Point? _dragStart;
    private TreeItem? _dragged;
    private TreeViewItem? _dropTarget;

    private void Tree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // In a name being edited, the mouse selects text: no drag.
        var row = IsRenaming || IsOnExpander(e.OriginalSource) ? null : TreeViewItemAt(e.OriginalSource);
        if (row is { Tag: TreeItem } && MarkOnClick(row))
        {
            _dragStart = null;
            e.Handled = true;
            return;
        }

        _dragStart = row is null ? null : e.GetPosition(Tree);
        _dragged = row?.Tag as TreeItem;
        e.Handled = row is not null && KeepSeveralOnPress(row);
    }

    /// <summary>Starts the drag once the mouse has moved far enough, the button held.</summary>
    private void Tree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragStart is not { } start || _dragged is not { } dragged || !HasMovedToDrag(e, Tree, start))
        {
            return;
        }

        _dragStart = null;
        if (_pressedInSelection is not null)
        {
            _pressedInSelection = null;
            DragOut(SelectedNodes);
            return;
        }

        Log.Debug("View", "Node dragged");
        DragNode(dragged);
        ShowDropTarget(null);
    }

    /// <summary>The tree row dragged inside kofrenot (its key carried as text), or null: several nodes, or data from elsewhere.</summary>
    private TreeItem? DraggedItem(DragEventArgs e) =>
        e.Data.GetData(DragFormat) is string key && _dragged is { } dragged && dragged.Key == key ? dragged : null;

    private void Tree_DragOver(object sender, DragEventArgs e)
    {
        var target = TreeViewItemAt(e.OriginalSource);
        e.Effects = e.Data.GetDataPresent(DragFormat) ? EffectOf(e, target) : FileEffectOf(e, target);
        ShowDropTarget(e.Effects == DragDropEffects.None ? null : target);
        e.Handled = true;
    }

    /// <summary>Files from elsewhere, dropped on a node of a tree: new datasets under it.</summary>
    private DragDropEffects FileEffectOf(DragEventArgs e, TreeViewItem? target) =>
        DroppedFiles(e) is not null && target?.Tag is TreeItem && _definition is not null && CreateNode is not null && ImportFiles is not null
            ? DragDropEffects.Copy
            : DragDropEffects.None;

    private static string[]? DroppedFiles(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths ? paths : null;

    /// <summary>
    /// One file: the creation screen for its dataset, under <paramref name="onto"/>; several (or a
    /// folder): their preview, then a dataset each. After the drop, so the other program is not held.
    /// </summary>
    private void DropFiles(TreeItem onto, string[] paths)
    {
        if (_definition is not { } definition || CreateNode is null || ImportFiles is null)
        {
            return;
        }

        var under = new ParentLink(onto.Node, definition.LinkType);
        Log.Info("Action", $"Files dropped on a view ({paths.Length})");
        Dispatcher.BeginInvoke(async () =>
        {
            if (paths is [var path] && File.Exists(path))
            {
                if (CreateNode(TypeCatalog.Dataset, under, path)?.NewNodes is [var added, ..])
                {
                    _selectedPath = $"{onto.Key}/{added.Id}";
                }
            }
            else
            {
                await ImportFiles(paths, under);
            }

            SetExpanded(onto.Key, isExpanded: true);
            Refresh();
        });
    }

    private void Tree_DragLeave(object sender, DragEventArgs e) => ShowDropTarget(null);

    private void Tree_Drop(object sender, DragEventArgs e)
    {
        var target = TreeViewItemAt(e.OriginalSource);
        ShowDropTarget(null);
        e.Handled = true;
        if (!e.Data.GetDataPresent(DragFormat))
        {
            if (FileEffectOf(e, target) != DragDropEffects.None && target?.Tag is TreeItem item && DroppedFiles(e) is { } paths)
            {
                DropFiles(item, paths);
            }

            return;
        }

        // Ours, even when it does nothing (several nodes): not dragged out.
        _droppedInside = true;
        var effect = EffectOf(e, target);
        if (effect == DragDropEffects.None || DraggedItem(e) is not { } dragged || target?.Tag is not TreeItem onto)
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
        if (DraggedItem(e) is not { } dragged || target?.Tag is not TreeItem onto || _graph is null || _definition is null)
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
        if (target == _dropTarget)
        {
            return;
        }

        var previous = _dropTarget;
        _dropTarget = target;
        if (previous is not null)
        {
            Paint(previous);
        }

        (target?.Header as Panel)?.SetResourceReference(Panel.BackgroundProperty, "SubtleFillColorTertiaryBrush");
    }

    private static bool HasMovedToDrag(MouseEventArgs e, IInputElement from, Point start)
    {
        var moved = e.GetPosition(from) - start;
        return Math.Abs(moved.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(moved.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    /// <summary>Whether an event comes from a row's arrow, which expands it: not a click on the node.</summary>
    private static bool IsOnExpander(object source)
    {
        for (var element = source as DependencyObject; element is not null and not TreeViewItem; element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
        {
            if (element is System.Windows.Controls.Primitives.ToggleButton)
            {
                return true;
            }
        }

        return false;
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
