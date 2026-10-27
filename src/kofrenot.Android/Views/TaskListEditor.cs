using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Kofrenot.Core.Notes;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The items of a task list being edited, as on Windows: each a checkbox, its text and ✕. Enter adds
/// an item after the one typed in; checked items are listed below the others, under "n checked".
/// Built in code: its rows change with the items.
/// </summary>
public sealed class TaskListEditor : StackPanel
{
    private List<TaskItem> _items = [];

    public TaskListEditor() => Spacing = 4;

    /// <summary>The items, in their order in the note.</summary>
    public IReadOnlyList<TaskItem> Items => _items;

    /// <summary>Shows <paramref name="items"/>, in place of those shown; an empty list gets one empty item, to type in.</summary>
    public void Show(IReadOnlyList<TaskItem> items)
    {
        _items = items.Count > 0 ? items.ToList() : [new TaskItem(string.Empty, false)];
        Build(focusIndex: null);
    }

    private void Build(int? focusIndex)
    {
        Children.Clear();
        var shown = Enumerable.Range(0, _items.Count).OrderBy(index => _items[index].IsDone).ToList();
        var doneCount = _items.Count(item => item.IsDone);
        foreach (var index in shown)
        {
            if (_items[index].IsDone && index == shown.First(i => _items[i].IsDone))
            {
                Children.Add(new TextBlock { Classes = { "caption" }, Text = doneCount == 1 ? "1 checked" : $"{doneCount} checked", Margin = new Thickness(0, 8, 0, 0) });
            }

            Children.Add(RowOf(index, focusIndex == index));
        }

        var add = new Button { Content = "+ Add item", Classes = { "flat" } };
        add.Click += (_, _) => Insert(_items.Count(item => !item.IsDone));
        Children.Insert(Math.Min(shown.Count(index => !_items[index].IsDone), Children.Count), add);
    }

    private Grid RowOf(int index, bool focus)
    {
        var item = _items[index];
        var check = new CheckBox { IsChecked = item.IsDone, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
        check.IsCheckedChanged += (_, _) =>
        {
            _items[index] = _items[index] with { IsDone = check.IsChecked == true };
            Build(focusIndex: null);
        };

        var box = new TextBox { Text = item.Text, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = item.IsDone ? 0.6 : 1 };
        box.TextChanged += (_, _) => _items[index] = _items[index] with { Text = box.Text ?? string.Empty };
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Insert(index + 1);
            }
        };
        if (focus)
        {
            box.AttachedToVisualTree += (_, _) => box.Focus();
        }

        var remove = new Button { Content = "✕", Classes = { "flat" }, VerticalAlignment = VerticalAlignment.Center };
        remove.Click += (_, _) =>
        {
            _items.RemoveAt(index);
            Build(focusIndex: null);
        };

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(box, 1);
        Grid.SetColumn(remove, 2);
        row.Children.Add(check);
        row.Children.Add(box);
        row.Children.Add(remove);
        return row;
    }

    // A new empty item at the place given, not checked, the caret in it.
    private void Insert(int index)
    {
        index = Math.Clamp(index, 0, _items.Count);
        _items.Insert(index, new TaskItem(string.Empty, false));
        Build(focusIndex: index);
    }
}
