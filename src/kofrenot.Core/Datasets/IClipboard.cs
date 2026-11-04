namespace Kofrenot.Core.Datasets;

/// <summary>
/// What the clipboard holds that can be pasted, known without reading it all: the files and folders
/// copied (Explorer's Copy), and whether it holds an image (a screenshot, a browser's Copy image).
/// </summary>
public sealed record ClipboardContent(IReadOnlyList<string> Paths, bool HasImage)
{
    public static readonly ClipboardContent Empty = new([], false);
}

/// <summary>The system's clipboard, read only (Windows': a listener tells when it changes).</summary>
public interface IClipboard
{
    /// <summary>What it holds now.</summary>
    ClipboardContent Content { get; }

    /// <summary>What it holds changed; raised on the UI thread.</summary>
    event Action? Changed;

    /// <summary>The image it holds, as a PNG file named by <see cref="PasteOffer.ImageNameOf"/>; null when it holds none, or it cannot be read.</summary>
    ImportFile? ReadImage();
}
