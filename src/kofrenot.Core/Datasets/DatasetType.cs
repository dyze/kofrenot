using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Datasets;

/// <summary>How kofrenot shows a dataset's file: as an image, as text, or not at all (export it).</summary>
public enum DatasetViewer
{
    None,
    Image,
    Text,
}

/// <summary>
/// The inner type of a dataset: its file's extension (lower case, without the dot; empty for
/// none), and the viewer that shows it. Read from the file name, so nothing more is stored.
/// </summary>
public sealed record DatasetType(string Extension, DatasetViewer Viewer)
{
    /// <summary>The largest image shown: it is decoded in memory.</summary>
    public const long MaxImageSize = 64L * 1024 * 1024;

    private static readonly HashSet<string> Images = ["png", "jpg", "jpeg", "gif", "bmp", "tif", "tiff", "ico"];

    private static readonly HashSet<string> Texts =
    [
        "txt", "md", "csv", "tsv", "log", "json", "xml", "html", "htm", "yaml", "yml", "ini", "toml",
        "cs", "py", "js", "ts", "css", "sql", "sh", "cmd", "ps1",
    ];

    /// <summary>"PNG file", or "File" without an extension.</summary>
    public string Name => Extension.Length == 0 ? "File" : $"{Extension.ToUpperInvariant()} file";

    public static DatasetType Of(DatasetFile file)
    {
        var extension = Path.GetExtension(file.Name).TrimStart('.').ToLowerInvariant();
        var viewer = Images.Contains(extension) ? DatasetViewer.Image
            : Texts.Contains(extension) ? DatasetViewer.Text
            : DatasetViewer.None;
        return new DatasetType(extension, viewer);
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
}
