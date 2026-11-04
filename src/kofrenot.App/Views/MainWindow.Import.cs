using System.Windows;
using System.Windows.Media;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// Files secured quickly: dropped on the drop zone (or several on a tree), or pasted by it (the
/// files or the image copied), they become datasets, named after their files, once their preview is accepted.
/// </summary>
public partial class MainWindow
{
    private const string NothingToPaste = "The clipboard holds no file nor image any more.";

    /// <summary>
    /// Shows the preview of importing <paramref name="paths"/>, then imports them when accepted:
    /// under <paramref name="under"/> when dropped on a node, else to this vault's last destination
    /// (<see cref="ImportDestinations"/>), which the preview can change, and which is kept for the next one.
    /// </summary>
    private Task ImportFiles(IReadOnlyList<string> paths, ParentLink? under) =>
        Import((planner, parent) => planner.Preview(paths, parent), under, choosesDestination: under is null);

    /// <summary>What was pasted in a view: under <paramref name="under"/>, or with no parent; the datasets imported.</summary>
    private Task<IReadOnlyList<Node>> ImportPasted(Pasted pasted, ParentLink? under) =>
        Import(pasted.Preview, under, choosesDestination: false);

    private async Task<IReadOnlyList<Node>> Import(Func<FileImportPlanner, ParentLink?, ImportPreview> previewOf, ParentLink? under, bool choosesDestination)
    {
        if (_session.Current is not { } vault)
        {
            return [];
        }

        var planner = new FileImportPlanner(vault.Graph);
        var choice = choosesDestination
            ? ImportPreviewDialog.Ask(this, vault.Graph, _destinations.Of(vault.Id), parent => previewOf(planner, parent))
            : ImportPreviewDialog.Ask(this, previewOf(planner, under));
        if (choice is null || !ConfirmUpdates(choice.Preview))
        {
            _log.Info("Action", "Import of files cancelled");
            return [];
        }

        if (choosesDestination)
        {
            _destinations.Set(vault.Id, choice.Destination);
        }

        return await _session.ImportFiles(Ask, choice.Preview);
    }

    private void PasteFiles_Click(object sender, RoutedEventArgs e)
    {
        _log.Info("Action", "Paste of files by the drop zone");
        if (PasteOffer.Read(_clipboard) is { } pasted)
        {
            _ = Import(pasted.Preview, under: null, choosesDestination: true);
        }
        else
        {
            MessageDialog.Inform(this, NothingToPaste);
        }
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
