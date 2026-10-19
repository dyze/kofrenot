using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;

namespace Kofrenot.UI.Views;

/// <summary>
/// Renaming the node selected in place (F2, or Rename): its name becomes a box, saved by Enter or
/// by leaving it, as in the details (<see cref="RenamePlanner"/>: an empty name or a link phrase is
/// refused by Enter, the reason under the box, and given up when the box is left). Esc cancels.
/// A redraw of the view (a change of the vault) drops the box.
/// </summary>
public partial class ViewPanel
{
    // While a name is edited: the box, in place of the name in the row of the node.
    private (TextBox Box, TextBlock Name, Node Node)? _renaming;
    private ToolTip? _renameProblem;

    /// <summary>Writes a rename to the vault; false when it could not (the user is told).</summary>
    public Func<NodeRenamed, bool>? Rename { get; set; }

    private bool IsRenaming => _renaming is not null;

    /// <summary>F2, Rename: the name of the node selected becomes a box, all selected. False without one.</summary>
    private bool StartRename()
    {
        var header = Tree.Visibility == Visibility.Visible
            ? (Tree.SelectedItem as TreeViewItem)?.Header
            : (List.SelectedItem as ListBoxItem)?.Content;
        if (IsRenaming || SelectedNode is not { } node || NamePanelOf(header) is not { Children: [.., TextBlock name] } row)
        {
            return false;
        }

        var box = new TextBox { Text = node.Name, MinWidth = 220, VerticalAlignment = VerticalAlignment.Center };
        box.PreviewKeyDown += (_, e) => RenameBox_PreviewKeyDown(box, e);
        box.LostKeyboardFocus += (_, _) => RenameBox_LostKeyboardFocus(box);
        box.TextChanged += (_, _) => ShowRenameProblem(null);
        name.Visibility = Visibility.Collapsed;
        row.Children.Add(box);
        _renaming = (box, name, node);
        Log.Debug("View", "Renaming in place");
        box.Focus();
        box.SelectAll();
        return true;
    }

    /// <summary>Esc while renaming: the old name stays. False when no name was edited.</summary>
    private bool CancelRename()
    {
        if (!IsRenaming)
        {
            return false;
        }

        StopRename();
        FocusContent();
        return true;
    }

    /// <summary>The box gives the name back to the row (both may be gone already: a redraw).</summary>
    private void StopRename()
    {
        if (_renaming is not { } renaming)
        {
            return;
        }

        _renaming = null;
        ShowRenameProblem(null);
        if (renaming.Box.Parent is Panel row)
        {
            row.Children.Remove(renaming.Box);
        }

        renaming.Name.Visibility = Visibility.Visible;
    }

    private void RenameBox_PreviewKeyDown(TextBox box, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _renaming is not { } renaming || renaming.Box != box || _graph is null)
        {
            return;
        }

        e.Handled = true;
        var plan = new RenamePlanner(_graph).Plan(renaming.Node, box.Text);
        if (plan.Problem is RenameProblem.Empty or RenameProblem.LinkPhrase)
        {
            ShowRenameProblem(plan.Status);
            return;
        }

        StopRename();
        SaveRename(plan);
        FocusContent();
    }

    /// <summary>Leaving the box (a click elsewhere) saves the name when it can be; else the old one stays.</summary>
    private void RenameBox_LostKeyboardFocus(TextBox box)
    {
        if (_renaming is not { } renaming || renaming.Box != box || _graph is null)
        {
            return;
        }

        StopRename();
        SaveRename(new RenamePlanner(_graph).Plan(renaming.Node, box.Text));
    }

    private void SaveRename(RenamePlan plan)
    {
        if (plan is { Problem: RenameProblem.None, Change: { } change })
        {
            Rename?.Invoke(change);
        }
    }

    /// <summary>Why Enter did not rename, under the box, its edge marked; null takes it away.</summary>
    private void ShowRenameProblem(string? problem)
    {
        if (_renameProblem is not null)
        {
            _renameProblem.IsOpen = false;
            _renameProblem = null;
        }

        if (_renaming is not { Box: var box })
        {
            return;
        }

        if (problem is null)
        {
            box.ClearValue(Control.BorderBrushProperty);
            return;
        }

        box.SetResourceReference(Control.BorderBrushProperty, "SystemFillColorCriticalBrush");
        _renameProblem = new ToolTip { Content = problem, PlacementTarget = box, Placement = PlacementMode.Bottom, IsOpen = true };
    }
}
