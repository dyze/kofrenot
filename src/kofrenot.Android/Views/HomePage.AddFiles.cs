using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Kofrenot.Core.Datasets;
using Kofrenot.Core.Logging;

namespace Kofrenot.Phone.Views;

/// <summary>
/// ＋: files picked with Android's picker, or a photo or video taken with the phone's camera app,
/// then added as datasets after their preview (<see cref="Shell.AddFilesAsync"/>). A photo or video
/// is deleted from the app's cache once added, or not.
/// </summary>
public sealed partial class HomePage
{
    // ＋: a sheet; the camera's choices only when the phone has a camera app.
    private void AddFiles_Click(object? sender, RoutedEventArgs e)
    {
        var camera = _shell.Services.Camera.IsAvailable;
        _shell.ShowSheet("Add",
        [
            new("Files…", () => _shell.Run(AddFilesAsync)),
            .. camera ? new SheetChoice[]
            {
                new("Take a photo", () => _shell.Run(() => CaptureAsync(CaptureKind.Photo))),
                new("Record a video", () => _shell.Run(() => CaptureAsync(CaptureKind.Video))),
            } : [],
        ]);
    }

    private async Task AddFilesAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            return;
        }

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Add files", AllowMultiple = true });
        var files = new List<ImportFile>();
        foreach (var file in picked)
        {
            files.Add(await _shell.Services.Picked.ImportFileOf(file));
        }

        if (files.Count > 0)
        {
            await _shell.AddFilesAsync(files);
        }
    }

    private async Task CaptureAsync(CaptureKind kind)
    {
        var services = _shell.Services;
        var path = services.Captures.NewPath(CapturedFile.NameOf(kind, DateTime.Now));
        try
        {
            if (!await TakeAsync(kind, path))
            {
                return;
            }

            var file = ImportFile.FromPath(path);
            services.Log.Info("Action", kind == CaptureKind.Photo ? "Photo taken" : "Video recorded");
            services.Log.Debug("Action", $"Capture of {file.Size} bytes");
            await _shell.AddFilesAsync([file]);
        }
        finally
        {
            services.Captures.Delete(path);
        }
    }

    // True once the camera app wrote something; false, logged, when cancelled; told when there is no camera app.
    private async Task<bool> TakeAsync(CaptureKind kind, string path)
    {
        var services = _shell.Services;
        try
        {
            if (await services.Camera.TakeAsync(kind, path) && File.Exists(path) && new FileInfo(path).Length > 0)
            {
                return true;
            }
        }
        catch (global::Android.Content.ActivityNotFoundException)
        {
            services.Log.Warn("Action", "No camera app to take a photo or video");
            _shell.Tell("No camera app on this phone can take it.");
            return false;
        }

        services.Log.Info("Action", "Camera cancelled");
        return false;
    }
}
