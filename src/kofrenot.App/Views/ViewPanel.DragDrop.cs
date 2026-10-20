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
/// cursor says when a move is refused. A dataset dragged out of kofrenot (to a folder) gives its
/// file, exported only then; a file dropped on a node from elsewhere becomes a new dataset under it.
/// </summary>
public partial class ViewPanel
{
    private const string DragFormat = "kofrenot.TreeItem";

    /// <summary>The open vault's dataset files, to drag a dataset's file out; null when none is open.</summary>
    public Func<DatasetFiles?>? DatasetFiles { get; set; }

    private Point? _dragStart;
    private TreeItem? _dragged;
    private Panel? _dropHeader;

    private void Tree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // In a name being edited, the mouse selects text: no drag.
        _dragStart = IsRenaming ? null : e.GetPosition(Tree);
        _dragged = IsRenaming ? null : TreeViewItemAt(e.OriginalSource)?.Tag as TreeItem;
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
        if (_graph?.DatasetVersionsOf(dragged.Node.Id) is [.., var latest] && DatasetFiles?.Invoke() is { } files)
        {
            DragWithFile(dragged, latest, files);
        }
        else
        {
            DragDrop.DoDragDrop(Tree, new DataObject(DragFormat, dragged), DragDropEffects.Move | DragDropEffects.Copy);
        }

        ShowDropTarget(null);
    }

    /// <summary>A dataset: the same drag, its file offered to other programs, exported only when one asks for it.</summary>
    private void DragWithFile(TreeItem dragged, DatasetEdited latest, DatasetFiles files)
    {
        Exception? failure = null;
        var (_, draggedOut) = DragExport.Drag(Tree, DragFormat, dragged, latest.File.SafeName, path =>
        {
            // Asked by another program during the drag: an error is told once the drag is over.
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                files.Export(latest.Id, path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                failure = ex;
                throw;
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }, DragDropEffects.Move | DragDropEffects.Copy);

        if (failure is not null)
        {
            Log.Warn("Action", $"Cannot drag the dataset's file out ({failure.GetType().Name})");
            MessageDialog.Inform(Window.GetWindow(this), $"Cannot export the file.\n\n{failure.Message}");
        }
        else if (draggedOut)
        {
            Log.Info("Action", $"Dataset dragged out of a view ({latest.File.Size} bytes)");
        }
    }

    private void Tree_DragOver(object sender, DragEventArgs e)
    {
        var target = TreeViewItemAt(e.OriginalSource);
        e.Effects = e.Data.GetDataPresent(DragFormat) ? EffectOf(e, target) : FileEffectOf(e, target);
        ShowDropTarget(e.Effects == DragDropEffects.None ? null : target);
        e.Handled = true;
    }

    /// <summary>One file from elsewhere, dropped on a node of a tree: a new dataset under it. None for several files, or a folder.</summary>
    private DragDropEffects FileEffectOf(DragEventArgs e, TreeViewItem? target) =>
        DroppedFile(e) is not null && target?.Tag is TreeItem && _definition is not null && CreateNode is not null
            ? DragDropEffects.Copy
            : DragDropEffects.None;

    private static string? DroppedFile(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths && File.Exists(paths[0]) ? paths[0] : null;

    /// <summary>Shows the creation screen for a dataset of the file dropped, under <paramref name="onto"/>; after the drop, so the other program is not held.</summary>
    private void DropFile(TreeItem onto, string path)
    {
        if (_definition is not { } definition || CreateNode is null)
        {
            return;
        }

        Log.Info("Action", "File dropped on a view");
        Dispatcher.BeginInvoke(() =>
        {
            if (CreateNode(TypeCatalog.Dataset, new ParentLink(onto.Node, definition.LinkType), path)?.NewNodes is [var added, ..])
            {
                SetExpanded(onto.Key, isExpanded: true);
                _selectedPath = $"{onto.Key}/{added.Id}";
            }

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
            if (FileEffectOf(e, target) != DragDropEffects.None && target?.Tag is TreeItem item && DroppedFile(e) is { } path)
            {
                DropFile(item, path);
            }

            return;
        }

        var effect = EffectOf(e, target);
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
