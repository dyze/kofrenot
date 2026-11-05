using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Browse;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Views;

namespace Kofrenot.UI.Views;

/// <summary>A node a tree can start from, in the editor's list: its name (marked among homonyms) and type.</summary>
public sealed record RootChoice(Node Node, string Label, NodeVisual Visual);

/// <summary>What a tree's children are sorted by: their name (no field), or a qualifier of its link type.</summary>
public sealed record OrderChoice(string? Field, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Edits a <see cref="ViewDraft"/>: a tree from a node found by name (Up/Down in the name choose
/// among the nodes found, the first one by default), through a link type; a list of a node type; or a
/// graph from such a node (its settings: ViewDefinitionEditor.Graph).
/// </summary>
public partial class ViewDefinitionEditor : UserControl
{
    private TypeCatalog _types = TypeCatalog.BuiltIn;
    private Func<string, IReadOnlyList<SearchHit>> _findRoots = _ => [];
    private Func<ViewDraft, IReadOnlyList<FieldDefinition>> _orderChoices = _ => [];
    private bool _showing;

    public ViewDefinitionEditor() => InitializeComponent();

    /// <summary>The draft changed: read <see cref="Draft"/>.</summary>
    public event Action? Changed;

    public ViewDraft Draft { get; private set; } = ViewDraft.Default;

    /// <param name="findRoots">Finds the nodes a tree can start from, by name.</param>
    /// <param name="orderChoices">The qualifiers a tree can be sorted by.</param>
    public void Setup(TypeCatalog types, Func<string, IReadOnlyList<SearchHit>> findRoots, Func<ViewDraft, IReadOnlyList<FieldDefinition>> orderChoices)
    {
        _types = types;
        _findRoots = findRoots;
        _orderChoices = orderChoices;
        LinkTypeBox.ItemsSource = types.LinkTypes;
        NodeTypeBox.ItemsSource = types.NodeTypes;
        SetupGraph(types);
    }

    /// <summary>Shows a draft to edit, its root's name in the box, a graph's fields in theirs.</summary>
    public void Load(ViewDraft draft)
    {
        Draft = draft;
        _showing = true;
        RootBox.Text = draft.Root?.Name ?? string.Empty;
        FieldsBox.Text = string.Join(", ", draft.Graph.Fields);
        _showing = false;
        ShowDraft();
    }

    /// <summary>Gives the keyboard to the first field to fill: the name of the root for a tree or a graph.</summary>
    public void FocusFirst()
    {
        if (Draft.Display != ViewDisplay.Flat)
        {
            RootBox.Focus();
            RootBox.SelectAll();
        }
        else
        {
            DisplayBox.Focus();
        }
    }

    private IEnumerable<RootChoice> RootChoices => RootList.ItemsSource as IEnumerable<RootChoice> ?? [];

    private void ShowDraft()
    {
        _showing = true;
        var tree = Draft.Display == ViewDisplay.Tree;
        var flat = Draft.Display == ViewDisplay.Flat;
        DisplayBox.SelectedIndex = (int)Draft.Display;
        RootLabel.Visibility = RootPanel.Visibility = flat ? Visibility.Collapsed : Visibility.Visible;
        ThroughLabel.Visibility = LinkTypeBox.Visibility = tree ? Visibility.Visible : Visibility.Collapsed;
        NodeTypeLabel.Visibility = NodeTypeBox.Visibility = flat ? Visibility.Visible : Visibility.Collapsed;
        ShowGraph(Draft.Display == ViewDisplay.Graph);
        LinkTypeBox.SelectedItem = Draft.LinkType;
        var orders = _orderChoices(Draft).Select(field => new OrderChoice(field.Id, field.Name)).Prepend(new OrderChoice(null, "Name")).ToList();
        OrderBox.ItemsSource = orders;
        OrderBox.SelectedItem = orders.FirstOrDefault(order => order.Field == Draft.Order) ?? orders[0];
        OrderLabel.Visibility = OrderBox.Visibility = tree && orders.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        NodeTypeBox.SelectedItem = Draft.NodeType;
        RootList.SelectedItem = RootChoices.FirstOrDefault(choice => choice.Node.Id == Draft.Root?.Id);
        _showing = false;
    }

    private void Change(Func<ViewDraft, ViewDraft> change)
    {
        if (_showing)
        {
            return;
        }

        Draft = change(Draft);
        ShowDraft();
        Changed?.Invoke();
    }

    /// <summary>The nodes found for the typed name; the first one is chosen when none of them is.</summary>
    private void RootBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RootList.ItemsSource = _findRoots(RootBox.Text)
            .Select(hit => new RootChoice(hit.Node, $"{hit.Node.Name}{hit.Mark} ({_types.NodeTypeName(hit.Node.Type)})", NodeVisual.OfType(hit.Node.Type)))
            .ToList();
        Change(view => view with { Root = RootChoices.FirstOrDefault(choice => choice.Node.Id == view.Root?.Id)?.Node ?? RootChoices.FirstOrDefault()?.Node });
    }

    private void RootBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var step = e.Key switch { Key.Up => -1, Key.Down => 1, _ => 0 };
        if (step != 0 && RootList.Items.Count > 0)
        {
            RootList.SelectedIndex = Math.Clamp(RootList.SelectedIndex + step, 0, RootList.Items.Count - 1);
            RootList.ScrollIntoView(RootList.SelectedItem);
            e.Handled = true;
        }
    }

    private void RootList_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        Change(view => view with { Root = (RootList.SelectedItem as RootChoice)?.Node });

    private void DisplayBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        Change(view => view with { Display = (ViewDisplay)Math.Max(DisplayBox.SelectedIndex, 0) });

    private void LinkTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LinkTypeBox.SelectedItem is LinkType type)
        {
            Change(view => view with { LinkType = type });
        }
    }

    private void OrderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OrderBox.SelectedItem is OrderChoice order)
        {
            Change(view => view with { Order = order.Field });
        }
    }

    private void NodeTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NodeTypeBox.SelectedItem is NodeType type)
        {
            Change(view => view with { NodeType = type });
        }
    }
}
