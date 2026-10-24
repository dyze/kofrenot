using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Kofrenot.Core.Notes;

namespace Kofrenot.UI.Views;

/// <summary>
/// Moving an item within its part of the list (not checked, or checked), as in Google Keep: by
/// dragging its grip, shown while the row is hovered (a line shows where it goes), or by Alt+Up/Down.
/// </summary>
public partial class TaskListBox
{
    private const string DragFormat = "kofrenot.TaskItem";
    private const double GripOpacity = 0.6;
    private const double DropLineHeight = 2;

    // Each row's lines, above and below it: one shows where a dragged item goes.
    private readonly Dictionary<int, (Rectangle Above, Rectangle Below)> _dropLines = [];
    private (Point At, int Index)? _gripPressed;

    private TextBlock GripOf(int index)
    {
        var grip = new TextBlock
        {
            Text = "",
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 14,
            Opacity = 0,
            Padding = new Thickness(2, 4, 2, 4),
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeAll,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "Drag to move (Alt+Up, Alt+Down)",
            Visibility = _isReadOnly ? Visibility.Collapsed : Visibility.Visible,
        };
        grip.MouseLeftButtonDown += (_, e) =>
        {
            _gripPressed = (e.GetPosition(this), index);
            e.Handled = true;
        };
        grip.MouseMove += Grip_MouseMove;
        return grip;
    }

    /// <summary>A row's drop lines, over its top and bottom edges: they take no room.</summary>
    private void AddDropLines(Grid row, int index)
    {
        var above = DropLine(VerticalAlignment.Top);
        var below = DropLine(VerticalAlignment.Bottom);
        row.Children.Add(above);
        row.Children.Add(below);
        _dropLines[index] = (above, below);
        row.DragOver += Row_DragOver;
        row.DragLeave += (_, _) => HideDropLines();
        row.Drop += Row_Drop;
    }

    private static Rectangle DropLine(VerticalAlignment edge)
    {
        var line = new Rectangle
        {
            Height = DropLineHeight,
            VerticalAlignment = edge,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
        };
        line.SetResourceReference(Shape.FillProperty, "AccentFillColorDefaultBrush");
        Grid.SetColumnSpan(line, 3);
        return line;
    }

    private void Grip_MouseMove(object sender, MouseEventArgs e)
    {
        if (_gripPressed is not { } pressed || e.LeftButton != MouseButtonState.Pressed)
        {
            _gripPressed = null;
            return;
        }

        var moved = e.GetPosition(this) - pressed.At;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _gripPressed = null;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(DragFormat, pressed.Index), DragDropEffects.Move);
        HideDropLines();
    }

    private void Row_DragOver(object sender, DragEventArgs e)
    {
        HideDropLines();
        e.Effects = DragDropEffects.None;
        if (DropOf(sender, e) is { } drop && _dropLines.TryGetValue(drop.Target, out var lines))
        {
            (drop.Before ? lines.Above : lines.Below).Visibility = Visibility.Visible;
            e.Effects = DragDropEffects.Move;
        }

        e.Handled = true;
    }

    private void Row_Drop(object sender, DragEventArgs e)
    {
        HideDropLines();
        if (DropOf(sender, e) is not { } drop)
        {
            return;
        }

        // Where it is put among the items before it is taken out, then once taken out.
        var place = drop.Before ? drop.Target : drop.Target + 1;
        Move(drop.From, place > drop.From ? place - 1 : place);
        e.Handled = true;
    }

    /// <summary>The item dragged over a row of its own part, and whether it goes before or after it; null otherwise.</summary>
    private (int From, int Target, bool Before)? DropOf(object sender, DragEventArgs e) =>
        sender is Grid { Tag: int target } row
        && e.Data.GetData(DragFormat) is int from
        && from < _items.Count && target < _items.Count
        && _items[from].IsDone == _items[target].IsDone
            ? (from, target, e.GetPosition(row).Y < row.ActualHeight / 2)
            : null;

    private void HideDropLines()
    {
        foreach (var (above, below) in _dropLines.Values)
        {
            above.Visibility = below.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Alt+Up/Down: the item typed in takes the place of the one before or after it, in its own part.</summary>
    private void MoveNextTo(int index, TextBox box, int step)
    {
        var shownAt = _shownBoxes.IndexOf(box) + step;
        if (shownAt >= 0 && shownAt < _shownBoxes.Count && _shownBoxes[shownAt].Tag is int neighbour && _items[neighbour].IsDone == _items[index].IsDone)
        {
            Move(index, neighbour);
        }
    }

    private void Move(int from, int to)
    {
        if (from == to)
        {
            return;
        }

        _items = TaskList.Move(_items, from, to).ToList();
        Build(focusIndex: to);
        Changed?.Invoke(_items);
    }
}
