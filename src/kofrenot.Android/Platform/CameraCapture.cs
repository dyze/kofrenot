using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Provider;
using AndroidX.Core.Content;
using Kofrenot.Core.Datasets;

namespace Kofrenot.Phone.Platform;

/// <summary>
/// A photo or video taken with the phone's camera app, written straight into the app's cache
/// (<see cref="Folder"/>), not into the gallery: the camera app is given that one file, to write,
/// through the <see cref="FileProvider"/>. kofrenot asks for no camera permission.
/// </summary>
public sealed class CameraCapture(Context context)
{
    private const string Authority = "eu.dlabs.kofrenot.files";
    private const int RequestCode = 0x4b43;

    private TaskCompletionSource<bool>? _taking;

    /// <summary>The activity shown, which starts the camera app; null while none is.</summary>
    public Activity? Activity { get; set; }

    /// <summary>Where the camera app writes (<c>cache/camera/</c>), a folder the file provider shares.</summary>
    public string Folder => Path.Combine(context.CacheDir!.AbsolutePath, "camera");

    /// <summary>The phone has a camera.</summary>
    public bool IsAvailable => context.PackageManager?.HasSystemFeature(PackageManager.FeatureCameraAny) == true;

    /// <summary>
    /// Opens the camera app to take a photo or a video into <paramref name="path"/>. True once it
    /// took one, false when it was cancelled.
    /// </summary>
    /// <exception cref="ActivityNotFoundException">No camera app.</exception>
    public Task<bool> TakeAsync(CaptureKind kind, string path)
    {
        var activity = Activity ?? throw new InvalidOperationException("kofrenot is not shown.");
        _taking?.TrySetResult(false);
        _taking = new TaskCompletionSource<bool>();

        var uri = FileProvider.GetUriForFile(context, Authority, new Java.IO.File(path))!;
        var intent = new Intent(kind == CaptureKind.Photo ? MediaStore.ActionImageCapture : MediaStore.ActionVideoCapture)
            .PutExtra(MediaStore.ExtraOutput, uri)
            .AddFlags(ActivityFlags.GrantWriteUriPermission | ActivityFlags.GrantReadUriPermission);
        // The grant applies to the intent's clip data: the camera app may write that file, nothing else.
        intent.ClipData = ClipData.NewRawUri(string.Empty, uri);
        activity.StartActivityForResult(intent, RequestCode);
        return _taking.Task;
    }

    /// <summary>The camera app is done (the activity's <c>OnActivityResult</c>).</summary>
    public void Finished(int requestCode, Result result)
    {
        if (requestCode == RequestCode)
        {
            _taking?.TrySetResult(result == Result.Ok);
            _taking = null;
        }
    }
}
