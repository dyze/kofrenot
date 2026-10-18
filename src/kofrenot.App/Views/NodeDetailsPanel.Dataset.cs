using System.IO;
using System.Windows;
using System.Windows.Input;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.UI.Views;

/// <summary>The file of a dataset, next to its properties: Import, Export, drop in, drag out.</summary>
public partial class NodeDetailsPanel
{
    /// <summary>The files of the open vault's datasets; null when no vault is open.</summary>
    public Func<DatasetFiles?>? DatasetFiles { get; set; }

    /// <summary>Saves a dataset version, its file read from the stream opened (the progress told the bytes sealed); false when it could not.</summary>
    public Func<DatasetEdited, Func<Stream>, IProgress<long>?, Task<bool>>? SaveDataset
    {
        set => DatasetBox.Save = value;
    }

    /// <summary>Exports a dataset version's file to a path; false when it could not.</summary>
    public Func<DatasetEdited, string, Task<bool>>? ExportDataset
    {
        set => DatasetBox.Export = value;
    }

    /// <summary>Shows a dataset version's image in its own window.</summary>
    public Action<DatasetEdited>? ShowImage
    {
        set => DatasetBox.ShowImage = value;
    }

    /// <summary>A file is being imported into this dataset.</summary>
    public Func<Guid, bool> IsImporting
    {
        set => DatasetBox.IsImporting = value;
    }

    private void ShowDataset(NodeDetails details)
    {
        var files = details.Node.Type == TypeCatalog.Dataset.Id ? DatasetFiles?.Invoke() : null;
        DatasetBox.Visibility = files is null ? Visibility.Collapsed : Visibility.Visible;
        ContentColumn.Width = files is null ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        if (files is null)
        {
            DatasetBox.Close();
        }
        else if (DatasetBox.Node?.Id == details.Node.Id)
        {
            DatasetBox.Refresh();
        }
        else
        {
            DatasetBox.Log = Log;
            DatasetBox.Open(Browser.Graph!, details.Node, files);
            Log.Info("Action", "Dataset opened");
        }

        FitDatasetRow();
    }

    /// <summary>The properties row grows only while the viewer shows a text, as a note's editor does.</summary>
    private void FitDatasetRow() =>
        TopRow.Height = DatasetBox.IsViewing ? new GridLength(2, GridUnitType.Star) : GridLength.Auto;

    /// <summary>
    /// Ctrl+I imports a file, Ctrl+E exports it, while a dataset is shown. In the text shown, Esc
    /// leaves it and the other keys are its own.
    /// </summary>
    private bool HandleDatasetKey(Key key)
    {
        if (DatasetBox.IsReading && key == Key.Escape)
        {
            Focus();
            return true;
        }

        if (DatasetBox.Node is null || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return false;
        }

        switch (key)
        {
            case Key.I:
                DatasetBox.ChooseImport();
                return true;
            case Key.E:
                DatasetBox.ChooseExport();
                return true;
            default:
                return false;
        }
    }
}
