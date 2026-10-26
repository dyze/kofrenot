using System.Windows;
using System.Windows.Media;
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
    /// <summary>Shows the preview of importing <paramref name="paths"/>, then imports them when accepted.</summary>
    private async Task ImportFiles(IReadOnlyList<string> paths, ParentLink? under)
    {
        if (_session.Current is not { } vault)
        {
            return;
        }

        var preview = new FileImportPlanner(vault.Graph).Preview(paths, under);
        if (!ImportPreviewDialog.Ask(this, preview))
        {
            _log.Info("Action", "Import of files cancelled");
            return;
        }

        await _session.ImportFiles(Ask, preview);
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
