using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Kofrenot.Core.Notes;

namespace Kofrenot.Phone.Views;

/// <summary>
/// The items of a task list, as on Windows (<see cref="TaskListEditing"/>): each a checkbox and its
/// text, those not checked first, "+ Add item" after them, then the checked ones collapsed under
/// their count until it is tapped. Enter adds an item after the one typed in; ✕, on the item typed in,
/// removes it, as does Backspace in an empty item. Built in code: its rows change with the items.
/// </summary>
public sealed class TaskListEditor : StackPanel
{
    private const double DoneOpacity = 0.6;

    private TaskListEditing _editing = new([]);
    private bool _isReadOnly;
    private Button? _typedRemove;

    public TaskListEditor() => Spacing = 2;

    /// <summary>Raised when the user changes the items (not when they are shown).</summary>
    public event Action<IReadOnlyList<TaskItem>>? Changed;

    /// <summary>Read only: items cannot be checked, typed in, added or removed.</summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            _isReadOnly = value;
            Build(focusIndex: null);
        }
    }

    /// <summary>Shows <paramref name="items"/>, in place of those shown, the checked ones collapsed.</summary>
    public void Show(IReadOnlyList<TaskItem> items)
    {
        _editing = new TaskListEditing(items);
        Build(focusIndex: null);
    }

    private void Build(int? focusIndex)
    {
        Children.Clear();
        _typedRemove = null;
        var shown = _editing.Shown;
        foreach (var index in shown.Where(index => !_editing.Items[index].IsDone))
        {
            Children.Add(RowOf(index, focusIndex == index));
        }

        if (!_isReadOnly)
        {
            var add = new Button { Content = Labeled("add", "Add item"), Classes = { "flat" }, Opacity = DoneOpacity };
            add.Click += (_, _) => Insert(_editing.Items.Count);
            Children.Add(add);
        }

        if (_editing.CheckedCount is var doneCount and > 0)
        {
            var header = new Button
            {
                Content = Labeled(_editing.ShowsChecked ? "chevron-down" : "chevron-right", doneCount == 1 ? "1 checked" : $"{doneCount} checked"),
                Classes = { "flat" },
                Opacity = DoneOpacity,
            };
            header.Click += (_, _) =>
            {
                _editing.ToggleChecked();
                Build(focusIndex: null);
            };
            Children.Add(header);
        }

        foreach (var index in shown.Where(index => _editing.Items[index].IsDone))
        {
            Children.Add(RowOf(index, focusIndex == index));
        }
    }

    // An icon before its text, as the page's actions (iconLabel, NodePage.axaml).
    private static StackPanel Labeled(string icon, string text) => new()
    {
        Classes = { "iconLabel" },
        Children = { new PathIcon { Data = PhoneTheme.Icon(icon) }, new TextBlock { Text = text } },
    };

    private Grid RowOf(int index, bool focus)
    {
        var item = _editing.Items[index];
        var check = new CheckBox { IsChecked = item.IsDone, IsEnabled = !_isReadOnly, MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
        check.IsCheckedChanged += (_, _) =>
        {
            _editing.Check(index, check.IsChecked == true);
            Build(focusIndex: null);
            Changed?.Invoke(_editing.Items);
        };

        var box = new TextBox
        {
            Text = item.Text,
            IsReadOnly = _isReadOnly,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Opacity = item.IsDone ? DoneOpacity : 1,
            BorderThickness = new Thickness(0),
            Background = Avalonia.Media.Brushes.Transparent,
        };
        var remove = new Button { Content = "✕", Classes = { "flat" }, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
        remove.Click += (_, _) => Remove(index);
        box.TextChanged += (_, _) =>
        {
            if (box.Text != _editing.Items[index].Text)
            {
                _editing.Edit(index, box.Text ?? string.Empty);
                Changed?.Invoke(_editing.Items);
            }
        };
        box.GotFocus += (_, _) => ShowRemove(remove);
        box.KeyDown += (_, e) => e.Handled = !_isReadOnly && KeyOf(e.Key, index, box.Text);
        if (focus)
        {
            box.AttachedToVisualTree += (_, _) =>
            {
                box.Focus();
                box.CaretIndex = box.Text?.Length ?? 0;
            };
        }

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(box, 1);
        Grid.SetColumn(remove, 2);
        row.Children.Add(check);
        row.Children.Add(box);
        row.Children.Add(remove);
        return row;
    }

    // ✕ shows on the item typed in (the last one focused), not on all: taps on it keep it there.
    private void ShowRemove(Button remove)
    {
        if (_typedRemove is { } before)
        {
            before.IsVisible = false;
        }

        remove.IsVisible = !_isReadOnly;
        _typedRemove = remove;
    }

    // Enter: a new item after this one; Backspace in an empty item: it goes. True when handled.
    private bool KeyOf(Key key, int index, string? text)
    {
        switch (key)
        {
            case Key.Enter:
                Insert(index + 1);
                return true;
            case Key.Back when string.IsNullOrEmpty(text):
                Remove(index);
                return true;
            default:
                return false;
        }
    }

    private void Insert(int index)
    {
        Build(focusIndex: _editing.Insert(index));
        Changed?.Invoke(_editing.Items);
    }

    private void Remove(int index)
    {
        Build(focusIndex: _editing.Remove(index));
        Changed?.Invoke(_editing.Items);
    }
}
