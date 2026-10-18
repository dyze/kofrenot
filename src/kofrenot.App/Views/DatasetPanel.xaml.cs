using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;
using Kofrenot.UI.Platform;
using Microsoft.Win32;

namespace Kofrenot.UI.Views;

/// <summary>A version offered in a conflict: the button shows its file and when it was imported.</summary>
public sealed record DatasetConflictChoice(DatasetChoice Choice)
{
    public string Label => $"{Choice.Version.File.Name}, {ByteSize.Format(Choice.Version.File.Size)}, {DatasetPanel.DateOf(Choice.Version)}"
        + (CanKeep ? string.Empty : " (not synced yet)");

    public bool CanKeep => Choice.IsAvailable;
}

/// <summary>
/// The file of the dataset shown in the details (a view of <see cref="DatasetPlanner"/>): its
/// versions' metadata is read from the graph, and the file itself only when exported.
/// </summary>
public partial class DatasetPanel : UserControl
{
    private KnowledgeGraph? _graph;
    private Node? _node;
    private DatasetFiles? _files;
    private DatasetState? _state;
    private string? _work;
    private Point? _pressedAt;
    private bool _draggingOut;

    public DatasetPanel()
    {
        InitializeComponent();
        View.ContentChanged += () => ViewChanged?.Invoke();
    }

    /// <summary>Raised when the viewer starts or stops showing the file, so the details give it room.</summary>
    public event Action? ViewChanged;

    /// <summary>The file is shown by its viewer.</summary>
    public bool IsViewing => View.IsShowing;

    /// <summary>The text shown has the keyboard: the details leave its keys alone.</summary>
    public bool IsReading => View.IsReading;

    /// <summary>Saves a version, its file read from the stream opened; false when it could not.</summary>
    public Func<DatasetEdited, Func<Stream>, IProgress<long>?, Task<bool>>? Save { get; set; }

    /// <summary>Exports a version's file to a path; false when it could not.</summary>
    public Func<DatasetEdited, string, Task<bool>>? Export { get; set; }

    /// <summary>A file is being imported into this dataset (here, or from the creation screen).</summary>
    public Func<Guid, bool> IsImporting { get; set; } = _ => false;

    /// <summary>Shows a version's image in its own window.</summary>
    public Action<DatasetEdited>? ShowImage { get; set; }

    public ILog Log
    {
        get => View.Log;
        set => View.Log = value;
    }

    /// <summary>The dataset shown, or null.</summary>
    public Node? Node => _node;

    private bool IsBusy => _work is not null || (_node is not null && IsImporting(_node.Id));

    private DatasetEdited? Exportable => _state is { IsMissing: false, Latest: { } latest } && !IsBusy ? latest : null;

    /// <summary>The latest version, when it is an image the image window shows (synced, not too large).</summary>
    private DatasetEdited? Viewable =>
        Exportable is { } latest && DatasetType.Of(latest.File).Viewer == DatasetViewer.Image && DatasetType.WhyNotShown(latest.File) is null
            ? latest
            : null;

    private void ViewImage()
    {
        if (Viewable is { } latest)
        {
            ShowImage?.Invoke(latest);
        }
    }

    public static string DateOf(DatasetEdited version) =>
        version.At.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);

    /// <summary>Shows a dataset: its versions, not its file.</summary>
    public void Open(KnowledgeGraph graph, Node node, DatasetFiles files)
    {
        (_graph, _node, _files) = (graph, node, files);
        Load();
    }

    /// <summary>The graph changed (an import, or changes from elsewhere): reads the versions again.</summary>
    public void Refresh()
    {
        if (_node is not null)
        {
            Load();
        }
    }

    /// <summary>Forgets the dataset (another node shown, or the vault closed). An import under way still ends.</summary>
    public void Close()
    {
        (_graph, _node, _files, _state) = (null, null, null, null);
        View.Clear();
    }

    /// <summary>Asks for a file to import (Ctrl+I).</summary>
    public void ChooseImport()
    {
        if (_node is null || IsBusy)
        {
            return;
        }

        var dialog = new OpenFileDialog { Title = "Import a file", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            _ = ImportFrom(dialog.FileName);
        }
    }

    /// <summary>Asks where to save the file (Ctrl+E).</summary>
    public void ChooseExport()
    {
        if (Exportable is not { } latest)
        {
            return;
        }

        var dialog = new SaveFileDialog { Title = "Export the file", FileName = latest.File.SafeName, Filter = "All files|*.*" };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            _ = ExportTo(latest, dialog.FileName);
        }
    }

    private void Load()
    {
        _state = new DatasetPlanner(_graph!).Load(_node!, _files!.Exists);
        ShowState();
        View.Show(_files, _state.Latest, !_state.IsMissing);
    }

    private void ShowState()
    {
        var latest = _state?.Latest;
        HeaderVisual.Visual = _graph is null || _node is null ? null : NodeVisual.Of(_graph, _node);
        FileNameText.Text = latest?.File.Name ?? "No file yet";
        FileInfoText.Text = latest is null ? string.Empty : $"{DatasetType.Of(latest.File).Name} · {ByteSize.Format(latest.File.Size)} · imported {DateOf(latest)}";
        ImportButton.IsEnabled = !IsBusy;
        ExportButton.IsEnabled = Exportable is not null;
        ViewButton.Visibility = latest is not null && DatasetType.Of(latest.File).Viewer == DatasetViewer.Image ? Visibility.Visible : Visibility.Collapsed;
        ViewButton.IsEnabled = Viewable is not null;
        ConflictVersions.ItemsSource = _state?.Conflict.Select(choice => new DatasetConflictChoice(choice)).ToList();
        ConflictPanel.Visibility = _state?.Conflict.Count > 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = _state switch
        {
            _ when _work is not null => _work,
            _ when IsBusy => "Importing…",
            { IsMissing: true } => "The file of this dataset is not synced to this computer yet.",
            { Latest: null } => "Drop a file here, or Import, to add it.",
            _ => "Drop a file here to import a new version. Drag the file out, or Export, to save it elsewhere.",
        };
    }

    /// <summary>Describes the file off the UI thread, then saves it as a new version unless it is the latest already.</summary>
    private async Task ImportFrom(string path)
    {
        var (graph, node, files) = (_graph!, _node!, _files!);
        const string Reading = "Reading the file…";
        const string Importing = "Importing…";
        await Run(Reading, async () =>
        {
            var size = new FileInfo(path).Length;
            var file = await Task.Run(() => DatasetFiles.Describe(path, ProgressOf(Reading, size)));
            var state = new DatasetPlanner(graph).Load(node, files.Exists);
            if (new DatasetPlanner(graph).Import(node, file, state) is not { } version)
            {
                Log.Info("Action", "Dataset import: the same file, nothing imported");
                return "This file is the dataset's already: nothing imported.";
            }

            _work = Importing;
            ShowState();
            return await Save!(version, () => File.OpenRead(path), ProgressOf(Importing, file.Size)) ? null : "The file was not imported.";
        });
    }

    /// <summary>
    /// Shows how far a step of the work is, in percent of <paramref name="total"/> bytes. Reports
    /// arrive later on the UI thread: one arriving after its step is dropped.
    /// </summary>
    private Progress<long> ProgressOf(string working, long total) => new(done =>
    {
        if (_work?.StartsWith(working, StringComparison.Ordinal) == true)
        {
            _work = $"{working} {ByteSize.Percent(done, total)} %";
            StatusText.Text = _work;
        }
    });

    private async Task ExportTo(DatasetEdited version, string target) =>
        await Run("Exporting…", async () => await Export!(version, target) ? null : "The file was not exported.");

    private async Task Keep(DatasetEdited kept)
    {
        var (graph, node, files, state) = (_graph!, _node!, _files!, _state!);
        var version = new DatasetPlanner(graph).Keep(node, kept, state);
        const string Keeping = "Keeping this version…";
        await Run(Keeping, async () => await Save!(version, () => files.OpenRead(kept.Id), ProgressOf(Keeping, kept.File.Size)) ? null : "The version was not kept.");
    }

    /// <summary>Runs some work, the panel busy meanwhile; the work returns a message to leave in the status, or null.</summary>
    private async Task Run(string working, Func<Task<string?>> work)
    {
        var node = _node;
        _work = working;
        ShowState();
        string? message;
        try
        {
            message = await work();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The message names the file: shown, not logged.
            Log.Warn("Action", $"Dataset: the file cannot be read ({ex.GetType().Name})");
            message = $"Cannot read the file: {ex.Message}";
        }

        _work = null;
        if (_node is not null && _node.Id == node?.Id)
        {
            Load();
            if (message is not null)
            {
                StatusText.Text = message;
            }
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e) => ChooseImport();

    private void Export_Click(object sender, RoutedEventArgs e) => ChooseExport();

    private void View_Click(object sender, RoutedEventArgs e) => ViewImage();

    private void KeepVersion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DatasetConflictChoice { CanKeep: true } choice } && !IsBusy)
        {
            _ = Keep(choice.Choice.Version);
        }
    }

    private void Zone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DroppedFile(e) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Zone_Drop(object sender, DragEventArgs e)
    {
        if (DroppedFile(e) is { } path)
        {
            Log.Info("Action", "Dataset: a file dropped to import");
            _ = ImportFrom(path);
        }

        e.Handled = true;
    }

    /// <summary>The one file dragged over the panel, or null: none, several, a folder, the panel busy, or its own file.</summary>
    private string? DroppedFile(DragEventArgs e) =>
        _node is not null && !IsBusy && !_draggingOut
        && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } paths && File.Exists(paths[0])
            ? paths[0]
            : null;

    /// <summary>A press may start a drag out; a double-click views an image.</summary>
    private void FileRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressedAt = e.GetPosition(this);
        if (e.ClickCount == 2)
        {
            _pressedAt = null;
            ViewImage();
        }
    }

    /// <summary>Dragging the file out exports it first (the cursor waits), then lets Windows drop it.</summary>
    private void FileRow_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _pressedAt is not { } start || Exportable is not { } latest)
        {
            _pressedAt = null;
            return;
        }

        var moved = e.GetPosition(this) - start;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _pressedAt = null;
        DragOut(latest);
    }

    private void DragOut(DatasetEdited latest)
    {
        var files = _files!;
        _draggingOut = true;
        try
        {
            var dropped = DragExport.Drag(FileRow, latest.File.SafeName, path =>
            {
                // The export holds the UI thread (Windows needs the file before the drag starts):
                // the message is drawn first, so a large file does not look frozen.
                Mouse.OverrideCursor = Cursors.Wait;
                StatusText.Text = $"Preparing the file to drag ({ByteSize.Format(latest.File.Size)})…";
                Dispatcher.Invoke(DispatcherPriority.Render, () => { });
                try
                {
                    files.Export(latest.Id, path);
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            });
            Log.Info("Action", dropped ? $"Dataset dragged out ({latest.File.Size} bytes)" : "Dataset drag cancelled");
            ShowState();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Warn("Action", $"Cannot export the dataset's file ({ex.GetType().Name})");
            StatusText.Text = $"Cannot export the file: {ex.Message}";
        }
        finally
        {
            _draggingOut = false;
        }
    }
}
