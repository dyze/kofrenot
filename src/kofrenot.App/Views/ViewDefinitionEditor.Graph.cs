using System.Windows;
using System.Windows.Controls;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>
/// A graph's settings (<see cref="GraphDraft"/>): the role of each link type, or none when the graph
/// does not follow it; how many steps away it goes; its layout; the node types it keeps (any when
/// none is checked); the fields its cards show.
/// </summary>
public partial class ViewDefinitionEditor
{
    private static readonly (string Label, GraphRole? Role)[] Roles =
    [
        ("Not followed", null),
        ("Down: the other node below", GraphRole.Down),
        ("Up: the other node above", GraphRole.Up),
        ("Beside: on the same rank", GraphRole.Beside),
        ("Free: no rank", GraphRole.Free),
    ];

    private readonly Dictionary<string, ComboBox> _roleBoxes = [];
    private readonly Dictionary<string, CheckBox> _keepBoxes = [];

    private UIElement[] GraphRows => [FollowsLabel, FollowsPanel, DepthLabel, DepthBox, LayoutLabel, LayoutBox, KeepsLabel, KeepsPanel, FieldsLabel, FieldsBox];

    /// <summary>A row per link type, named as read from the root's side ("parent of"), and a box per node type.</summary>
    private void SetupGraph(TypeCatalog types)
    {
        DepthBox.ItemsSource = Enumerable.Range(GraphDefinition.MinDepth, GraphDefinition.MaxDepth - GraphDefinition.MinDepth + 1).ToList();
        RoleRows.Children.Clear();
        _roleBoxes.Clear();
        foreach (var type in types.LinkTypes)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock { Text = type.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            var roles = new ComboBox { ItemsSource = Roles.Select(role => role.Label).ToList(), Tag = type.Id };
            roles.SelectionChanged += RoleBox_SelectionChanged;
            Grid.SetColumn(roles, 1);
            row.Children.Add(roles);
            RoleRows.Children.Add(row);
            _roleBoxes[type.Id] = roles;
        }

        KeepsPanel.Children.Clear();
        _keepBoxes.Clear();
        foreach (var type in types.NodeTypes)
        {
            var keep = new CheckBox { Content = type.Name, Tag = type.Id, Margin = new Thickness(0, 0, 12, 4) };
            keep.Click += KeepBox_Click;
            KeepsPanel.Children.Add(keep);
            _keepBoxes[type.Id] = keep;
        }
    }

    private void ShowGraph(bool isGraph)
    {
        foreach (var element in GraphRows)
        {
            element.Visibility = isGraph ? Visibility.Visible : Visibility.Collapsed;
        }

        var graph = Draft.Graph;
        foreach (var (type, box) in _roleBoxes)
        {
            box.SelectedIndex = Array.FindIndex(Roles, role => role.Role == graph.RoleOf(type));
        }

        foreach (var (type, box) in _keepBoxes)
        {
            box.IsChecked = graph.NodeTypes.Contains(type);
        }

        DepthBox.SelectedItem = graph.Depth;
        LayoutBox.SelectedIndex = (int)graph.Layout;
    }

    private void ChangeGraph(Func<GraphDraft, GraphDraft> change) => Change(view => view with { Graph = change(view.Graph) });

    private void RoleBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { Tag: string type, SelectedIndex: >= 0 and var index })
        {
            ChangeGraph(graph => graph.WithRole(type, Roles[index].Role));
        }
    }

    private void KeepBox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: string type } box)
        {
            ChangeGraph(graph => graph.WithNodeType(type, box.IsChecked == true));
        }
    }

    private void DepthBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DepthBox.SelectedItem is int depth)
        {
            ChangeGraph(graph => graph.WithDepth(depth));
        }
    }

    private void LayoutBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LayoutBox.SelectedIndex >= 0)
        {
            ChangeGraph(graph => graph with { Layout = (GraphLayoutKind)LayoutBox.SelectedIndex });
        }
    }

    private void FieldsBox_TextChanged(object sender, TextChangedEventArgs e) => ChangeGraph(graph => graph.WithFields(FieldsBox.Text));
}
