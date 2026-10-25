using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Storage;

namespace Kofrenot.UI.Views;

/// <summary>
/// Shows a dataset version's image, read and decoded off the UI thread, in memory only: nothing
/// decrypted is written to disk. It opens fitted to the window (never enlarged) and follows its
/// size until zoomed (<see cref="ImageZoom"/>): the wheel zooms around the pointer, +/- around the
/// middle, 0 fits again; a larger image is moved by dragging it with the left button. The window
/// is resizable and remembers where it was left.
/// </summary>
public partial class ImageWindow : Window
{
    private int _loading;
    private DatasetEdited? _version;
    private double _zoom = 1;
    private bool _fitting = true;
    private (Point Start, Vector Offset)? _panFrom;

    public ImageWindow() => InitializeComponent();

    public ILog Log { get; set; } = NullLog.Instance;

    private BitmapSource? Image => Picture.Source as BitmapSource;

    /// <summary>Shows an image instead of the one shown, then brings the window to the front.</summary>
    public void Open(Vault vault, DatasetEdited version)
    {
        _version = version;
        Title = $"{version.File.Name} - kofrenot";
        Picture.Source = null;
        StatusText.Text = $"Loading… ({ByteSize.Format(version.File.Size)})";
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        _ = Load(vault, version);
    }

    private async Task Load(Vault vault, DatasetEdited version)
    {
        var loading = ++_loading;
        if (!vault.Datasets.Exists(version.Id))
        {
            StatusText.Text = ContentState.Downloading.Describe("this image");
        }

        if (!await vault.FetchAsync(version, retry: true))
        {
            if (loading == _loading)
            {
                StatusText.Text = vault.StateOf(version.Id).Describe("this image");
            }

            return;
        }

        try
        {
            var image = await Task.Run(() => Read(vault.Datasets, version.Id));
            if (loading != _loading)
            {
                return;
            }

            Picture.Source = image;
            Fit();
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

    /// <summary>The whole image in the window, at most one image pixel per screen pixel; it then follows the window's size.</summary>
    private void Fit()
    {
        if (Image is not { } image)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        _fitting = true;
        ZoomTo(ImageZoom.Fit(Scroller.ActualWidth * dpi.DpiScaleX, Scroller.ActualHeight * dpi.DpiScaleY, image.PixelWidth, image.PixelHeight), at: null);
    }

    /// <summary>Zooms, keeping the image point under <paramref name="at"/> (in the scroller; null: its middle) in place.</summary>
    private void ZoomTo(double zoom, Point? at)
    {
        if (Image is not { } image)
        {
            return;
        }

        var anchor = at ?? new Point(Scroller.ViewportWidth / 2, Scroller.ViewportHeight / 2);
        var before = Scroller.TranslatePoint(anchor, Picture);
        var (ratioX, ratioY) = Picture.ActualWidth > 0 ? (before.X / Picture.ActualWidth, before.Y / Picture.ActualHeight) : (0.5, 0.5);

        // One image pixel per screen pixel at zoom 1, whatever the screen's scaling.
        var dpi = VisualTreeHelper.GetDpi(this);
        _zoom = zoom;
        Picture.Width = image.PixelWidth * zoom / dpi.DpiScaleX;
        Picture.Height = image.PixelHeight * zoom / dpi.DpiScaleY;
        Scroller.UpdateLayout();

        var after = Picture.TranslatePoint(new Point(ratioX * Picture.ActualWidth, ratioY * Picture.ActualHeight), Scroller);
        Scroller.ScrollToHorizontalOffset(Scroller.HorizontalOffset + after.X - anchor.X);
        Scroller.ScrollToVerticalOffset(Scroller.VerticalOffset + after.Y - anchor.Y);
        ShowStatus(image);
    }

    private void Step(bool zoomIn, Point? at)
    {
        _fitting = false;
        ZoomTo(zoomIn ? ImageZoom.In(_zoom) : ImageZoom.Out(_zoom), at);
    }

    private void ShowStatus(BitmapSource image) =>
        StatusText.Text = $"{image.PixelWidth} × {image.PixelHeight} pixels · {ByteSize.Format(_version!.File.Size)} · "
            + $"{ImageZoom.Percent(_zoom)}{(_fitting ? " (fitted)" : string.Empty)}. Wheel or +/- zooms, 0 fits.";

    private void Scroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        Step(zoomIn: e.Delta > 0, e.GetPosition(Scroller));
        e.Handled = true;
    }

    /// <summary>The image is larger than the window: it can be dragged.</summary>
    private bool CanPan => Scroller.ScrollableWidth > 0 || Scroller.ScrollableHeight > 0;

    // A drag with the left button moves the image: the point grabbed follows the pointer.
    private void Scroller_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!CanPan || IsOnScrollBar(e))
        {
            return;
        }

        _panFrom = (e.GetPosition(Scroller), new Vector(Scroller.HorizontalOffset, Scroller.VerticalOffset));
        Scroller.CaptureMouse();
        Scroller.Cursor = Cursors.ScrollAll;
        e.Handled = true;
    }

    private void Scroller_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_panFrom is not var (start, offset))
        {
            return;
        }

        var moved = e.GetPosition(Scroller) - start;
        Scroller.ScrollToHorizontalOffset(offset.X - moved.X);
        Scroller.ScrollToVerticalOffset(offset.Y - moved.Y);
        e.Handled = true;
    }

    private void Scroller_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_panFrom is not null)
        {
            Scroller.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void Scroller_LostMouseCapture(object sender, MouseEventArgs e)
    {
        _panFrom = null;
        ShowPanCursor();
    }

    // The hand says the image can be moved; it follows the zoom and the window's size.
    private void Scroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_panFrom is null)
        {
            ShowPanCursor();
        }
    }

    private void ShowPanCursor() => Scroller.Cursor = CanPan ? Cursors.Hand : null;

    // A press on a scroll bar (its thumb or arrows) is the scroll bar's.
    private bool IsOnScrollBar(MouseButtonEventArgs e)
    {
        var point = e.GetPosition(Scroller);
        return point.X > Scroller.ViewportWidth || point.Y > Scroller.ViewportHeight;
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_fitting)
        {
            Fit();
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                break;
            case Key.Add or Key.OemPlus:
                Step(zoomIn: true, at: null);
                break;
            case Key.Subtract or Key.OemMinus:
                Step(zoomIn: false, at: null);
                break;
            case Key.D0 or Key.NumPad0:
                Fit();
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
