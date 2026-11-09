using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Phone.Platform;

namespace Kofrenot.Phone.Views;

/// <summary>
/// Files from the phone, for every screen that takes one: picked with Android's picker (read
/// through its content resolver), or a photo or video taken with the phone's camera app, written
/// into kofrenot's own files and deleted once used, or not.
/// </summary>
public sealed partial class Shell
{
    /// <summary>The files picked; none when cancelled. <paramref name="images"/>: only images offered.</summary>
    public async Task<IReadOnlyList<ImportFile>> PickAsync(string title, bool many = false, bool images = false)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return [];
        }

        var options = new FilePickerOpenOptions { Title = title, AllowMultiple = many, FileTypeFilter = images ? [FilePickerFileTypes.ImageAll] : null };
        var files = new List<ImportFile>();
        foreach (var file in await storage.OpenFilePickerAsync(options))
        {
            files.Add(await Services.Picked.ImportFileOf(file));
        }

        return files;
    }

    /// <summary>
    /// A dataset's file decrypted into the cache (cache/open/, deleted at the next start), downloaded first when
    /// it is not here, then handed to another app: the one the user picks to open it, or to share it with.
    /// </summary>
    public async Task<bool> HandAsync(DatasetEdited version, Action<string> hand, string action)
    {
        Services.Log.Info("Action", action);
        var path = Services.Opened.NewPath(version.File.Name);
        if (!await ExportAsync(answers => Services.Session.ExportDataset(answers, version, path)))
        {
            return false;
        }

        hand(path);
        return true;
    }

    /// <summary>A dataset's file written out, downloaded first when it is not here; what went wrong told.</summary>
    public async Task<bool> ExportAsync(Func<PreparedAnswers, Task<bool>> export)
    {
        var answers = new PreparedAnswers();
        try
        {
            return await export(answers);
        }
        finally
        {
            Tell(answers.Told);
        }
    }

    /// <summary>Takes a photo or video with the camera app, and hands it to <paramref name="use"/>; nothing when cancelled.</summary>
    public async Task CaptureAsync(CaptureKind kind, Func<ImportFile, Task> use)
    {
        var path = Services.Captures.NewPath(CapturedFile.NameOf(kind, DateTime.Now));
        try
        {
            if (!await TakeAsync(kind, path))
            {
                return;
            }

            var file = ImportFile.FromPath(path);
            Services.Log.Info("Action", kind == CaptureKind.Photo ? "Photo taken" : "Video recorded");
            Services.Log.Debug("Action", $"Capture of {file.Size} bytes");
            await use(file);
        }
        finally
        {
            Services.Captures.Delete(path);
        }
    }

    // True once the camera app wrote something; false, logged, when cancelled; told when there is no camera app.
    private async Task<bool> TakeAsync(CaptureKind kind, string path)
    {
        try
        {
            if (await Services.Camera.TakeAsync(kind, path) && File.Exists(path) && new FileInfo(path).Length > 0)
            {
                return true;
            }
        }
        catch (global::Android.Content.ActivityNotFoundException)
        {
            Services.Log.Warn("Action", "No camera app to take a photo or video");
            Tell("No camera app on this phone can take it.");
            return false;
        }

        Services.Log.Info("Action", "Camera cancelled");
        return false;
    }
}
