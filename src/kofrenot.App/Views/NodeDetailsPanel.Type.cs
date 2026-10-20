using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// A node's type: a generic node shows it in a combo, to make it a note, a dataset or a view (asked
/// first, as it cannot be undone); a type conflict is resolved by keeping one.
/// </summary>
public partial class NodeDetailsPanel
{
    // Set while the combo is filled, so its selection is not taken as the user's choice.
    private bool _showingType;

    /// <summary>Gives a node another type (a view asks for its definition first); false when cancelled or not written.</summary>
    public Func<NodeRetyped, bool>? Retype { get; set; }

    private void ShowType(NodeDetails details)
    {
        var targets = RetypePlanner.TargetsOf(details.Node);
        var canChange = Retype is not null && targets.Count > 0;
        TypeBox.Visibility = canChange ? Visibility.Visible : Visibility.Collapsed;
        TypeText.Visibility = canChange ? Visibility.Collapsed : Visibility.Visible;
        if (canChange)
        {
            _showingType = true;
            try
            {
                TypeBox.ItemsSource = targets.Prepend(TypeCatalog.Generic).ToList();
                TypeBox.SelectedItem = TypeCatalog.Generic;
            }
            finally
            {
                _showingType = false;
            }
        }

        ConflictTypes.ItemsSource = details.TypeConflict;
        TypeConflictPanel.Visibility = details.TypeConflict.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_showingType || TypeBox.SelectedItem is not NodeType type || type == TypeCatalog.Generic || Browser.Details is not { } details)
        {
            return;
        }

        var name = type.Name.ToLowerInvariant();
        var question = $"Make “{details.Name}” a {name}?\n\nThis cannot be undone: a note, a dataset or a view stays one.";
        if (!MessageDialog.Confirm(Window.GetWindow(this), question, $"Make it a {name}", "Cancel") || !ChangeType(type))
        {
            Show();
        }
    }

    private void KeepType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NodeType type })
        {
            ChangeType(type);
        }
    }

    /// <summary>False when it is not written (refused, cancelled, or it failed).</summary>
    private bool ChangeType(NodeType type)
    {
        if (Browser is not { Graph: { } graph, Details: { } details }
            || new RetypePlanner(graph).Plan(details.Node, type) is not { Change: { } change }
            || Retype?.Invoke(change) != true)
        {
            return false;
        }

        Show();
        return true;
    }
}
