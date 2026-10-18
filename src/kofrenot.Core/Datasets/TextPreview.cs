using System.Text;

namespace Kofrenot.Core.Datasets;

/// <summary>
/// The beginning of a text file, as shown by the viewer: at most <see cref="MaxChars"/>
/// characters, so a long file stays light. UTF-8 unless a byte order mark says otherwise.
/// </summary>
public sealed record TextPreview(string Text, bool IsCut)
{
    public const int MaxChars = 256 * 1024;

    /// <summary>Reads the beginning of <paramref name="content"/>, at most <paramref name="maxChars"/> characters.</summary>
    public static TextPreview Read(Stream content, int maxChars = MaxChars)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var buffer = new char[maxChars + 1];
        var read = reader.ReadBlock(buffer, 0, buffer.Length);
        return new TextPreview(new string(buffer, 0, Math.Min(read, maxChars)), read > maxChars);
    }
}
