namespace Kofrenot.Core.Notes;

/// <summary>What a line of a note is, as shown.</summary>
public enum BlockKind
{
    Paragraph,
    Heading,
    Bullet,
    Task,
}

/// <summary>A piece of a line, with its emphasis, or a link to <see cref="Link"/>.</summary>
public sealed record TextRun(string Text, bool IsBold = false, bool IsItalic = false, string? Link = null);

/// <summary>
/// A line of a note, as shown: its runs; a heading's <see cref="Level"/> (1 to 6) or a list
/// item's indent (0 at the margin); whether a task is done. A blank line has no runs.
/// </summary>
public sealed record MarkdownBlock(BlockKind Kind, IReadOnlyList<TextRun> Runs, int Level = 0, bool IsDone = false);

/// <summary>
/// A note's Markdown read to be shown, not edited: what the toolbar writes (<see cref="MarkdownFormat"/>)
/// and task lines (<see cref="TaskList"/>). One block per line, so the note's line breaks are kept;
/// what is not understood shows as typed.
/// </summary>
public static class MarkdownBlocks
{
    private const int MaxHeading = 6;

    public static IReadOnlyList<MarkdownBlock> Parse(string text) =>
        text.Split('\n').Select(line => BlockOf(line.TrimEnd('\r'))).ToList();

    /// <summary>The runs of a line's text: <c>**bold**</c>, <c>*italic*</c>, <c>[label](url)</c>; a mark not closed is text.</summary>
    public static IReadOnlyList<TextRun> Runs(string text)
    {
        var runs = new List<TextRun>();
        var plain = new System.Text.StringBuilder();
        var (bold, italic) = (false, false);

        void Flush()
        {
            if (plain.Length > 0)
            {
                runs.Add(new TextRun(plain.ToString(), bold, italic));
                plain.Clear();
            }
        }

        for (var i = 0; i < text.Length;)
        {
            if (LinkAt(text, i) is { } link)
            {
                Flush();
                runs.Add(new TextRun(link.Label, bold, italic, link.Url));
                i = link.End;
            }
            else if (string.CompareOrdinal(text, i, "**", 0, 2) == 0 && (bold || text.IndexOf("**", i + 2, StringComparison.Ordinal) > i + 2))
            {
                Flush();
                bold = !bold;
                i += 2;
            }
            else if (text[i] == '*' && (italic || text.IndexOf('*', i + 1) > i + 1))
            {
                Flush();
                italic = !italic;
                i++;
            }
            else
            {
                plain.Append(text[i]);
                i++;
            }
        }

        Flush();
        return runs;
    }

    private static MarkdownBlock BlockOf(string line)
    {
        var trimmed = line.TrimStart();
        var indent = (line.Length - trimmed.Length) / 2;
        if (string.IsNullOrWhiteSpace(line))
        {
            return new MarkdownBlock(BlockKind.Paragraph, []);
        }

        if (TaskList.ItemOf(trimmed) is { } item)
        {
            return new MarkdownBlock(BlockKind.Task, Runs(item.Text), indent, item.IsDone);
        }

        var hashes = trimmed.TakeWhile(c => c == '#').Count();
        if (hashes is > 0 and <= MaxHeading && trimmed.Length > hashes && trimmed[hashes] == ' ')
        {
            return new MarkdownBlock(BlockKind.Heading, Runs(trimmed[(hashes + 1)..]), hashes);
        }

        return trimmed.Length > 2 && trimmed[0] is '-' or '*' or '+' && trimmed[1] == ' '
            ? new MarkdownBlock(BlockKind.Bullet, Runs(trimmed[2..]), indent)
            : new MarkdownBlock(BlockKind.Paragraph, Runs(line));
    }

    // [label](url), the label not empty, the url without spaces.
    private static (string Label, string Url, int End)? LinkAt(string text, int start)
    {
        if (text[start] != '[')
        {
            return null;
        }

        var middle = text.IndexOf("](", start, StringComparison.Ordinal);
        var end = middle < 0 ? -1 : text.IndexOf(')', middle + 2);
        if (middle <= start + 1 || end < 0)
        {
            return null;
        }

        var url = text[(middle + 2)..end];
        return url.Length == 0 || url.Any(char.IsWhiteSpace) ? null : (text[(start + 1)..middle], url, end + 1);
    }
}
