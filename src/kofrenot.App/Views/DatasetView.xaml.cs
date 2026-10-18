using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.UI.Views;

/// <summary>
/// Shows a dataset version's file with its viewer (<see cref="DatasetType"/>), read and decoded off
/// the UI thread, in memory only: nothing decrypted is written to disk.
/// </summary>
public partial class DatasetView : UserControl
{
    private Guid? _shown;
    private int _loading;

    public DatasetView() => InitializeComponent();

    /// <summary>Raised when the view starts or stops showing a file, so the details give it room.</summary>
    public event Action? ContentChanged;

    public ILog Log { get; set; } = NullLog.Instance;

    /// <summary>An image or a text is shown.</summary>
    public bool IsShowing => ImageView.Visibility == Visibility.Visible || TextView.Visibility == Visibility.Visible;

    /// <summary>The text shown has the keyboard: the details leave its keys alone.</summary>
    public bool IsReading => TextView.IsKeyboardFocusWithin;

    /// <summary>Shows a version's file, unless it is shown already; null, or a file not synced yet, shows nothing.</summary>
    public void Show(DatasetFiles files, DatasetEdited? version, bool isAvailable)
    {
        if (version is not null && isAvailable && version.Id == _shown)
        {
            return;
        }

        Clear();
        if (version is null || !isAvailable)
        {
            return;
        }

        _shown = version.Id;
        Visibility = Visibility.Visible;
        if (DatasetType.WhyNotShown(version.File) is { } why)
        {
            MessageText.Text = why;
            return;
        }

        _ = Load(files, version);
    }

    /// <summary>Shows nothing, and forgets a file being read.</summary>
    public void Clear()
    {
        _loading++;
        _shown = null;
        var hadContent = IsShowing;
        ImageView.Source = null;
        TextView.Text = string.Empty;
        ImageView.Visibility = TextView.Visibility = Visibility.Collapsed;
        MessageText.Text = string.Empty;
        Visibility = Visibility.Collapsed;
        if (hadContent)
        {
            ContentChanged?.Invoke();
        }
    }

    private async Task Load(DatasetFiles files, DatasetEdited version)
    {
        var loading = ++_loading;
        var viewer = DatasetType.Of(version.File).Viewer;
        MessageText.Text = "Loading…";
        try
        {
            object content = viewer == DatasetViewer.Image
                ? await Task.Run(() => ReadImage(files, version.Id))
                : await Task.Run(() => ReadText(files, version.Id));
            if (loading != _loading)
            {
                return;
            }

            ShowContent(content);
            Log.Info("Action", $"Dataset viewed ({viewer})");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            if (loading == _loading)
            {
                Log.Warn("Action", $"Dataset cannot be shown ({ex.GetType().Name})");
                MessageText.Text = "This file cannot be shown here: export it to open it.";
            }
        }
    }

    private void ShowContent(object content)
    {
        if (content is BitmapSource image)
        {
            ImageView.Source = image;
            ImageView.Visibility = Visibility.Visible;
            MessageText.Text = $"{image.PixelWidth} × {image.PixelHeight} pixels";
        }
        else if (content is TextPreview text)
        {
            TextView.Text = text.Text;
            TextView.Visibility = Visibility.Visible;
            MessageText.Text = text.IsCut ? "Only the beginning is shown: export the file to see all of it." : string.Empty;
        }

        ContentChanged?.Invoke();
    }

    // Decoded in memory, then frozen to cross to the UI thread.
    private static BitmapSource ReadImage(DatasetFiles files, Guid version)
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

    private static TextPreview ReadText(DatasetFiles files, Guid version)
    {
        using var content = files.OpenRead(version);
        return TextPreview.Read(content);
    }
}
