using System.Windows;
using System.Windows.Interop;
using Kofrenot.Core.Datasets;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>A row of the preview: the dataset a file becomes.</summary>
public sealed record ImportRowView(ImportRow Row)
{
    public string Name => Row.NodeName;

    public string Type => Row.Type.Name;

    public string Size => ByteSize.Format(Row.File.Size);

    public string Homonym => Row.IsHomonym ? "homonym" : string.Empty;
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
            UnderText.Text = $"Each as a dataset under “{under.Parent.Name}”.";
            UnderText.Visibility = Visibility.Visible;
        }

        if (preview.Skipped.Count > 0)
        {
            SkippedText.Text = $"Skipped (folders, or files that cannot be read): {string.Join(", ", preview.Skipped)}";
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
