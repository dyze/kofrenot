namespace Kofrenot.Core.Notes;

/// <summary>A text with its selection (or caret, when <see cref="Length"/> is 0).</summary>
public sealed record TextEdit(string Text, int Start, int Length)
{
    public int End => Start + Length;
}

/// <summary>
/// The note toolbar: Markdown inserted around the selection or at the start of its lines. Each
/// returns the new text with what to select next.
/// </summary>
public static class MarkdownFormat
{
    public const string LinkPlaceholder = "url";

    /// <summary>Bold (<c>**</c>) or italic (<c>*</c>): wraps the selection, or puts the caret between the marks.</summary>
    public static TextEdit Emphasis(TextEdit edit, string marker) =>
        new(edit.Text.Insert(edit.End, marker).Insert(edit.Start, marker), edit.Start + marker.Length, edit.Length);

    /// <summary>
    /// A heading (<c># </c>) or a list item (<c>- </c>) for each line of the selection; removed
    /// instead when every one of them has it already.
    /// </summary>
    public static TextEdit LinePrefix(TextEdit edit, string prefix)
    {
        var first = edit.Start == 0 ? 0 : edit.Text.LastIndexOf('\n', edit.Start - 1) + 1;
        // From the selection's last character: a selection ending with a line break leaves the next line alone.
        var lineBreak = edit.Text.IndexOf('\n', edit.Length > 0 ? edit.End - 1 : edit.Start);
        var end = lineBreak < 0 ? edit.Text.Length : lineBreak;
        var lines = edit.Text[first..end].Split('\n');
        var remove = lines.All(line => line.StartsWith(prefix, StringComparison.Ordinal));
        var changed = string.Join('\n', lines.Select(line => remove ? line[prefix.Length..] : prefix + line));
        var text = edit.Text[..first] + changed + edit.Text[end..];
        return new TextEdit(text, first, changed.Length);
    }

    /// <summary>A link: <c>[selection](url)</c>, with <c>url</c> selected to be typed over.</summary>
    public static TextEdit Link(TextEdit edit)
    {
        var label = edit.Text.Substring(edit.Start, edit.Length);
        var link = $"[{label}]({LinkPlaceholder})";
        var text = edit.Text[..edit.Start] + link + edit.Text[edit.End..];
        return new TextEdit(text, edit.Start + label.Length + 3, LinkPlaceholder.Length);
    }
}
