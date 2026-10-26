namespace Kofrenot.Core.Notes;

/// <summary>An item of a task list: its text, and whether it is checked.</summary>
public sealed record TaskItem(string Text, bool IsDone);

/// <summary>How many items of a note are checked, of how many.</summary>
public sealed record TaskProgress(int Done, int Total);

/// <summary>
/// A note shown as a task list, as in Google Keep: its items are Markdown task lines
/// (<c>- [ ] text</c>, <c>- [x] text</c>), so nothing but the text is stored. A note is a list when
/// its lines are all items, blank lines aside. Its line breaks (<c>\r\n</c> or <c>\n</c>) are kept.
/// </summary>
public static class TaskList
{
    private const string OpenMark = "- [ ]";
    private const string DoneMark = "- [x]";

    /// <summary>The text has items, and only items (blank lines aside).</summary>
    public static bool IsList(string text)
    {
        var lines = FilledLines(text).ToList();
        return lines.Count > 0 && lines.All(line => ItemOf(line) is not null);
    }

    /// <summary>The items of the text, in order; other lines are left out.</summary>
    public static IReadOnlyList<TaskItem> Parse(string text) =>
        FilledLines(text).Select(ItemOf).OfType<TaskItem>().ToList();

    /// <summary>The text of <paramref name="items"/>, one line each.</summary>
    public static string Format(IEnumerable<TaskItem> items, string lineBreak = "\n") =>
        string.Join(lineBreak, items.Select(item => $"{(item.IsDone ? DoneMark : OpenMark)} {item.Text}"));

    /// <summary>
    /// Show checkboxes: each line becomes an unchecked item, an item stays as it is, blank lines
    /// go. An empty text becomes one empty item, to be typed in.
    /// </summary>
    public static string Show(string text)
    {
        var items = FilledLines(text).Select(line => ItemOf(line) ?? new TaskItem(line, false)).ToList();
        return Format(items.Count > 0 ? items : [new TaskItem(string.Empty, false)], LineBreakOf(text));
    }

    /// <summary>
    /// The text with its item number <paramref name="task"/> (from 0, in order, indented ones
    /// included, as <see cref="MarkdownBlocks"/> shows them) checked or unchecked; every other
    /// character as it was.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The text has no such item.</exception>
    public static string Toggle(string text, int task)
    {
        var lines = Lines(text).ToList();
        var seen = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (ItemOf(trimmed) is { } item && ++seen == task)
            {
                var indent = lines[i][..(lines[i].Length - trimmed.Length)];
                lines[i] = indent + (item.IsDone ? OpenMark : DoneMark) + trimmed[OpenMark.Length..];
                return string.Join(LineBreakOf(text), lines);
            }
        }

        throw new ArgumentOutOfRangeException(nameof(task), task, "The text has no such item.");
    }

    /// <summary>Hide checkboxes: each item becomes its text, checked or not; other lines stay.</summary>
    public static string Hide(string text) =>
        string.Join(LineBreakOf(text), Lines(text).Select(line => ItemOf(line)?.Text ?? line));

    /// <summary>
    /// <paramref name="items"/> with the item at <paramref name="from"/> moved to <paramref name="to"/>,
    /// the place it takes once moved (the others keep their order).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An index not in the list.</exception>
    public static IReadOnlyList<TaskItem> Move(IReadOnlyList<TaskItem> items, int from, int to)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(from);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(from, items.Count);
        ArgumentOutOfRangeException.ThrowIfNegative(to);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(to, items.Count);
        var moved = items.ToList();
        var item = moved[from];
        moved.RemoveAt(from);
        moved.Insert(to, item);
        return moved;
    }

    /// <summary>The items checked, of all the items of the text (a list or not).</summary>
    public static TaskProgress ProgressOf(string text)
    {
        var items = Parse(text);
        return new TaskProgress(items.Count(item => item.IsDone), items.Count);
    }

    /// <summary>The line break the text uses: <c>\r\n</c> (typed in a Windows text box) or <c>\n</c>.</summary>
    public static string LineBreakOf(string text) => text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static IEnumerable<string> Lines(string text) => text.Split('\n').Select(line => line.TrimEnd('\r'));

    private static IEnumerable<string> FilledLines(string text) => Lines(text).Where(line => !string.IsNullOrWhiteSpace(line));

    /// <summary>The item a line is, or null when it is not one.</summary>
    public static TaskItem? ItemOf(string line)
    {
        var isDone = line.StartsWith(DoneMark, StringComparison.OrdinalIgnoreCase);
        if (!isDone && !line.StartsWith(OpenMark, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = line[OpenMark.Length..];
        return rest.Length == 0 || rest[0] == ' ' ? new TaskItem(rest.Length == 0 ? rest : rest[1..], isDone) : null;
    }
}
