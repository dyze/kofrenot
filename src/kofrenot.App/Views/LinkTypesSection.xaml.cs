using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>A row of the list: a link type read one way, its built-in phrases, and the user's aliases.</summary>
public sealed record VerbRow(string Name, string BuiltIn, IReadOnlyList<LinkAlias> Aliases);

/// <summary>Settings > Link types: shows the aliases of the open vault, and adds or removes them (<see cref="AliasPlanner"/>).</summary>
public partial class LinkTypesSection : UserControl
{
    private VaultSession? _session;

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
            ? "Open a vault to see and edit its aliases."
            : "Type a link with any of these phrases. Your aliases are stored in the vault, so every device shares them.";
        AddPanel.IsEnabled = graph is not null;
        VerbList.ItemsSource = graph?.Aliases.ByVerb()
            .Select(verb => new VerbRow(verb.Verb.Name, string.Join(", ", verb.BuiltIn.Skip(1)), verb.Aliases))
            .ToList();
        if (graph is not null && VerbBox.ItemsSource is null)
        {
            VerbBox.ItemsSource = graph.Types.Verbs;
            VerbBox.SelectedIndex = 0;
        }

        ShowPlan();
    }

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
