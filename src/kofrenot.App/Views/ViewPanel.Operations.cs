using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// The operations of a tree: copy or cut a node (Ctrl+C, Ctrl+X), paste it under the selected one
/// (Ctrl+V: a copy, or a move), delete it with its subtree (Del); also in a right-click menu. A copy
/// and a deletion are shown first; the vault is written through the session.
/// </summary>
public partial class ViewPanel
{
    private (TreeItem Item, bool IsCut)? _taken;

    public ILog Log { get; set; } = NullLog.Instance;

    /// <summary>Writes changes, logging the action: false when they are not written (the user is told).</summary>
    public Func<IReadOnlyList<Change>, string, bool>? Write { get; set; }

    /// <summary>Writes a copy, its content off the UI thread: false when it is not written (the user is told).</summary>
    public Func<CopyPlan, Task<bool>>? Copy { get; set; }

    /// <summary>Whether a note's or dataset's version has its file in the vault.</summary>
    public Func<ContentEdited, bool>? ContentExists { get; set; }

    private TreeItem? SelectedItem => (Tree.SelectedItem as TreeViewItem)?.Tag as TreeItem;

    private Window Owner => Window.GetWindow(this)!;

    private bool HandleTreeKey(Key key) => (key, Keyboard.Modifiers) switch
    {
        (Key.C, ModifierKeys.Control) => Take(isCut: false),
        (Key.X, ModifierKeys.Control) => Take(isCut: true),
        (Key.V, ModifierKeys.Control) => Paste(),
        (Key.Delete, ModifierKeys.None) => Delete(),
        _ => false,
    };

    private bool Take(bool isCut)
    {
        if (SelectedItem is not { } item)
        {
            return false;
        }

        _taken = (item, isCut);
        Log.Debug("View", isCut ? "Node cut" : "Node copied");
        return true;
    }

    /// <summary>Pastes the node taken under the selected one: moved when it was cut, copied otherwise.</summary>
    private bool Paste()
    {
        if (_taken is not { } taken || SelectedItem is not { } target || _graph is null || _definition is null)
        {
            return false;
        }

        SetExpanded(target.Key, isExpanded: true);
        _selectedPath = target.Key;
        if (taken.IsCut)
        {
            Move(taken.Item, target.Node);
        }
        else
        {
            _ = CopyUnder(taken.Item.Node, target.Node);
        }

        return true;
    }

    private void Move(TreeItem item, Node target)
    {
        var plan = new MovePlanner(_graph!, _definition!.LinkType).Plan(item, target);
        if (OperationPreview.Refusal(plan.Problem) is { } refusal)
        {
            MessageDialog.Inform(Owner, refusal);
        }
        else if (Write?.Invoke(plan.Changes, "Node moved") == true)
        {
            _taken = null;
        }

        FocusContent();
    }

    private async Task CopyUnder(Node node, Node target)
    {
        var plan = new CopyPlanner(_graph!, _definition!.LinkType).Plan(node, target, version => ContentExists?.Invoke(version) ?? false);
        var preview = OperationPreview.Copy(plan, target, _graph!.Types);
        if (!preview.CanProceed)
        {
            MessageDialog.Inform(Owner, preview.Summary, preview.Lines);
        }
        else if (MessageDialog.Confirm(Owner, preview.Summary, "Copy", lines: preview.Lines) && Copy is not null)
        {
            await Copy(plan);
        }

        FocusContent();
    }

    /// <summary>Deletes the selected node with its subtree, once the preview is confirmed.</summary>
    private bool Delete()
    {
        if (SelectedItem is not { } item || _graph is null || _definition is null)
        {
            return false;
        }

        var plan = new DeletePlanner(_graph, _definition.LinkType).Plan(item);
        var preview = OperationPreview.Delete(plan, _graph.Types);
        if (MessageDialog.Confirm(Owner, preview.Summary, "Delete", lines: preview.Lines))
        {
            _selectedPath = item.ParentKey;
            if (Write?.Invoke([plan.Change], $"Nodes deleted ({plan.Nodes.Count})") == true && _taken is { } taken && plan.Nodes.Contains(taken.Item.Node))
            {
                _taken = null;
            }
        }

        FocusContent();
        return true;
    }

    /// <summary>A right click selects the node under the mouse, for its menu.</summary>
    private void Tree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (TreeViewItemAt(e.OriginalSource) is { } item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    /// <summary>The tree's menu: the operations of the node selected; none without one.</summary>
    private void Tree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var menu = Tree.ContextMenu!;
        menu.Items.Clear();
        if (SelectedItem is null)
        {
            e.Handled = true;
            return;
        }

        menu.Items.Add(MenuItemOf("Copy", "Ctrl+C", () => Take(isCut: false)));
        menu.Items.Add(MenuItemOf("Cut", "Ctrl+X", () => Take(isCut: true)));
        menu.Items.Add(MenuItemOf(_taken is { IsCut: true } ? "Move here" : "Paste a copy here", "Ctrl+V", () => Paste(), isEnabled: _taken is not null));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItemOf("Delete…", "Del", () => Delete()));
    }

    private static MenuItem MenuItemOf(string header, string keys, Action action, bool isEnabled = true)
    {
        var item = new MenuItem { Header = header, InputGestureText = keys, IsEnabled = isEnabled };
        item.Click += (_, _) => action();
        return item;
    }
}
