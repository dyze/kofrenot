using System.Globalization;

namespace Kofrenot.Core.Datasets;

/// <summary>What the phone's camera takes for kofrenot.</summary>
public enum CaptureKind
{
    Photo,
    Video,
}

/// <summary>A photo or video taken from kofrenot, named after when it was taken, then imported as any file.</summary>
public static class CapturedFile
{
    /// <summary>"Photo 2026-10-28 20.30.jpg", "Video 2026-10-28 20.30.mp4": the dataset is named so, without the extension.</summary>
    public static string NameOf(CaptureKind kind, DateTime localTime) =>
        string.Create(CultureInfo.InvariantCulture, $"{kind} {localTime:yyyy-MM-dd HH.mm}{ExtensionOf(kind)}");

    /// <summary>The file's extension, as the camera app writes it: ".jpg", ".mp4".</summary>
    public static string ExtensionOf(CaptureKind kind) => kind == CaptureKind.Photo ? ".jpg" : ".mp4";
}
