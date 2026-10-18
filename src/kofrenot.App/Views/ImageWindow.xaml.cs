using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.UI.Views;

/// <summary>
/// Shows a dataset version's image, read and decoded off the UI thread, in memory only: nothing
/// decrypted is written to disk. The image fits the window, never enlarged; the window is
/// resizable and remembers where it was left.
/// </summary>
public partial class ImageWindow : Window
{
    private int _loading;

    public ImageWindow() => InitializeComponent();

    public ILog Log { get; set; } = NullLog.Instance;

    /// <summary>The version shown, or null.</summary>
    public Guid? Version { get; private set; }

    /// <summary>Shows an image instead of the one shown, then brings the window to the front.</summary>
    public void Open(DatasetFiles files, DatasetEdited version)
    {
        Version = version.Id;
        Title = $"{version.File.Name} - kofrenot";
        Picture.Source = null;
        StatusText.Text = $"Loading… ({ByteSize.Format(version.File.Size)})";
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        _ = Load(files, version);
    }

    private async Task Load(DatasetFiles files, DatasetEdited version)
    {
        var loading = ++_loading;
        try
        {
            var image = await Task.Run(() => Read(files, version.Id));
            if (loading != _loading)
            {
                return;
            }

            Picture.Source = image;
            StatusText.Text = $"{image.PixelWidth} × {image.PixelHeight} pixels · {ByteSize.Format(version.File.Size)}";
            Log.Info("Action", "Dataset viewed (image)");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException)
        {
            if (loading == _loading)
            {
                Log.Warn("Action", $"Dataset image cannot be shown ({ex.GetType().Name})");
                StatusText.Text = "This image cannot be shown: export it to open it.";
            }
        }
    }

    // Decoded in memory, then frozen to cross to the UI thread.
    private static BitmapSource Read(DatasetFiles files, Guid version)
    {
        var bytes = new MemoryStream();
        using (var content = files.OpenRead(version))
        {
            content.CopyTo(bytes);
        }

        bytes.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = bytes;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}
