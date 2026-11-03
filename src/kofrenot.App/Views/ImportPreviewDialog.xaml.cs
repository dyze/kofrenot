using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>A row of the preview: the dataset a file becomes, or the node a folder becomes, indented under its folder.</summary>
public sealed record ImportRowView(ImportRow Row)
{
    private const double IndentPerFolder = 16;

    public string Name => Row.NodeName;

    public Thickness Indent => new(Row.Depth * IndentPerFolder, 0, 0, 0);

    public string Type => Row.TypeName;

    public string Size => Row.IsFolder ? string.Empty : ByteSize.Format(Row.Size);

    /// <summary>"update", "existing", "homonym", or nothing for a new node of its own name.</summary>
    public string Tag => Row switch
    {
        { IsUpdate: true } => "update",
        { Existing: not null } => "existing",
        { IsHomonym: true } => "homonym",
        _ => string.Empty,
    };

    public string TagTip => Row switch
    {
        { IsUpdate: true } => "This dataset is already here: the file becomes its new version, the earlier ones kept",
        { Existing: not null } => "This node is already here: the folder's content goes under it",
        _ => "A node already has this name here: another one is added",
    };
}

/// <summary>An import accepted: what it adds, and where it goes when it was chosen here (null: no parent, or a node dropped on).</summary>
public sealed record ImportChoice(ImportPreview Preview, ImportDestination? Destination);

/// <summary>
/// Shows what an import adds (<see cref="ImportPreview"/>): nothing is added before Add. Files not
/// dropped on a node go to a destination, changed here (<see cref="ImportDestinationDialog"/>), the
/// preview made again for it.
/// </summary>
public partial class ImportPreviewDialog : Window
{
    private readonly KnowledgeGraph? _graph;
    private readonly Func<ParentLink?, ImportPreview>? _previewOf;
    private ImportPreview _preview;
    private ImportDestination? _destination;

    private ImportPreviewDialog(Window owner, ImportPreview preview, KnowledgeGraph? graph = null, ImportDestination? destination = null, Func<ParentLink?, ImportPreview>? previewOf = null)
    {
        InitializeComponent();
        Owner = owner;
        _graph = graph;
        _previewOf = previewOf;
        _preview = preview;
        _destination = destination;
        DestinationBar.Visibility = previewOf is null ? Visibility.Collapsed : Visibility.Visible;
        ShowPreview(preview);
    }

    /// <summary>The import of items dropped on a node, accepted; null when cancelled.</summary>
    public static ImportChoice? Ask(Window owner, ImportPreview preview) => Accepted(new ImportPreviewDialog(owner, preview));

    /// <summary>
    /// The import of items to <paramref name="destination"/> (none: no parent), which can be changed
    /// here, accepted with the destination chosen; null when cancelled.
    /// </summary>
    public static ImportChoice? Ask(Window owner, KnowledgeGraph graph, ImportDestination? destination, Func<ParentLink?, ImportPreview> previewOf) =>
        Accepted(new ImportPreviewDialog(owner, previewOf(ImportDestinations.Resolve(graph, destination)?.Under), graph, destination, previewOf));

    private static ImportChoice? Accepted(ImportPreviewDialog dialog) =>
        dialog.ShowDialog() == true ? new ImportChoice(dialog._preview, dialog._destination) : null;

    private void ShowPreview(ImportPreview preview)
    {
        _preview = preview;
        HeadingText.Text = preview.Heading;
        RowsList.ItemsSource = preview.Rows.Select(row => new ImportRowView(row)).ToList();
        if (_previewOf is not null)
        {
            var resolved = ImportDestinations.Resolve(_graph!, _destination);
            DestinationText.Text = resolved is null ? "Into: no parent (listed as unlinked)" : $"Into: {resolved.Label}";
            NoDestinationButton.Visibility = resolved is null ? Visibility.Collapsed : Visibility.Visible;
        }
        else if (preview.Under is { } under)
        {
            UnderText.Text = preview.FolderCount > 0
                ? $"Under “{under.Parent.Name}”: each file as a dataset, each folder as a node holding its content."
                : $"Each as a dataset under “{under.Parent.Name}”.";
            UnderText.Visibility = Visibility.Visible;
        }

        SkippedText.Text = $"Skipped (cannot be read, or a folder link): {string.Join(", ", preview.Skipped)}";
        SkippedText.Visibility = preview.Skipped.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        AddButton.IsEnabled = preview.Count > 0;
    }

    private void ChangeDestination_Click(object sender, RoutedEventArgs e)
    {
        if (ImportDestinationDialog.Ask(this, _graph!, _destination) is { } chosen)
        {
            ShowDestination(chosen);
        }
    }

    private void NoDestination_Click(object sender, RoutedEventArgs e) => ShowDestination(null);

    private void ShowDestination(ImportDestination? destination)
    {
        _destination = destination;
        ShowPreview(_previewOf!(ImportDestinations.Resolve(_graph!, destination)?.Under));
    }

    private void AddButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
