using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core.Datasets;
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

/// <summary>Shows what an import adds (<see cref="ImportPreview"/>): nothing is added before Add.</summary>
public partial class ImportPreviewDialog : Window
{
    private ImportPreviewDialog(Window owner, ImportPreview preview)
    {
        InitializeComponent();
        Owner = owner;
        HeadingText.Text = preview.Heading;
        RowsList.ItemsSource = preview.Rows.Select(row => new ImportRowView(row)).ToList();
        if (preview.Under is { } under)
        {
            UnderText.Text = preview.FolderCount > 0
                ? $"Under “{under.Parent.Name}”: each file as a dataset, each folder as a node holding its content."
                : $"Each as a dataset under “{under.Parent.Name}”.";
            UnderText.Visibility = Visibility.Visible;
        }

        if (preview.Skipped.Count > 0)
        {
            SkippedText.Text = $"Skipped (cannot be read, or a folder link): {string.Join(", ", preview.Skipped)}";
            SkippedText.Visibility = Visibility.Visible;
        }

        AddButton.IsEnabled = preview.Count > 0;
    }

    /// <summary>True when the import is accepted.</summary>
    public static bool Ask(Window owner, ImportPreview preview) => new ImportPreviewDialog(owner, preview).ShowDialog() == true;

    private void AddButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
