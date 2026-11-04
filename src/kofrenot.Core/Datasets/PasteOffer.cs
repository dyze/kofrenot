using System.Globalization;
using Kofrenot.Core.Input;

namespace Kofrenot.Core.Datasets;

/// <summary>What was read off the clipboard to paste: the paths of the files and folders copied, else one image.</summary>
public sealed record Pasted(IReadOnlyList<string> Paths, ImportFile? Image)
{
    /// <summary>The one file given to a dataset or an image field: the image, or the one file copied; null with several, or a folder.</summary>
    /// <exception cref="IOException">The file copied cannot be read.</exception>
    public ImportFile? OneFile => Image ?? (Paths is [var path] && File.Exists(path) ? ImportFile.FromPath(path) : null);

    /// <summary>Its import as datasets, under <paramref name="under"/>, as files dropped there are.</summary>
    public ImportPreview Preview(FileImportPlanner planner, ParentLink? under) =>
        Image is { } image ? planner.Preview([image], under) : planner.Preview(Paths, under);
}

/// <summary>
/// Where what the clipboard holds can be pasted: what a Paste button is shown for. The files copied
/// come first; without any, the image. Pasting shows its preview, and asks, first.
/// </summary>
public static class PasteOffer
{
    /// <summary>An image field takes an image, or one image file copied.</summary>
    public static bool FitsImage(ClipboardContent content) => content.Paths.Count == 0
        ? content.HasImage
        : content.Paths is [var path] && DatasetType.Of(Path.GetFileName(path)).Viewer == DatasetViewer.Image && File.Exists(path);

    /// <summary>A dataset takes, as its new version, an image or one file copied.</summary>
    public static bool FitsVersion(ClipboardContent content) => content.Paths.Count == 0
        ? content.HasImage
        : content.Paths is [var path] && File.Exists(path);

    /// <summary>New datasets, each on its own (no folder): an image, or files copied.</summary>
    public static bool FitsFiles(ClipboardContent content) => content.Paths.Count == 0
        ? content.HasImage
        : content.Paths.All(File.Exists);

    /// <summary>New datasets, folders as nodes holding their content: an image, or anything copied.</summary>
    public static bool FitsImport(ClipboardContent content) => content.Paths.Count > 0 || content.HasImage;

    /// <summary>What to paste, read now: the paths copied, else the image; null when it holds neither (any more).</summary>
    public static Pasted? Read(IClipboard clipboard) =>
        clipboard.Content.Paths is { Count: > 0 } paths ? new Pasted(paths, null)
        : clipboard.ReadImage() is { } image ? new Pasted([], image)
        : null;

    /// <summary>"Pasted image 2026-11-04 10.30.15.png": the dataset is named so, without the extension.</summary>
    public static string ImageNameOf(DateTime localTime) =>
        string.Create(CultureInfo.InvariantCulture, $"Pasted image {localTime:yyyy-MM-dd HH.mm.ss}.png");
}
