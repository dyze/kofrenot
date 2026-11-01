using Avalonia.Interactivity;
using Kofrenot.Core.Datasets;

namespace Kofrenot.Phone.Views;

/// <summary>
/// ＋: files picked with Android's picker, or a photo or video taken with the phone's camera app
/// (<see cref="Shell.PickAsync"/>, <see cref="Shell.CaptureAsync"/>), then added as datasets after
/// their preview (<see cref="Shell.AddFilesAsync"/>).
/// </summary>
public sealed partial class HomePage
{
    // ＋: a sheet, a node first (HomePage.AddNode); the camera's choices only when the phone has a camera app.
    private void AddFiles_Click(object? sender, RoutedEventArgs e)
    {
        var camera = _shell.Services.Camera.IsAvailable;
        _shell.ShowSheet("Add",
        [
            .. AddNodeChoice() is { } node ? [node] : Array.Empty<SheetChoice>(),
            new("Files…", () => _shell.Run(AddFilesAsync), Icon: "file"),
            .. camera ? new SheetChoice[]
            {
                new("Take a photo", () => _shell.Run(() => _shell.CaptureAsync(CaptureKind.Photo, file => _shell.AddFilesAsync([file]))), Icon: "camera"),
                new("Record a video", () => _shell.Run(() => _shell.CaptureAsync(CaptureKind.Video, file => _shell.AddFilesAsync([file]))), Icon: "video"),
            } : [],
        ]);
    }

    private async Task AddFilesAsync()
    {
        if (await _shell.PickAsync("Add files", many: true) is { Count: > 0 } files)
        {
            await _shell.AddFilesAsync(files);
        }
    }
}
