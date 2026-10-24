using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Kofrenot.Core.Notes;

namespace Kofrenot.UI.Views;

/// <summary>
/// The items of a task list, each a checkbox and its text. Enter adds an item after the one typed
/// in, Backspace in an empty item removes it, Up/Down go from item to item in the order shown;
/// an item moves within its part (not checked, checked) by its grip or Alt+Up/Down (<see cref="Move"/>).
/// </summary>
public partial class TaskListBox : UserControl
{
    private readonly List<TextBox> _shownBoxes = [];
    private List<TaskItem> _items = [];
    private bool _isReadOnly;

    public TaskListBox() => InitializeComponent();

    /// <summary>Raised when the user changes the items (not when they are shown).</summary>
    public event Action<IReadOnlyList<TaskItem>>? Changed;

    /// <summary>Read only: items cannot be checked, typed in, added or removed.</summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            _isReadOnly = value;
            Build();
        }
    }

    /// <summary>Shows <paramref name="items"/>, in place of those shown.</summary>
    public void Show(IReadOnlyList<TaskItem> items)
    {
        _items = items.ToList();
        Build();
    }

    /// <summary>Puts the caret at the end of the first item not checked, or of the first item.</summary>
    public void FocusFirst()
    {
        if (_shownBoxes.Count > 0)
        {
            FocusLater(_shownBoxes[0]);
        }
    }

    private void Build(int? focusIndex = null)
    {
        OpenRows.Children.Clear();
        DoneRows.Children.Clear();
        _shownBoxes.Clear();
        _dropLines.Clear();
        var shown = Enumerable.Range(0, _items.Count).OrderBy(index => _items[index].IsDone).ToList();
        foreach (var index in shown)
        {
            (_items[index].IsDone ? DoneRows : OpenRows).Children.Add(RowOf(index, focusIndex == index));
        }

        var doneCount = _items.Count(item => item.IsDone);
        CheckedHeader.Text = doneCount == 1 ? "1 checked" : $"{doneCount} checked";
        CheckedHeader.Visibility = doneCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        AddButton.Visibility = _isReadOnly ? Visibility.Collapsed : Visibility.Visible;
    }

    private Grid RowOf(int index, bool focus)
    {
        var item = _items[index];
        var check = new CheckBox
        {
            IsChecked = item.IsDone,
            IsEnabled = !_isReadOnly,
            MinWidth = 0,
            Padding = new Thickness(0),
            Margin = new Thickness(6, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = false,
        };
        check.Click += (_, _) => Change(index, _items[index] with { IsDone = check.IsChecked == true });

        var box = new TextBox
        {
            Text = item.Text,
            IsReadOnly = _isReadOnly,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            TextWrapping = TextWrapping.Wrap,
            Opacity = item.IsDone ? 0.6 : 1,
            TextDecorations = item.IsDone ? TextDecorations.Strikethrough : null,
            Tag = index,
        };
        box.TextChanged += (_, _) => Edit(index, box.Text);
        box.PreviewKeyDown += Item_PreviewKeyDown;
        _shownBoxes.Add(box);
        if (focus)
        {
            FocusLater(box);
        }

        var grip = GripOf(index);
        var row = new Grid { Tag = index, Background = Brushes.Transparent, AllowDrop = !_isReadOnly };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(check, 1);
        Grid.SetColumn(box, 2);
        row.Children.Add(grip);
        row.Children.Add(check);
        row.Children.Add(box);
        AddDropLines(row, index);
        row.MouseEnter += (_, _) => grip.Opacity = GripOpacity;
        row.MouseLeave += (_, _) => grip.Opacity = 0;
        return row;
    }

    /// <summary>An item checked or unchecked: it moves to its part of the list.</summary>
    private void Change(int index, TaskItem item)
    {
        _items[index] = item;
        Build();
        Changed?.Invoke(_items);
    }

    /// <summary>An item typed in: the rows stay, so the caret does too.</summary>
    private void Edit(int index, string text)
    {
        _items[index] = _items[index] with { Text = text };
        Changed?.Invoke(_items);
    }

    private void Insert(int index)
    {
        _items.Insert(index, new TaskItem(string.Empty, false));
        Build(focusIndex: index);
        Changed?.Invoke(_items);
    }

    private void Remove(int index, TextBox box)
    {
        var before = _shownBoxes.IndexOf(box) - 1;
        _items.RemoveAt(index);
        int? focusIndex = before < 0 ? null : (int)_shownBoxes[before].Tag;
        Build(focusIndex is { } shown && shown > index ? shown - 1 : focusIndex);
        Changed?.Invoke(_items);
    }

    private void Item_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { Tag: int index } box || _isReadOnly)
        {
            return;
        }

        // Alt+Up/Down come as a system key.
        if (Keyboard.Modifiers == ModifierKeys.Alt && e.Key == Key.System && e.SystemKey is Key.Up or Key.Down)
        {
            MoveNextTo(index, box, e.SystemKey == Key.Up ? -1 : 1);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        var shownAt = _shownBoxes.IndexOf(box);
        switch (e.Key)
        {
            case Key.Enter:
                Insert(index + 1);
                break;
            case Key.Back when box.Text.Length == 0:
                Remove(index, box);
                break;
            case Key.Up when shownAt > 0:
                FocusLater(_shownBoxes[shownAt - 1]);
                break;
            case Key.Down when shownAt < _shownBoxes.Count - 1:
                FocusLater(_shownBoxes[shownAt + 1]);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private void Add_Click(object sender, RoutedEventArgs e) => Insert(_items.Count);

    /// <summary>Focuses a row once it is laid out (a row just built cannot take the focus yet).</summary>
    private void FocusLater(TextBox box) => Dispatcher.BeginInvoke(() =>
    {
        box.Focus();
        box.CaretIndex = box.Text.Length;
    }, DispatcherPriority.Loaded);
}
