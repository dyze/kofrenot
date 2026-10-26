using Android.Content;
using Avalonia.Platform.Storage;
using Kofrenot.Core.Datasets;

namespace Kofrenot.Phone.Platform;

/// <summary>
/// Files picked with Android's picker, as files to import: their name and size read once, their
/// content read through the <see cref="ContentResolver"/> each time it is needed (off the UI thread).
/// </summary>
public sealed class PickedFiles(Context context)
{
    public async Task<ImportFile> ImportFileOf(IStorageFile file)
    {
        var size = (long)((await file.GetBasicPropertiesAsync()).Size ?? 0);
        var uri = Android.Net.Uri.Parse(file.Path.OriginalString)!;
        return new ImportFile(file.Name, size, () => context.ContentResolver!.OpenInputStream(uri) ?? throw new IOException("The file cannot be read."));
    }
}
