namespace Kofrenot.Core.Notes;

/// <summary>
/// A task list being edited, as in Google Keep: the items not checked are shown first, then the
/// checked ones, collapsed under their count until opened. An item moves only within its part.
/// The items keep their order in the note (<see cref="Items"/>); <see cref="Shown"/> gives the order on screen.
/// </summary>
public sealed class TaskListEditing
{
    private readonly List<TaskItem> _items;

    public TaskListEditing(IEnumerable<TaskItem> items) => _items = items.ToList();

    /// <summary>The items, in their order in the note.</summary>
    public IReadOnlyList<TaskItem> Items => _items;

    /// <summary>The checked items are shown, under their count; otherwise only the count is.</summary>
    public bool ShowsChecked { get; private set; }

    /// <summary>How many items are checked.</summary>
    public int CheckedCount => _items.Count(item => item.IsDone);

    /// <summary>The indexes of the items shown, in the order shown: those not checked, then the checked ones when shown.</summary>
    public IReadOnlyList<int> Shown => ShownList();

    /// <summary>Shows or hides the checked items.</summary>
    public void ToggleChecked() => ShowsChecked = !ShowsChecked;

    /// <summary>A new empty item, not checked, at the end of the note: the last of those not checked. Its index.</summary>
    public int Add() => Insert(_items.Count);

    /// <summary>A new empty item, not checked, put at <paramref name="index"/> (kept within the list). Its index.</summary>
    public int Insert(int index)
    {
        index = Math.Clamp(index, 0, _items.Count);
        _items.Insert(index, new TaskItem(string.Empty, false));
        return index;
    }

    /// <summary>Checks or unchecks an item: it goes to its part of the list.</summary>
    public void Check(int index, bool isDone) => _items[index] = _items[index] with { IsDone = isDone };

    /// <summary>Changes an item's text.</summary>
    public void Edit(int index, string text) => _items[index] = _items[index] with { Text = text };

    /// <summary>Removes an item. The index, once it is gone, of the item shown before it (to type in next); null when it was shown first.</summary>
    public int? Remove(int index)
    {
        var shown = ShownList();
        var shownAt = shown.IndexOf(index);
        _items.RemoveAt(index);
        if (shownAt <= 0)
        {
            return null;
        }

        var before = shown[shownAt - 1];
        return before > index ? before - 1 : before;
    }

    /// <summary>
    /// The item shown <paramref name="step"/> places from <paramref name="index"/> (-1: above, 1: below)
    /// when it is in the same part; null otherwise.
    /// </summary>
    public int? Neighbour(int index, int step)
    {
        var shown = ShownList();
        var at = shown.IndexOf(index) + step;
        return shown.Contains(index) && at >= 0 && at < shown.Count && _items[shown[at]].IsDone == _items[index].IsDone ? shown[at] : null;
    }

    /// <summary>Moves an item to <paramref name="to"/>, the place it takes once moved, when both are in the same part. False when nothing moved.</summary>
    public bool Move(int from, int to)
    {
        if (_items[from].IsDone != _items[to].IsDone)
        {
            return false;
        }

        return MoveItem(from, to);
    }

    /// <summary>
    /// An item dropped before or after <paramref name="target"/>: the index it ends at, or null
    /// when it was not moved (another part, or its own place).
    /// </summary>
    public int? Drop(int from, int target, bool before)
    {
        // Where it is put among the items before it is taken out, then once taken out.
        var place = before ? target : target + 1;
        var to = place > from ? place - 1 : place;
        return CanDrop(from, target) && MoveItem(from, to) ? to : null;
    }

    /// <summary>An item can be dropped next to <paramref name="target"/>: both are in the list, in the same part.</summary>
    public bool CanDrop(int from, int target) =>
        from >= 0 && target >= 0 && from < _items.Count && target < _items.Count && _items[from].IsDone == _items[target].IsDone;

    private bool MoveItem(int from, int to)
    {
        if (from == to)
        {
            return false;
        }

        var item = _items[from];
        _items.RemoveAt(from);
        _items.Insert(to, item);
        return true;
    }

    private List<int> ShownList() => Enumerable.Range(0, _items.Count)
        .Where(index => ShowsChecked || !_items[index].IsDone)
        .OrderBy(index => _items[index].IsDone)
        .ToList();
}
