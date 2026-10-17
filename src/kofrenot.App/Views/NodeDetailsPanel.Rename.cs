using System.Windows;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>Renaming the node shown: in place (F2 or ✎), or by keeping one of the names of a conflict.</summary>
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

    private void StopEditing()
    {
        if (!_editing)
        {
            return;
        }

        _editing = false;
        ShowEditing();
        Focus();
    }

    private void ShowEditing()
    {
        NameText.Visibility = _editing ? Visibility.Collapsed : Visibility.Visible;
        NameBox.Visibility = RenameText.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
        EditButton.Visibility = _editing ? Visibility.Hidden : Visibility.Visible;
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

    private void EditButton_Click(object sender, RoutedEventArgs e) => StartEditing();

    private void KeepName_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: string name } && PlanRename(name) is { } plan)
        {
            Save(plan);
        }
    }
}
