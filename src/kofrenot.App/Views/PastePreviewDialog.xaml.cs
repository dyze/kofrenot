using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Kofrenot.Core.Datasets;
using Kofrenot.UI.Platform;

namespace Kofrenot.UI.Views;

/// <summary>A file pasted from the clipboard, shown before it is: its picture when it is an image, its name and size.</summary>
public partial class PastePreviewDialog : Window
{
    private const int PictureHeight = 240;

    private PastePreviewDialog(Window? owner, string question, string yes, ImportFile file)
    {
        InitializeComponent();
        Owner = owner;
        QuestionText.Text = question;
        PasteButton.Content = yes;
        FileText.Text = $"{file.Name} · {ByteSize.Format(file.Size)}";
        if (DatasetType.Of(file.Name).Viewer == DatasetViewer.Image && PictureOf(file) is { } picture)
        {
            Picture.Source = picture;
            PictureFrame.Visibility = Visibility.Visible;
        }

        Loaded += (_, _) => PasteButton.Focus();
    }

    /// <summary>Asks whether to paste <paramref name="file"/>, as <paramref name="question"/> says: true when the user chose <paramref name="yes"/>.</summary>
    public static bool Confirm(Window? owner, string question, string yes, ImportFile file) =>
        new PastePreviewDialog(owner, question, yes, file).ShowDialog() == true;

    // An image WPF cannot decode is still pasted as a file: only its picture is missing here.
    private static BitmapImage? PictureOf(ImportFile file)
    {
        try
        {
            using var content = file.Open();
            var picture = new BitmapImage();
            picture.BeginInit();
            picture.CacheOption = BitmapCacheOption.OnLoad;
            picture.DecodePixelHeight = PictureHeight;
            picture.StreamSource = content;
            picture.EndInit();
            picture.Freeze();
            return picture;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or InvalidOperationException)
        {
            return null;
        }
    }

    private void PasteButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Window_SourceInitialized(object? sender, EventArgs e) =>
        NativeWindows.RemoveMinimizeMaximizeButtons(new WindowInteropHelper(this).Handle);
}
