using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>A type shown in a node's details: its glyph, and whether × takes it out.</summary>
public sealed record TypeChip(NodeType Type, string Glyph, bool CanRemove);

/// <summary>
/// A node's types, as chips: × takes one out, always asked first; Add type… adds one
/// (<see cref="TypeSetPlanner"/>), asked first when it hides content (a note's text, a dataset's
/// file, what a view shows: shown again with the type back).
/// </summary>
public partial class NodeDetailsPanel
{
    /// <summary>Adds a type to a node, or takes one out (a view asks for its definition first); false when cancelled or not written.</summary>
    public Func<TypeSetPlan, bool>? ChangeTypes { get; set; }

    private void ShowType(NodeDetails details)
    {
        var canChange = ChangeTypes is not null;
        TypesLabel.Text = details.Types.Count > 1 ? "Types" : "Type";
        TypeChips.ItemsSource = details.Types
            .Select(type => new TypeChip(type, TypeGlyphs.Of(type.Icon), canChange && type != TypeCatalog.Generic))
            .ToList();
        AddTypeButton.Visibility = canChange ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AddType_Click(object sender, RoutedEventArgs e)
    {
        if (Browser is not { Graph: { } graph, Details: { } details })
        {
            return;
        }

        var menu = new ContextMenu { PlacementTarget = AddTypeButton };
        foreach (var type in graph.Types.NodeTypes.Where(type => type != TypeCatalog.Generic && !graph.HasType(details.Node.Id, type.Id)))
        {
            var item = new MenuItem { Header = type.Name };
            var name = type.Name.ToLowerInvariant();
            item.Click += (_, _) => Change(new TypeSetPlanner(graph).Add(details.Node, type), $"Make “{details.Name}” a {name}?", $"Make it a {name}", alwaysAsk: false);
            menu.Items.Add(item);
        }

        menu.IsOpen = menu.Items.Count > 0;
    }

    private void RemoveType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TypeChip chip } && Browser is { Graph: { } graph, Details: { } details })
        {
            var kept = details.Fields.Any(field => field.Type == chip.Type && field.Value is not null) ? "\n\nIts fields with a value stay, as its own." : string.Empty;
            Change(new TypeSetPlanner(graph).Remove(details.Node, chip.Type), $"Take the type {chip.Type.Name} out of “{details.Name}”?{kept}", "Take it out", alwaysAsk: true);
        }
    }

    /// <summary>Writes the plan, asked first when it hides content or <paramref name="alwaysAsk"/>; shown again either way.</summary>
    private void Change(TypeSetPlan plan, string question, string confirm, bool alwaysAsk)
    {
        var owner = Window.GetWindow(this);
        if (plan.Hidden is not null && DatasetBox.IsImporting(plan.Node.Id))
        {
            MessageDialog.Inform(owner, "A file is being imported into this dataset: change its type once it is done.");
        }
        else if ((plan.Hidden is null && !alwaysAsk)
            || MessageDialog.Confirm(owner, plan.Hidden is null ? question : $"{question}\n\n{plan.Hidden}", confirm, "Cancel"))
        {
            ChangeTypes?.Invoke(plan);
        }

        Show();
    }
}
