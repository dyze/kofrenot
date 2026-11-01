using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>A row of the list: an image dataset, its picture (its thumbnail), and the name of its latest file.</summary>
public sealed record ImageDatasetRow(ImageChoice Choice, NodeVisual Visual);

/// <summary>Chooses an image dataset of the vault for an image field (<see cref="ImagePicker.Images"/>).</summary>
public partial class ImageDatasetDialog : Window
{
    private readonly KnowledgeGraph _graph;

    private ImageDatasetDialog(Window owner, KnowledgeGraph graph, string fieldLabel)
    {
        InitializeComponent();
        Owner = owner;
        _graph = graph;
        Heading.Text = $"The image of {fieldLabel}";
        ShowImages();
        Loaded += (_, _) => SearchBox.Focus();
    }

    /// <summary>The image dataset chosen; null when cancelled.</summary>
    public static ImageChoice? Ask(Window owner, KnowledgeGraph graph, string fieldLabel)
    {
        var dialog = new ImageDatasetDialog(owner, graph, fieldLabel);
        return dialog.ShowDialog() == true ? (dialog.ImageList.SelectedItem as ImageDatasetRow)?.Choice : null;
    }

    private void ShowImages()
    {
        var rows = ImagePicker.Images(_graph, SearchBox.Text).Select(choice => new ImageDatasetRow(choice, NodeVisual.Of(_graph, choice.Node))).ToList();
        ImageList.ItemsSource = rows;
        ImageList.SelectedIndex = rows.Count > 0 ? 0 : -1;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ShowImages();

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && ImageList.Items.Count > 0)
        {
            (ImageList.ItemContainerGenerator.ContainerFromIndex(ImageList.SelectedIndex) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
    }

    private void ChooseButton_Click(object sender, RoutedEventArgs e)
    {
        if (ImageList.SelectedItem is not null)
        {
            DialogResult = true;
        }
    }

    private void ImageList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ChooseButton_Click(sender, e);

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
