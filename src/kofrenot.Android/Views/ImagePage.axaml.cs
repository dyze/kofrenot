using Avalonia.Controls;
using Avalonia.Interactivity;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// An image dataset's file, or an image field's, on a screen of its own (a tap on its thumbnail), as the image
/// viewer on Windows: downloaded first when it is not here, then zoomed and moved by hand (<see cref="ZoomImage"/>);
/// ⋮ opens it with another app, or shares it.
/// </summary>
public sealed partial class ImagePage : UserControl, IPage
{
    private readonly Shell _shell;
    private readonly DatasetEdited _image;

    public ImagePage(Shell shell, DatasetEdited image)
    {
        _shell = shell;
        _image = image;
        InitializeComponent();
        Bar.Title = image.File.Name;
        _shell.Run(LoadAsync);
    }

    public bool GoBack() => false;

    public void Refresh()
    {
    }

    public void SyncChanged()
    {
    }

    // Decrypted into memory, not into a file; decoded off the screen's thread.
    private async Task LoadAsync()
    {
        try
        {
            var bytes = new MemoryStream();
            if (!await _shell.ExportAsync(answers => _shell.Services.Session.ExportDataset(answers, _image, bytes)))
            {
                ShowMessage("The image could not be read.");
                return;
            }

            var data = bytes.ToArray();
            if (await Task.Run(() => ScreenImage.Of(data)) is not { } bitmap)
            {
                _shell.Services.Log.Warn("Image", "An image could not be decoded");
                ShowMessage("This file cannot be shown as an image: Open it with another app.");
                return;
            }

            Picture.Source = bitmap;
        }
        finally
        {
            Busy.IsVisible = false;
        }
    }

    private void ShowMessage(string text)
    {
        Message.Text = text;
        Message.IsVisible = true;
    }

    private void Actions_Click(object? sender, RoutedEventArgs e) =>
        _shell.ShowSheet(_image.File.Name,
        [
            new("Open", () => _shell.Run(() => _shell.HandAsync(_image, _shell.Services.Chooser.Open, "Open an image with another app")), Icon: "open"),
            new("Share…", () => _shell.Run(() => _shell.HandAsync(_image, _shell.Services.Chooser.Share, "Share an image")), Icon: "share"),
        ]);
}
