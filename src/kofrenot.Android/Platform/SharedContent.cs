using Android.Content;
using Android.Provider;
using AndroidX.Core.Content;
using Kofrenot.Core.Datasets;

namespace Kofrenot.Phone.Platform;

/// <summary>What another app shared with kofrenot (Android's share sheet): files, or text (a message, a link).</summary>
public sealed record SharedContent(IReadOnlyList<ImportFile> Files, string? Text)
{
    /// <summary>
    /// The files or text of a share (<see cref="Intent.ActionSend"/>, <see cref="Intent.ActionSendMultiple"/>);
    /// null for another intent, or a share with nothing kofrenot can add. A file is read through the
    /// <see cref="ContentResolver"/>, while the share grants it: right after it is accepted.
    /// </summary>
    public static SharedContent? Of(Context context, Intent? intent)
    {
        if (intent?.Action is not (Intent.ActionSend or Intent.ActionSendMultiple))
        {
            return null;
        }

        var uriClass = Java.Lang.Class.FromType(typeof(Android.Net.Uri));
        IEnumerable<Java.Lang.Object?> uris = intent.Action == Intent.ActionSend
            ? [IntentCompat.GetParcelableExtra(intent, Intent.ExtraStream, uriClass)]
            : IntentCompat.GetParcelableArrayListExtra(intent, Intent.ExtraStream, uriClass)?.Cast<Java.Lang.Object?>() ?? [];
        var files = uris.OfType<Android.Net.Uri>().Select(uri => FileOf(context, uri)).ToList();
        if (files.Count > 0)
        {
            return new SharedContent(files, null);
        }

        var text = intent.GetStringExtra(Intent.ExtraText);
        var subject = intent.GetStringExtra(Intent.ExtraSubject);
        var whole = string.IsNullOrWhiteSpace(subject) || text?.StartsWith(subject, StringComparison.Ordinal) == true ? text : $"{subject}\n{text}";
        return string.IsNullOrWhiteSpace(whole) ? null : new SharedContent([], whole);
    }

    // Its name and size as the sharing app gives them; the content read when imported.
    private static ImportFile FileOf(Context context, Android.Net.Uri uri)
    {
        var resolver = context.ContentResolver!;
        var (name, size) = ("shared file", 0L);
        using (var cursor = resolver.Query(uri, [IOpenableColumns.DisplayName, IOpenableColumns.Size], null, null, null))
        {
            if (cursor?.MoveToFirst() == true)
            {
                name = cursor.GetString(0) ?? name;
                size = cursor.IsNull(1) ? 0 : cursor.GetLong(1);
            }
        }

        return new ImportFile(name, size, () => resolver.OpenInputStream(uri) ?? throw new IOException("The file cannot be read."));
    }
}
