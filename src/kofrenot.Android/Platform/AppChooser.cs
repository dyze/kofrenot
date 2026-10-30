using Android.Content;
using Android.Webkit;
using AndroidX.Core.Content;

namespace Kofrenot.Phone.Platform;

/// <summary>
/// Hands a dataset's file, decrypted into the app's cache (<see cref="Folder"/>), read only to the
/// app the user picks in Android's chooser (<see cref="FileProvider"/>): to open it, or to share it.
/// </summary>
public sealed class AppChooser(Context context)
{
    private const string Authority = "eu.dlabs.kofrenot.files";
    private const string AnyType = "application/octet-stream";

    /// <summary>Where the files opened are decrypted (<c>cache/open/</c>), the folder the file provider shares.</summary>
    public string Folder => Path.Combine(context.CacheDir!.AbsolutePath, "open");

    /// <summary>Shows Android's chooser to open the file at <paramref name="path"/>; it says so itself when no app opens it.</summary>
    public void Open(string path)
    {
        var (uri, type) = UriOf(path);
        Choose(new Intent(Intent.ActionView).SetDataAndType(uri, type), "Open with");
    }

    /// <summary>Shows Android's share sheet for the file at <paramref name="path"/>.</summary>
    public void Share(string path)
    {
        var (uri, type) = UriOf(path);
        Choose(new Intent(Intent.ActionSend).SetType(type).PutExtra(Intent.ExtraStream, uri), "Share");
    }

    private (Android.Net.Uri Uri, string Type) UriOf(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return (FileProvider.GetUriForFile(context, Authority, new Java.IO.File(path))!, MimeTypeMap.Singleton?.GetMimeTypeFromExtension(extension) ?? AnyType);
    }

    private void Choose(Intent intent, string title) =>
        context.StartActivity(Intent.CreateChooser(intent.AddFlags(ActivityFlags.GrantReadUriPermission), title)!.AddFlags(ActivityFlags.NewTask));
}
