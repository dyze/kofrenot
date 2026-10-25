using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// A node's type, in a combo to give it another: asked first, saying what it would hide (a note's
/// text, a dataset's file, what a view shows: shown again with the type back). A type conflict is
/// resolved by keeping one.
/// </summary>
public partial class NodeDetailsPanel
{
    // Set while the combo is filled, so its selection is not taken as the user's choice.
    private bool _showingType;

    /// <summary>Gives a node another type (a view asks for its definition first); false when cancelled or not written.</summary>
    public Func<NodeRetyped, bool>? Retype { get; set; }

    private void ShowType(NodeDetails details)
    {
        var current = TypeCatalog.BuiltIn.FindNodeType(details.Node.Type);
        var canChange = Retype is not null && current is not null;
        TypeBox.Visibility = canChange ? Visibility.Visible : Visibility.Collapsed;
        TypeText.Visibility = canChange ? Visibility.Collapsed : Visibility.Visible;
        if (canChange)
        {
            _showingType = true;
            try
            {
                TypeBox.ItemsSource = TypeCatalog.BuiltIn.NodeTypes;
                TypeBox.SelectedItem = current;
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
        if (_showingType || TypeBox.SelectedItem is not NodeType type || Browser is not { Graph: { } graph, Details: { } details } || type.Id == details.Node.Type)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        if (DatasetBox.IsImporting(details.Node.Id))
        {
            MessageDialog.Inform(owner, "A file is being imported into this dataset: change its type once it is done.");
            Show();
            return;
        }

        var name = type.Name.ToLowerInvariant();
        var hidden = new RetypePlanner(graph).Plan(details.Node, type).Hidden;
        var question = $"Make “{details.Name}” a {name}?" + (hidden is null ? string.Empty : $"\n\n{hidden}");
        if (!MessageDialog.Confirm(owner, question, $"Make it a {name}", "Cancel") || !ChangeType(type))
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
