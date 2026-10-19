using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>A node found by the search: its place (<see cref="TreeItem.Key"/>, or the node's id in a list), and its tree item.</summary>
internal sealed record Occurrence(string Key, Node Node, TreeItem? Item);

/// <summary>
/// The search of a view, at the right of its bar: it goes to the nodes whose name matches, in the
/// whole tree (expanding the nodes above), and hides none. While typing it stays on the node gone
/// to if it still matches, else goes to the first one; Enter (or ↓) goes to the next, Shift+Enter
/// to the previous, wrapping around. The text is never logged.
/// </summary>
public partial class ViewPanel
{
    // The place gone to, among the nodes found.
    private string? _foundKey;

    /// <summary>Ctrl+F: the keyboard to the search box.</summary>
    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    /// <summary>Esc in a view: clears the search, else forgets the node cut. False when there was neither.</summary>
    public bool HandleEscape()
    {
        if (SearchBox.Text.Length > 0 || SearchBox.IsKeyboardFocusWithin)
        {
            SearchBox.Clear();
            FocusContent();
            return true;
        }

        return CancelCut();
    }

    /// <summary>Another view shown: its search starts empty.</summary>
    private void ClearSearch()
    {
        _foundKey = null;
        SearchBox.Clear();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchPlaceholder.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        GoToFound(step: 0);
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NextFound(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.None)
        {
            // Into the view, on the node found.
            FocusContent();
            e.Handled = true;
        }
    }

    private void SearchNext_Click(object sender, RoutedEventArgs e) => NextFound(1);

    private void NextFound(int step)
    {
        Log.Info("Action", step > 0 ? "View search: next node found" : "View search: previous node found");
        GoToFound(step);
    }

    /// <summary>
    /// Goes to a node found, <paramref name="step"/> places from the one gone to (0: stays on it while
    /// it matches, else the first), and says which of how many it is.
    /// </summary>
    private void GoToFound(int step)
    {
        var found = FindTyped();
        if (found.Count == 0)
        {
            _foundKey = null;
            ShowCount(found, index: -1);
            return;
        }

        var index = IndexOfFound(found);
        index = index < 0 ? 0 : (((index + step) % found.Count) + found.Count) % found.Count;
        _foundKey = found[index].Key;
        ShowCount(found, index);
        Reveal(found[index]);
    }

    /// <summary>The view drawn again (the vault changed): the nodes found are counted again, without moving to one.</summary>
    private void RefreshFound()
    {
        var found = FindTyped();
        ShowCount(found, IndexOfFound(found));
    }

    /// <summary>"2 of 5"; "5 found" when the node gone to no longer matches; "Not found".</summary>
    /// <param name="index">The node gone to among <paramref name="found"/>, or -1.</param>
    private void ShowCount(IReadOnlyList<Occurrence> found, int index)
    {
        SearchNextButton.IsEnabled = found.Count > (index < 0 ? 0 : 1);
        SearchCount.Text = found.Count == 0 ? (IsSearchEmpty ? string.Empty : "Not found")
            : index < 0 ? $"{found.Count} found"
            : $"{index + 1} of {found.Count}";
    }

    private bool IsSearchEmpty => SearchBox.Text.Trim().Length == 0;

    // Nothing typed: nothing to walk the tree for.
    private IReadOnlyList<Occurrence> FindTyped() => IsSearchEmpty ? [] : Find(SearchBox.Text);

    private int IndexOfFound(IReadOnlyList<Occurrence> found) => found.ToList().FindIndex(occurrence => occurrence.Key == _foundKey);

    private IReadOnlyList<Occurrence> Find(string text) =>
        _definition is not null && _tree is not null ? _tree.Find(text).Select(item => new Occurrence(item.Key, item.Node, item)).ToList()
        : _list is not null ? _list.Find(text).Select(node => new Occurrence(node.Id.ToString(), node, null)).ToList()
        : [];

    /// <summary>Shows a node found, selected: in a tree, the nodes above it expanded; scrolled to.</summary>
    private void Reveal(Occurrence occurrence)
    {
        if (occurrence.Item is { } item)
        {
            foreach (var key in item.AncestorKeys)
            {
                SetExpanded(key, isExpanded: true);
            }

            _selectedPath = item.Key;
            Show(_vault, _graph!, _view!);
            Tree.UpdateLayout();
            (Tree.SelectedItem as TreeViewItem)?.BringIntoView();
        }
        else if (List.ItemsSource is IEnumerable<ViewRow> rows && rows.FirstOrDefault(row => row.Node.Id == occurrence.Node.Id) is { } row)
        {
            _selectedNode = row.Node.Id;
            List.SelectedItem = row;
            List.ScrollIntoView(row);
        }
    }
}
