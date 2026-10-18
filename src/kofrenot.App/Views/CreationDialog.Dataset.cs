using System.IO;
using System.Windows;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Input;
using Kofrenot.Core.Storage;
using Microsoft.Win32;

namespace Kofrenot.UI.Views;

/// <summary>A new dataset's step: its file, chosen or dropped, described off the UI thread.</summary>
public partial class CreationDialog
{
    private bool _reading;

    private void ShowFile(Side side)
    {
        FilePanel.Visibility = _form.IsDataset(side) ? Visibility.Visible : Visibility.Collapsed;
        var chosen = _form.FileOf(side);
        FileText.Text = _reading
            ? "Reading the file…"
            : chosen is null ? "No file yet." : $"{chosen.File.Name} · {ByteSize.Format(chosen.File.Size)}";
        ChooseFileButton.IsEnabled = !_reading;
        RemoveFileButton.Visibility = chosen is null || _reading ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task Choose(string path)
    {
        if (StepSide is not { } side || _reading)
        {
            return;
        }

        _reading = true;
        ShowForm();
        try
        {
            var file = await Task.Run(() => DatasetFiles.Describe(path));
            _form.SetFile(side, new ChosenFile(path, file));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageDialog.Inform(this, $"Cannot read the file.\n\n{ex.Message}");
        }
        finally
        {
            _reading = false;
        }

        // The step may have changed meanwhile (Back): show the current one.
        ShowForm();
    }

    private void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Choose the dataset's file", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true)
        {
            _ = Choose(dialog.FileName);
        }
    }

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if (StepSide is { } side)
        {
            _form.SetFile(side, null);
            ShowForm();
        }
    }

    private void FilePanel_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DroppedFile(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void FilePanel_Drop(object sender, DragEventArgs e)
    {
        if (DroppedFile(e) is { } path)
        {
            _ = Choose(path);
        }

        e.Handled = true;
    }

    /// <summary>The one file dropped, or null: none, several, or a folder.</summary>
    private string? DroppedFile(DragEventArgs e) =>
        !_reading && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths && File.Exists(paths[0]) ? paths[0] : null;
}
