using System.Windows;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// Renaming the node shown: in place (a click on the name, or F2), saved by Enter or by leaving the
/// name; or by keeping one of the names of a conflict. A name that cannot be saved (empty, a link
/// phrase) is refused by Enter, and given up when the name is left.
/// </summary>
public partial class NodeDetailsPanel
{
    private bool _editing;

    /// <summary>Writes a rename to the vault; false when it could not.</summary>
    public Func<NodeRenamed, bool>? Rename { get; set; }

    private RenamePlan? PlanRename(string text) =>
        Browser is { Graph: { } graph, Details: { } details } ? new RenamePlanner(graph).Plan(details.Node, text) : null;

    private void StartEditing()
    {
        if (Browser.Details is not { } details)
        {
            return;
        }

        _editing = true;
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
                if (PlanRename(NameBox.Text) is { } plan && (plan.Problem == RenameProblem.Unchanged || Save(plan)))
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
        RenameText.Text = PlanRename(NameBox.Text)?.Status ?? string.Empty;

    private void NameText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        StartEditing();
        e.Handled = true;
    }

    /// <summary>Leaving the name (a click elsewhere) saves it, when it can be; Enter and Esc have stopped editing already.</summary>
    private void NameBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_editing)
        {
            return;
        }

        if (PlanRename(NameBox.Text) is { Problem: RenameProblem.None } plan)
        {
            Save(plan);
        }

        StopEditing(keepFocus: false);
    }

    private void KeepName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string name } && PlanRename(name) is { } plan)
        {
            Save(plan);
        }
    }
}
