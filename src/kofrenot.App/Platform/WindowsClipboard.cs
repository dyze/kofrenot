using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Platform;

/// <summary>
/// Windows' clipboard, read only: told when it changes by a message-only window
/// (<c>AddClipboardFormatListener</c>), what it holds kept until then. An image is read as PNG:
/// its own when it has one (transparency kept), else its bitmap encoded so.
/// </summary>
public sealed class WindowsClipboard : IClipboard, IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private static readonly IntPtr MessageOnly = new(-3);

    private readonly HwndSource _source;
    private readonly ILog _log;

    public WindowsClipboard(ILog log)
    {
        _log = log;
        _source = new HwndSource(new HwndSourceParameters("kofrenot clipboard") { ParentWindow = MessageOnly, WindowStyle = 0 });
        _source.AddHook(Hook);
        if (!AddClipboardFormatListener(_source.Handle))
        {
            _log.Warn("App", $"Cannot follow the clipboard (error {Marshal.GetLastWin32Error()}): Paste buttons show what it held at start");
        }

        Content = Read();
    }

    public event Action? Changed;

    public ClipboardContent Content { get; private set; }

    public ImportFile? ReadImage()
    {
        try
        {
            var png = Clipboard.GetData("PNG") is MemoryStream own ? own.ToArray() : EncodedBitmap();
            return png is null ? null : new ImportFile(PasteOffer.ImageNameOf(DateTime.Now), png.Length, () => new MemoryStream(png, writable: false));
        }
        catch (ExternalException ex)
        {
            _log.Warn("App", $"Cannot read the clipboard's image ({ex.GetType().Name})");
            return null;
        }
    }

    public void Dispose()
    {
        RemoveClipboardFormatListener(_source.Handle);
        _source.RemoveHook(Hook);
        _source.Dispose();
    }

    private static byte[]? EncodedBitmap()
    {
        if (Clipboard.GetImage() is not { } bitmap)
        {
            return null;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream();
        encoder.Save(png);
        return png.ToArray();
    }

    // Another program may hold the clipboard open for a moment: nothing to paste until it changes again.
    private ClipboardContent Read()
    {
        try
        {
            var paths = Clipboard.ContainsFileDropList() ? Clipboard.GetFileDropList().Cast<string>().ToList() : [];
            return new ClipboardContent(paths, Clipboard.ContainsImage() || Clipboard.ContainsData("PNG"));
        }
        catch (ExternalException ex)
        {
            _log.Debug("App", $"Cannot read the clipboard ({ex.GetType().Name})");
            return ClipboardContent.Empty;
        }
    }

    private IntPtr Hook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmClipboardUpdate)
        {
            Content = Read();
            Changed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr window);
}
