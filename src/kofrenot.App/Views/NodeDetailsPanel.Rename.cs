using System.Windows;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// Renaming the node shown: in place (a click on the name, or F2), saved by Enter or by leaving the
/// name or the details (<see cref="LeaveName"/>); or by keeping one of the names of a conflict. A name that cannot
/// be saved (empty, a link phrase) is refused by Enter, given up when the name is left, and asked about when the details are.
/// </summary>
public partial class NodeDetailsPanel
{
    private bool _editing;

    // The node renamed: the one shown when editing started, even once another one is shown.
    private Node? _renamed;

    // Asking whether to discard the name: the dialog takes the focus, the name is not left.
    private bool _askingName;

    /// <summary>Writes a rename to the vault; false when it could not.</summary>
    public Func<NodeRenamed, bool>? Rename { get; set; }

    private RenamePlan? PlanRename(Node? node, string text) =>
        Browser.Graph is { } graph && node is not null && graph.FindNode(node.Id) is { } current ? new RenamePlanner(graph).Plan(current, text) : null;

    private void StartEditing()
    {
        if (Browser.Details is not { } details)
        {
            return;
        }

        _editing = true;
        _renamed = details.Node;
        NameBox.Text = details.Node.Name;
        ShowEditing();
        NameBox.Focus();
        NameBox.SelectAll();
    }

    /// <param name="keepFocus">Takes the keyboard back to the details (Enter, Esc); not when it went elsewhere (a click).</param>
    private void StopEditing(bool keepFocus = true)
    {
        if (!_editing)
        {
            return;
        }

        _editing = false;
        _renamed = null;
        ShowEditing();
        if (keepFocus)
        {
            Focus();
        }
    }

    private void ShowEditing()
    {
        NameText.Visibility = _editing ? Visibility.Collapsed : Visibility.Visible;
        NameBox.Visibility = RenameText.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
        Show();
    }

    private bool HandleEditKey(Key key)
    {
        switch (key)
        {
            case Key.Enter:
                if (PlanRename(_renamed, NameBox.Text) is { } plan && (plan.Problem == RenameProblem.Unchanged || Save(plan)))
                {
                    StopEditing();
                }

                return true;
            case Key.Escape:
                StopEditing();
                return true;
            default:
                return false;
        }
    }

    private bool Save(RenamePlan plan) => plan.Change is { } change && Rename?.Invoke(change) == true;

    private void NameBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        RenameText.Text = PlanRename(_renamed, NameBox.Text)?.Status ?? string.Empty;

    private void NameText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StartEditing();
        e.Handled = true;
    }

    /// <summary>Leaving the name (a click elsewhere) saves it, when it can be; Enter and Esc have stopped editing already.</summary>
    private void NameBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_editing || _askingName)
        {
            return;
        }

        if (PlanRename(_renamed, NameBox.Text) is { Problem: RenameProblem.None } plan)
        {
            Save(plan);
        }

        StopEditing(keepFocus: false);
    }

    /// <summary>
    /// The name being typed, before the details are left: saved when it can be. One that cannot is discarded when
    /// <paramref name="ask"/> and the user agrees, else kept in its box: false then, the details are not left.
    /// </summary>
    private bool LeaveName(bool ask)
    {
        if (!_editing)
        {
            return true;
        }

        var plan = PlanRename(_renamed, NameBox.Text);
        if (plan is null or { Problem: RenameProblem.None or RenameProblem.Unchanged })
        {
            if (plan is { Problem: RenameProblem.None })
            {
                Save(plan);
            }

            StopEditing(keepFocus: false);
            return true;
        }

        if (!ask)
        {
            return false;
        }

        _askingName = true;
        var discard = MessageDialog.Confirm(Window.GetWindow(this), $"Name: {plan.Remark}\n\nDiscard “{NameBox.Text.Trim()}”?", "Discard", "Keep editing");
        _askingName = false;
        if (discard)
        {
            StopEditing(keepFocus: false);
            return true;
        }

        NameBox.Focus();
        return false;
    }

    private void KeepName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string name } && PlanRename(Browser.Details?.Node, name) is { } plan)
        {
            Save(plan);
        }
    }
}
