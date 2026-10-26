using Android.Content;
using Android.Webkit;
using AndroidX.Core.Content;

namespace Kofrenot.Phone.Platform;

/// <summary>
/// A dataset's file opened with another app: decrypted into the app's cache (<c>cache/open/</c>),
/// handed read only to the app the user picks in Android's chooser (<see cref="FileProvider"/>),
/// and deleted at the next start.
/// </summary>
public sealed class OpenedFiles(Context context)
{
    private const string Authority = "eu.dlabs.kofrenot.files";
    private const string AnyType = "application/octet-stream";

    private string Folder => Path.Combine(context.CacheDir!.AbsolutePath, "open");

    /// <summary>Where to decrypt a file named <paramref name="fileName"/>: a folder of its own, so its name is kept.</summary>
    public string NewPath(string fileName)
    {
        var folder = Path.Combine(Folder, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, Path.GetFileName(fileName));
    }

    /// <summary>Shows Android's chooser for the file at <paramref name="path"/>; it says so itself when no app opens it.</summary>
    public void Open(string path)
    {
        var uri = FileProvider.GetUriForFile(context, Authority, new Java.IO.File(path));
        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        var view = new Intent(Intent.ActionView)
            .SetDataAndType(uri, MimeTypeMap.Singleton?.GetMimeTypeFromExtension(extension) ?? AnyType)
            .AddFlags(ActivityFlags.GrantReadUriPermission);
        context.StartActivity(Intent.CreateChooser(view, "Open with")!.AddFlags(ActivityFlags.NewTask));
    }

    /// <summary>Deletes the files opened before: at each start, once the apps they were given to are done.</summary>
    /// <exception cref="IOException">One is still in use.</exception>
    public void Clear()
    {
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, recursive: true);
        }
    }
}
