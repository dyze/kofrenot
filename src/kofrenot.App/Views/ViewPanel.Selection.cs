using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// Several nodes selected, as in Explorer: in a tree, Ctrl+click adds or takes out a node, Shift+click
/// selects the rows shown from the last one clicked; in a list, the list's own (Extended). A plain
/// click selects one node again, on release, so a drag keeps the nodes selected. With several, only
/// a drag out acts on them (<see cref="DragOut"/>): the actions on one node are off.
/// </summary>
public partial class ViewPanel
{
    private const string MarkedBrush = "SubtleFillColorSecondaryBrush";

    // A tree's nodes selected, by key, when there are several; empty: the TreeView's own selection alone.
    private readonly HashSet<string> _marked = new(StringComparer.Ordinal);

    // Where a Shift+click range starts: the last row clicked without Shift.
    private string? _anchor;

    // True while the code moves the TreeView's selection without taking back the nodes marked.
    private bool _marking;

    // A plain click on one of several selected: it selects that one alone when released, unless dragged.
    private object? _pressedInSelection;

    /// <summary>The nodes selected, in the order shown: a tree's marked ones, or its one selected; a list's.</summary>
    private IReadOnlyList<Node> SelectedNodes => Tree.Visibility == Visibility.Visible
        ? _marked.Count > 0
            ? AllTreeItems().Where(item => _marked.Contains(((TreeItem)item.Tag).Key)).Select(item => ((TreeItem)item.Tag).Node).ToList()
            : SelectedNode is { } node ? [node] : []
        : List.SelectedItems.Cast<object>().Select(NodeOf).OfType<Node>().ToList();

    private bool HasSeveralSelected => Tree.Visibility == Visibility.Visible ? _marked.Count > 1 : List.SelectedItems.Count > 1;

    /// <summary>Enter, F2, Del, Ctrl+C, Ctrl+X, Ctrl+V: what acts on one node, off with several.</summary>
    private static bool IsOneNodeKey(Key key) => (key, Keyboard.Modifiers) switch
    {
        (Key.Enter or Key.F2 or Key.Delete, ModifierKeys.None) => true,
        (Key.C or Key.X or Key.V, ModifierKeys.Control) => true,
        _ => false,
    };

    /// <summary>Ctrl+click or Shift+click on a tree's row: marks it, or the rows up to it. False for another click.</summary>
    private bool MarkOnClick(TreeViewItem clicked)
    {
        var key = ((TreeItem)clicked.Tag).Key;
        switch (Keyboard.Modifiers)
        {
            case ModifierKeys.Control:
                if (_marked.Count == 0 && SelectedItem is { } selected)
                {
                    _marked.Add(selected.Key);
                }

                if (!_marked.Add(key))
                {
                    _marked.Remove(key);
                }

                _anchor = key;
                break;
            case ModifierKeys.Shift:
                var shown = ShownTreeItems().Select(item => ((TreeItem)item.Tag).Key).ToList();
                var from = shown.IndexOf(_anchor ?? SelectedItem?.Key ?? key);
                var to = shown.IndexOf(key);
                _marked.Clear();
                _marked.UnionWith(shown.Skip(Math.Min(from, to)).Take(Math.Abs(to - from) + 1));
                break;
            default:
                return false;
        }

        if (_marked.Count == 1)
        {
            _marked.Clear();
        }

        _marking = true;
        clicked.IsSelected = true;
        clicked.Focus();
        _marking = false;
        Log.Debug("View", "Nodes selected");
        PaintMarked();
        UpdateActions();
        return true;
    }

    /// <summary>The TreeView's selection moved (a click, the keyboard): one node selected again, unless the code marks.</summary>
    private void UnmarkUnlessMarking()
    {
        if (_marking)
        {
            return;
        }

        _anchor = SelectedItem?.Key;
        if (_marked.Count > 0)
        {
            _marked.Clear();
            PaintMarked();
        }
    }

    /// <summary>A plain press on one of several selected: kept, so they can be dragged together.</summary>
    private bool KeepSeveralOnPress(object row)
    {
        var isSelected = row switch
        {
            TreeViewItem { Tag: TreeItem item } => _marked.Contains(item.Key),
            ListBoxItem listRow => listRow.IsSelected,
            _ => false,
        };
        _pressedInSelection = Keyboard.Modifiers == ModifierKeys.None && isSelected && HasSeveralSelected ? row : null;
        return _pressedInSelection is not null;
    }

    /// <summary>Released without a drag: the row pressed is selected alone.</summary>
    private void SelectPressedAlone()
    {
        switch (_pressedInSelection)
        {
            case TreeViewItem item:
                _marked.Clear();
                PaintMarked();
                item.IsSelected = true;
                item.Focus();
                UpdateActions();
                break;
            case ListBoxItem row:
                List.SelectedItem = row;
                row.Focus();
                break;
        }

        _pressedInSelection = null;
    }

    private void Tree_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => SelectPressedAlone();

    /// <summary>Shows the marked rows as selected, the others as they are.</summary>
    private void PaintMarked()
    {
        foreach (var item in AllTreeItems())
        {
            Paint(item);
        }
    }

    private void Paint(TreeViewItem item)
    {
        if (item.Header is not Panel header || item == _dropTarget)
        {
            return;
        }

        if (_marked.Contains(((TreeItem)item.Tag).Key))
        {
            header.SetResourceReference(Panel.BackgroundProperty, MarkedBrush);
        }
        else
        {
            header.ClearValue(Panel.BackgroundProperty);
        }
    }

    /// <summary>Every row built, in the order shown, those under a collapsed node too.</summary>
    private IEnumerable<TreeViewItem> AllTreeItems() => RowsUnder(Tree.Items, collapsedToo: true);

    /// <summary>The rows shown, in order: not those under a collapsed node.</summary>
    private IEnumerable<TreeViewItem> ShownTreeItems() => RowsUnder(Tree.Items, collapsedToo: false);

    private static IEnumerable<TreeViewItem> RowsUnder(ItemCollection items, bool collapsedToo)
    {
        foreach (var item in items.OfType<TreeViewItem>())
        {
            yield return item;
            if (collapsedToo || item.IsExpanded)
            {
                foreach (var child in RowsUnder(item.Items, collapsedToo))
                {
                    yield return child;
                }
            }
        }
    }
}
