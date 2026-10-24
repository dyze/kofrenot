using Kofrenot.Core.Notes;

namespace Kofrenot.Core.Browse;

/// <summary>
/// What a note's card shows of its text: a task list's unchecked items and the number checked, or
/// the first lines of a text, as typed; each cut short. <see cref="HasMore"/>: more items or lines than shown.
/// </summary>
public sealed record NotePreview(IReadOnlyList<string> Lines, IReadOnlyList<string> Items, int CheckedCount, bool HasMore)
{
    public const int MaxLines = 6;
    public const int MaxLineLength = 100;

    public static NotePreview Of(string text)
    {
        if (TaskList.IsList(text))
        {
            var items = TaskList.Parse(text);
            var open = items.Where(item => !item.IsDone).Select(item => item.Text).ToList();
            return new NotePreview([], Shown(open), items.Count - open.Count, open.Count > MaxLines);
        }

        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        return new NotePreview(Shown(lines), [], 0, lines.Count > MaxLines);
    }

    private static List<string> Shown(IEnumerable<string> lines) =>
        lines.Take(MaxLines).Select(line => line.Length > MaxLineLength ? line[..MaxLineLength] + "…" : line).ToList();
}
