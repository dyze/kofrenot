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
    /// <summary>The open vault, whose datasets' files are read (and downloaded when needed); null when none is open.</summary>
    public Func<Vault?>? OpenVault { get; set; }

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

    /// <summary>Opens a dataset version's file with its app; returns why it could not, or null.</summary>
    public Func<DatasetEdited, Task<string?>>? OpenDataset
    {
        set => DatasetBox.OpenInApp = value;
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
        var vault = details.Node.Type == TypeCatalog.Dataset.Id ? OpenVault?.Invoke() : null;
        DatasetBox.Visibility = vault is null ? Visibility.Collapsed : Visibility.Visible;
        ContentColumn.Width = vault is null ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        if (vault is null)
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
            DatasetBox.Open(Browser.Graph!, details.Node, vault);
            Log.Info("Action", "Dataset opened");
        }

        FitDatasetRow();
    }

    /// <summary>The properties row grows only while the viewer shows a text, as a note's editor does.</summary>
    private void FitDatasetRow() =>
        TopRow.Height = DatasetBox.IsViewing ? new GridLength(2, GridUnitType.Star) : GridLength.Auto;

    /// <summary>
    /// Ctrl+I imports a file, Ctrl+E exports it, Ctrl+O opens it in its app, while a dataset is shown. In the text shown, Esc
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
            case Key.O:
                DatasetBox.OpenFile();
                return true;
            default:
                return false;
        }
    }
}
