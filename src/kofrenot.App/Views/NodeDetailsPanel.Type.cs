using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>A node's type: a generic node can be made a note, a dataset or a view; a type conflict is resolved by keeping one.</summary>
public partial class NodeDetailsPanel
{
    /// <summary>Gives a node another type (a view asks for its definition first); false when cancelled or not written.</summary>
    public Func<NodeRetyped, bool>? Retype { get; set; }

    private void ShowType(NodeDetails details)
    {
        ChangeTypeButton.Visibility = Retype is not null && RetypePlanner.TargetsOf(details.Node).Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ConflictTypes.ItemsSource = details.TypeConflict;
        TypeConflictPanel.Visibility = details.TypeConflict.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ChangeTypeButton_Click(object sender, RoutedEventArgs e)
    {
        if (Browser.Details is not { } details)
        {
            return;
        }

        var menu = new ContextMenu { PlacementTarget = ChangeTypeButton, Placement = PlacementMode.Bottom };
        foreach (var type in RetypePlanner.TargetsOf(details.Node))
        {
            var item = new MenuItem { Header = type.Name };
            item.Click += (_, _) => ChangeType(type);
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }

    private void KeepType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NodeType type })
        {
            ChangeType(type);
        }
    }

    private void ChangeType(NodeType type)
    {
        if (Browser is { Graph: { } graph, Details: { } details }
            && new RetypePlanner(graph).Plan(details.Node, type) is { Change: { } change }
            && Retype?.Invoke(change) == true)
        {
            Show();
        }
    }
}
