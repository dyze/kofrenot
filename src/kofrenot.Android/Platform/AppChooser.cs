using Android.Content;
using Android.Webkit;
using AndroidX.Core.Content;

namespace Kofrenot.Phone.Platform;

/// <summary>
/// Hands a dataset's file, decrypted into the app's cache (<see cref="Folder"/>), read only to the
/// app the user picks in Android's chooser (<see cref="FileProvider"/>).
/// </summary>
public sealed class AppChooser(Context context)
{
    private const string Authority = "eu.dlabs.kofrenot.files";
    private const string AnyType = "application/octet-stream";

    /// <summary>Where the files opened are decrypted (<c>cache/open/</c>), the folder the file provider shares.</summary>
    public string Folder => Path.Combine(context.CacheDir!.AbsolutePath, "open");

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
}
