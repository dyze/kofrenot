using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// A dataset's file on its page, named as on Windows: its thumbnail for an image, **Open** (with the
/// app the user picks), **Export…** (where the user chooses), and the phone's own **Share…** (Android's
/// share sheet). The file is downloaded first when it is not here; Open and Share decrypt it into the
/// cache (cache/open/, deleted at the next start). An image field's **Open** does as a file's.
/// </summary>
public sealed partial class NodePage
{
    // Its latest version's; a thumbnail not here yet shows once the page is shown again after it came.
    private async void ShowThumbnail(DatasetEdited version)
    {
        if (!DatasetType.HasThumbnail(version.File) || await ThumbnailCache.Shared.Get(version.Id) is not { } image)
        {
            return;
        }

        Thumbnail.Source = image;
        ThumbnailFrame.IsVisible = Files.IsVisible;
    }

    private void Open_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is FileRow row)
        {
            _shell.Run(() => HandAsync(row.Version, _shell.Services.Chooser.Open, "Open a dataset's file with another app"));
        }
    }

    private void OpenImage_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is FieldLine { Image: { } image })
        {
            _shell.Run(() => HandAsync(image, _shell.Services.Chooser.Open, "Open an image field with another app"));
        }
    }

    private void Share_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is FileRow row)
        {
            _shell.Run(() => HandAsync(row.Version, _shell.Services.Chooser.Share, "Share a dataset's file"));
        }
    }

    private void Export_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is FileRow row)
        {
            _shell.Run(() => ExportFileAsync(row.Version));
        }
    }

    // Decrypted into the cache, then handed to another app: the one the user picks to open it, or to share it with.
    private async Task HandAsync(DatasetEdited version, Action<string> hand, string action)
    {
        _shell.Services.Log.Info("Action", action);
        var path = _shell.Services.Opened.NewPath(version.File.Name);
        if (await ExportAsync(answers => _shell.Services.Session.ExportDataset(answers, version, path)))
        {
            hand(path);
        }
    }

    // Written where the user chooses; the file the picker made is deleted again when it cannot be.
    private async Task ExportFileAsync(DatasetEdited version)
    {
        _shell.Services.Log.Info("Action", "Export a dataset's file");
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage
            || await storage.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Export", SuggestedFileName = version.File.Name }) is not { } file)
        {
            return;
        }

        bool saved;
        await using (var target = await file.OpenWriteAsync())
        {
            saved = await ExportAsync(answers => _shell.Services.Session.ExportDataset(answers, version, target));
        }

        if (!saved)
        {
            await file.DeleteAsync();
        }
    }

    // The file downloaded first when it is not here, the busy bar shown; what went wrong told.
    private async Task<bool> ExportAsync(Func<PreparedAnswers, Task<bool>> export)
    {
        var answers = new PreparedAnswers();
        Busy.IsVisible = true;
        Files.IsEnabled = false;
        try
        {
            return await export(answers);
        }
        finally
        {
            Busy.IsVisible = false;
            Files.IsEnabled = true;
            _shell.Tell(answers.Told);
        }
    }
}
