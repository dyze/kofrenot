using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>A row of the list: a link type read one way, its built-in phrases, and the user's aliases.</summary>
public sealed record VerbRow(string Name, string BuiltIn, IReadOnlyList<LinkAlias> Aliases);

/// <summary>A link type of the user's in the list: its names, and what is wrong with them.</summary>
public sealed record TypeRow(CustomLinkType Type, string Names, string Warning);

/// <summary>
/// Settings > Link types: shows the aliases and link types of the open vault, adds or removes
/// aliases (<see cref="AliasPlanner"/>), and adds, renames or removes link types (<see cref="LinkTypePlanner"/>).
/// </summary>
public partial class LinkTypesSection : UserControl
{
    private VaultSession? _session;
    private CustomLinkType? _renamed;

    public LinkTypesSection() => InitializeComponent();

    /// <summary>The vault whose aliases are shown; set once by the Settings window.</summary>
    public VaultSession Session
    {
        set
        {
            _session = value;
            Show();
        }
    }

    private KnowledgeGraph? Graph => _session?.Current?.Graph;

    private void Show()
    {
        var graph = Graph;
        StateText.Text = graph is null
            ? "Open a vault to see and edit its link types and aliases."
            : "Type a link with any of these phrases. Your link types and aliases are stored in the vault, so every device shares them.";
        AddPanel.IsEnabled = graph is not null;
        TypePanel.IsEnabled = graph is not null;
        VerbList.ItemsSource = graph?.Aliases.ByVerb()
            .Select(verb => new VerbRow(verb.Verb.Name, string.Join(", ", verb.BuiltIn.Skip(1)), verb.Aliases))
            .ToList();
        if (graph is not null)
        {
            var selected = VerbBox.SelectedItem as LinkVerb;
            VerbBox.ItemsSource = graph.Types.Verbs;
            VerbBox.SelectedIndex = selected is null ? 0 : Math.Max(0, graph.Types.Verbs.ToList().IndexOf(selected));
        }

        // Renamed from what it is now: another device may have changed it meanwhile.
        _renamed = _renamed is null ? null : graph?.LinkTypes.Find(_renamed.Type.Id);
        var types = graph?.LinkTypes.All ?? [];
        TypeList.ItemsSource = types.Select(RowOf).ToList();
        NoTypesText.Visibility = types.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowPlan();
        ShowTypePlan();
    }

    private static TypeRow RowOf(CustomLinkType type) => new(
        type,
        type.Type.Reverse is { } reverse ? $"{type.Type.Name} / {reverse.Name}" : $"{type.Type.Name} (both ways)",
        type switch
        {
            { IsConflict: true } => "Renamed on two devices: rename it to settle which name to keep.",
            { TakenBy: { } other } => $"Another link type has one of its names already (added on another device): “{other.Name}” is read instead. Rename it.",
            _ => string.Empty,
        });

    private AliasPlan? Plan() =>
        Graph is { } graph && VerbBox.SelectedItem is LinkVerb verb ? new AliasPlanner(graph).Plan(verb, PhraseBox.Text) : null;

    private void ShowPlan()
    {
        var plan = Plan();
        PlanText.Text = plan is null || plan.Problem == AliasProblem.Empty ? string.Empty : plan.Status;
        AddButton.IsEnabled = plan?.CanAdd == true;
    }

    private void Add()
    {
        if (_session is null || Plan() is not { Change: { } change } plan)
        {
            return;
        }

        if (_session.Write(Window.GetWindow(this)!, change, $"Alias added to {plan.Verb.Type.Id}{(plan.Verb.IsReverse ? " (reverse)" : string.Empty)}"))
        {
            PhraseBox.Clear();
        }

        Show();
    }

    private void RemoveAlias_Click(object sender, RoutedEventArgs e)
    {
        if (_session is not null && Graph is { } graph && sender is FrameworkElement { DataContext: LinkAlias alias })
        {
            _session.Write(Window.GetWindow(this)!, new AliasPlanner(graph).Remove(alias), $"Alias removed from {alias.Verb.Type.Id}");
            Show();
        }
    }

    private LinkTypePlan? TypePlan()
    {
        if (Graph is not { } graph)
        {
            return null;
        }

        var planner = new LinkTypePlanner(graph);
        return _renamed is null
            ? planner.Plan(TypeNameBox.Text, TypeReverseBox.Text)
            : planner.Rename(_renamed, TypeNameBox.Text, TypeReverseBox.Text);
    }

    private void ShowTypePlan()
    {
        var plan = TypePlan();
        TypeFormText.Text = _renamed is null ? "New link type" : $"Rename “{_renamed.Type.Name}”";
        TypeButton.Content = _renamed is null ? "Add" : "Rename";
        CancelRenameButton.Visibility = _renamed is null ? Visibility.Collapsed : Visibility.Visible;
        TypeReverseBox.IsEnabled = _renamed?.Type.IsSymmetric != true;
        TypePlanText.Text = plan is null || plan.Problem == LinkTypeProblem.Empty ? string.Empty : plan.Status;
        TypeButton.IsEnabled = plan?.CanApply == true;
    }

    private void ApplyType()
    {
        if (_session is null || TypePlan() is not { Change: { } change })
        {
            return;
        }

        var action = _renamed is null ? $"Link type {change.LinkType} added" : $"Link type {change.LinkType} renamed";
        if (_session.Write(Window.GetWindow(this)!, change, action))
        {
            EndForm();
        }

        Show();
    }

    private void EndForm()
    {
        _renamed = null;
        TypeNameBox.Clear();
        TypeReverseBox.Clear();
    }

    private void RenameType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TypeRow row })
        {
            _renamed = row.Type;
            TypeNameBox.Text = row.Type.Type.Name;
            TypeReverseBox.Text = row.Type.Type.Reverse?.Name ?? string.Empty;
            ShowTypePlan();
            TypeNameBox.Focus();
            TypeNameBox.SelectAll();
        }
    }

    private void RemoveType_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null || Graph is not { } graph || sender is not FrameworkElement { DataContext: TypeRow row })
        {
            return;
        }

        var removal = new LinkTypePlanner(graph).Remove(row.Type);
        if (removal.Change is { } change)
        {
            _session.Write(Window.GetWindow(this)!, change, $"Link type {row.Type.Type.Id} removed");
            if (_renamed?.Type == row.Type.Type)
            {
                EndForm();
            }

            Show();
        }
        else
        {
            TypePlanText.Text = removal.Status;
        }
    }

    private void CancelRename_Click(object sender, RoutedEventArgs e)
    {
        EndForm();
        ShowTypePlan();
    }

    private void TypeBox_TextChanged(object sender, TextChangedEventArgs e) => ShowTypePlan();

    private void TypeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyType();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _renamed is not null)
        {
            CancelRename_Click(sender, e);
            e.Handled = true;
        }
    }

    private void TypeButton_Click(object sender, RoutedEventArgs e) => ApplyType();

    private void VerbBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowPlan();

    private void PhraseBox_TextChanged(object sender, TextChangedEventArgs e) => ShowPlan();

    private void PhraseBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Add();
            e.Handled = true;
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e) => Add();
}
