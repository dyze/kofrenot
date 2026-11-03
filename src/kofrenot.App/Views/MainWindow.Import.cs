using System.Windows;
using System.Windows.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// Files secured quickly: dropped on the drop zone (or several on a tree), they become datasets,
/// named after their files, once their preview is accepted.
/// </summary>
public partial class MainWindow
{
    /// <summary>
    /// Shows the preview of importing <paramref name="paths"/>, then imports them when accepted:
    /// under <paramref name="under"/> when dropped on a node, else to this vault's last destination
    /// (<see cref="ImportDestinations"/>), which the preview can change, and which is kept for the next one.
    /// </summary>
    private async Task ImportFiles(IReadOnlyList<string> paths, ParentLink? under)
    {
        if (_session.Current is not { } vault)
        {
            return;
        }

        var planner = new FileImportPlanner(vault.Graph);
        var choice = under is null
            ? ImportPreviewDialog.Ask(this, vault.Graph, _destinations.Of(vault.Id), parent => planner.Preview(paths, parent))
            : ImportPreviewDialog.Ask(this, planner.Preview(paths, under));
        if (choice is null || !ConfirmUpdates(choice.Preview))
        {
            _log.Info("Action", "Import of files cancelled");
            return;
        }

        if (under is null)
        {
            _destinations.Set(vault.Id, choice.Destination);
        }

        await _session.ImportFiles(Ask, choice.Preview);
    }

    /// <summary>Asks before existing datasets get a new version, naming them; true when there are none.</summary>
    private bool ConfirmUpdates(ImportPreview preview)
    {
        var updated = preview.Rows.Where(row => row.IsUpdate).Select(row => row.NodeName).ToList();
        var datasets = updated.Count == 1 ? "1 existing dataset" : $"{updated.Count} existing datasets";
        return updated.Count == 0
            || MessageDialog.Confirm(this, $"Update {datasets} with a new version? Their earlier versions are kept.", "Update", lines: updated);
    }

    private static string[]? DroppedFiles(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths ? paths : null;

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        var accepted = DroppedFiles(e) is not null && _session.Current is not null;
        e.Effects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        DropZone.BorderBrush = accepted ? (Brush)FindResource("AccentFillColorDefaultBrush") : (Brush)FindResource("ControlStrokeColorDefaultBrush");
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e) =>
        DropZone.BorderBrush = (Brush)FindResource("ControlStrokeColorDefaultBrush");

    // After the drop, so the other program is not held while the preview is shown.
    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        DropZone_DragLeave(sender, e);
        e.Handled = true;
        if (DroppedFiles(e) is { } paths)
        {
            _log.Info("Action", $"Files dropped on the drop zone ({paths.Length})");
            Dispatcher.BeginInvoke(async () => await ImportFiles(paths, under: null));
        }
    }
}
