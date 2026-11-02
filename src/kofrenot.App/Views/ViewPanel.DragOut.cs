using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;
using Kofrenot.Core.Views;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>
/// Nodes dragged out of kofrenot, to a folder (<see cref="FolderExportPlanner"/>): several selected
/// in a tree, or any selected in a list, at once; one in a tree once the pointer leaves the window
/// (<see cref="DragNode"/>). A dataset gives its file; a node with children in the tree, a folder of
/// them. Windows needs them on disk before their drag starts, so they are exported first
/// (<see cref="DragExport"/>); a drop of files inside kofrenot does nothing. What is left out (not
/// downloaded, not a file) is told after the drop.
/// </summary>
public partial class ViewPanel
{
    // What a drag of several nodes carries in kofrenot's format: never a row's key, so no drop inside acts on it.
    private const string SeveralNodes = "*";

    private Point? _listDragStart;

    // Set by a drop on the tree during a drag, which then did not go out of kofrenot.
    private bool _droppedInside;

    // While a single node is dragged inside kofrenot: it has files to give, once the pointer leaves the window.
    private (FolderExport Export, DatasetFiles Files)? _exportOnLeave;

    // Set when that drag was stopped at the window's edge, to go on as files.
    private bool _leftWindow;

    private void DragOut(IReadOnlyList<Node> nodes)
    {
        if (PlanExport(nodes) is not { } planned)
        {
            Log.Debug("View", $"Nothing to drag out of {nodes.Count} nodes");
            return;
        }

        Log.Debug("View", $"Nodes dragged ({nodes.Count})");
        DragFiles(planned.Export, planned.Files);
    }

    /// <summary>
    /// One node of a tree, in two steps: first a drag inside kofrenot (a move, a copy), with no file;
    /// once the pointer leaves the window, that drag stops and the same one goes on as files, exported
    /// then (<see cref="Tree_QueryContinueDrag"/>). A move inside kofrenot decrypts nothing.
    /// </summary>
    private void DragNode(TreeItem dragged)
    {
        _exportOnLeave = PlanExport([dragged.Node]);
        _leftWindow = false;
        try
        {
            DragDrop.DoDragDrop(Tree, new DataObject(DragFormat, dragged.Key), DragDropEffects.Move | DragDropEffects.Copy);
        }
        finally
        {
            var planned = _exportOnLeave;
            _exportOnLeave = null;
            if (_leftWindow && planned is { } leaving)
            {
                Log.Debug("View", "Node dragged out of the window");
                DragFiles(leaving.Export, leaving.Files);
            }
        }
    }

    /// <summary>The pointer leaves the window, the button held: the drag inside kofrenot stops, to go on as files.</summary>
    private void Tree_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
    {
        if (_exportOnLeave is not null && !e.EscapePressed && e.KeyStates.HasFlag(DragDropKeyStates.LeftMouseButton) && IsPointerOutsideWindow())
        {
            _leftWindow = true;
            e.Action = DragAction.Cancel;
            e.Handled = true;
        }
    }

    private bool IsPointerOutsideWindow() =>
        Window.GetWindow(this) is { } window
        && NativeWindows.VisibleBounds(new WindowInteropHelper(window).Handle) is { } bounds
        && NativeWindows.CursorPosition() is { } pointer
        && (pointer.X < bounds.Left || pointer.X >= bounds.Right || pointer.Y < bounds.Top || pointer.Y >= bounds.Bottom);

    /// <summary>The files and folders of <paramref name="nodes"/>; null when none can be given (nothing on this device, or no file).</summary>
    private (FolderExport Export, DatasetFiles Files)? PlanExport(IReadOnlyList<Node> nodes) =>
        _graph is not null && DatasetFiles?.Invoke() is { } files && new FolderExportPlanner(_graph, _definition?.LinkType).Plan(nodes, files.Exists) is { IsEmpty: false } export
            ? (export, files)
            : null;

    /// <summary>Exports, then drags the files out; told after the drop what was left out.</summary>
    private void DragFiles(FolderExport export, DatasetFiles files)
    {
        var effect = DragExported(folder => [.. export.WriteTo(folder, files.Export)], DragDropEffects.Copy, SeveralNodes);
        if (IsDraggedOut(effect))
        {
            Log.Info("Action", $"Nodes dragged out of a view ({export.Files.Count} files, {export.Folders.Count} folders, {export.NotDownloaded} not downloaded, {export.NotFiles} not files)");
            TellLeftOut(export);
        }
    }

    /// <summary>
    /// Exports the files (<paramref name="export"/>, into a folder of the drag's own), then drags them,
    /// carrying <paramref name="ownData"/> for a drop inside kofrenot. The export holds the UI thread,
    /// the cursor waiting. Null when it fails (the user is told).
    /// </summary>
    private DragDropEffects? DragExported(Func<string, string[]> export, DragDropEffects allowed, string ownData)
    {
        _droppedInside = false;
        var source = Tree.Visibility == Visibility.Visible ? (DependencyObject)Tree : List;
        try
        {
            return DragExport.Drag(source, folder =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    return export(folder);
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }, allowed, DragFormat, ownData);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Warn("Action", $"Cannot export the files to drag ({ex.GetType().Name})");
            MessageDialog.Inform(Window.GetWindow(this), $"Cannot export the files.\n\n{ex.Message}");
            return null;
        }
    }

    private bool IsDraggedOut(DragDropEffects? effect) => effect is { } dropped && dropped != DragDropEffects.None && !_droppedInside;

    private void TellLeftOut(FolderExport export)
    {
        var left = new List<string>();
        if (export.NotDownloaded > 0)
        {
            left.Add($"{export.NotDownloaded} not downloaded to this device yet: open them first");
        }

        if (export.NotFiles > 0)
        {
            left.Add($"{export.NotFiles} with no file (notes, empty datasets, nodes with nothing under them)");
        }

        if (left.Count > 0)
        {
            MessageDialog.Inform(Window.GetWindow(this), $"Some nodes were not exported:\n\n{string.Join("\n", left)}");
        }
    }

    private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = IsRenaming ? null : RowAt(e.OriginalSource);
        _listDragStart = row is null ? null : e.GetPosition(List);
        e.Handled = row is not null && KeepSeveralOnPress(row);
    }

    /// <summary>Drags the rows selected out once the mouse has moved far enough, the button held.</summary>
    private void List_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _listDragStart is not { } start || !HasMovedToDrag(e, List, start))
        {
            return;
        }

        _listDragStart = null;
        _pressedInSelection = null;
        DragOut(SelectedNodes);
    }

    private void List_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _listDragStart = null;
        SelectPressedAlone();
    }

    /// <summary>The list row an event comes from, or null off the rows.</summary>
    private static ListBoxItem? RowAt(object source)
    {
        var element = source as DependencyObject;
        while (element is not null and not ListBoxItem)
        {
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }

        return element as ListBoxItem;
    }
}
