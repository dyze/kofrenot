using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Datasets;

/// <summary>How kofrenot shows a dataset's file: as an image, as text, or not at all (export it).</summary>
public enum DatasetViewer
{
    None,
    Image,
    Text,
}

/// <summary>The kind of a dataset's file, for its icon.</summary>
public enum DatasetCategory
{
    Other,
    Image,
    Text,
    Code,
    Audio,
    Video,
    Archive,
    Pdf,
}

/// <summary>
/// The inner type of a dataset: its file's extension (lower case, without the dot; empty for
/// none), its category, and the viewer that shows it. Read from the file name, so nothing more is stored.
/// </summary>
public sealed record DatasetType(string Extension, DatasetCategory Category, DatasetViewer Viewer)
{
    /// <summary>The largest image shown, or given a thumbnail: it is decoded in memory.</summary>
    public const long MaxImageSize = 64L * 1024 * 1024;

    // The images WPF decodes: shown by the viewer, and given a thumbnail.
    private static readonly HashSet<string> DecodedImages = ["png", "jpg", "jpeg", "gif", "bmp", "tif", "tiff", "ico"];

    private static readonly Dictionary<string, DatasetCategory> Categories = CategoriesOf(
        (DatasetCategory.Image, [.. DecodedImages, "webp", "svg", "heic", "raw"]),
        (DatasetCategory.Text, ["txt", "md", "csv", "tsv", "log", "rtf"]),
        (DatasetCategory.Code,
        [
            "json", "xml", "html", "htm", "yaml", "yml", "ini", "toml", "cs", "py", "js", "ts", "css", "sql",
            "sh", "cmd", "ps1", "java", "c", "cpp", "h", "go", "rs",
        ]),
        (DatasetCategory.Audio, ["wav", "mp3", "flac", "ogg", "m4a", "aac", "wma"]),
        (DatasetCategory.Video, ["mp4", "mkv", "avi", "mov", "webm", "wmv"]),
        (DatasetCategory.Archive, ["zip", "7z", "rar", "tar", "gz", "bz2", "xz"]),
        (DatasetCategory.Pdf, ["pdf"]));

    /// <summary>"PNG file", or "File" without an extension.</summary>
    public string Name => Extension.Length == 0 ? "File" : $"{Extension.ToUpperInvariant()} file";

    public static DatasetType Of(DatasetFile file)
    {
        var extension = Path.GetExtension(file.Name).TrimStart('.').ToLowerInvariant();
        var category = Categories.GetValueOrDefault(extension, DatasetCategory.Other);
        var viewer = category switch
        {
            DatasetCategory.Image when DecodedImages.Contains(extension) => DatasetViewer.Image,
            DatasetCategory.Text or DatasetCategory.Code => DatasetViewer.Text,
            _ => DatasetViewer.None,
        };
        return new DatasetType(extension, category, viewer);
    }

    /// <summary>
    /// Why the file is not shown, or null when it is: no viewer for its type, or an image too
    /// large to decode in memory. Text is always shown, its beginning only when long (<see cref="TextPreview"/>).
    /// </summary>
    public static string? WhyNotShown(DatasetFile file) => Of(file) switch
    {
        { Viewer: DatasetViewer.None } type => $"No viewer for {(type.Extension.Length == 0 ? "this file" : type.Name + "s")}: export it to open it.",
        { Viewer: DatasetViewer.Image } when file.Size > MaxImageSize => $"Too large to show here (over {ByteSize.Format(MaxImageSize)}): export it to open it.",
        _ => null,
    };

    /// <summary>A thumbnail is made at import: an image the viewer shows.</summary>
    public static bool HasThumbnail(DatasetFile file) => Of(file).Viewer == DatasetViewer.Image && file.Size <= MaxImageSize;

    private static Dictionary<string, DatasetCategory> CategoriesOf(params (DatasetCategory Category, string[] Extensions)[] groups) =>
        groups.SelectMany(group => group.Extensions.Select(extension => (extension, group.Category)))
            .ToDictionary(pair => pair.extension, pair => pair.Category);
}
