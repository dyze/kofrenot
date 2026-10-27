using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Session;

namespace Kofrenot.UI.Views;

/// <summary>A node type of the user's on the node types screen: its glyph, its colour's dot and its fields.</summary>
public sealed record TypeLine(CustomNodeType Type)
{
    public string Glyph => TypeGlyphs.Of(Type.Type.Icon);

    public Brush? Fill => NoteColors.Fill(Type.Type.Color);

    public bool HasColor => Fill is not null;

    public string Fields => Type.Type.Fields.Count == 0
        ? "No field"
        : string.Join(", ", Type.Type.Fields.Select(definition => $"{definition.Name} ({FieldKinds.NameOf(definition.Kind)})"));
}

/// <summary>
/// Settings > Node types: lists the user's node types of the open vault, adds, changes and removes
/// them (<see cref="NodeTypePlanner"/>) in a form shown in place of the list.
/// </summary>
public partial class NodeTypesSection : UserControl
{
    private VaultSession? _session;
    private NodeTypeForm? _form;

    public NodeTypesSection() => InitializeComponent();

    /// <summary>The vault whose node types are shown; set by the Settings window.</summary>
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
            ? "Open a vault to see and edit its node types."
            : "A node type is a name, an icon, a colour and fields: its nodes show its fields in their details, to fill. "
              + "Built in: Generic, Note, Dataset and View. Your node types are stored in the vault, so every device shares them.";
        NewTypeButton.IsEnabled = graph is not null;
        if (_form is { Edited: { } edited } && graph?.NodeTypes.Find(edited.Type.Id) is null)
        {
            _form = null;
        }

        TypeList.ItemsSource = graph?.NodeTypes.All.Select(type => new TypeLine(type)).ToList();
        ListPanel.Visibility = _form is null ? Visibility.Visible : Visibility.Collapsed;
        FormPanel.Visibility = _form is null ? Visibility.Collapsed : Visibility.Visible;
        FormPanel.DataContext = _form;
        _form?.Refresh();
    }

    private NodeTypePlan? PlanType(NodeTypeForm form)
    {
        if (Graph is not { } graph)
        {
            return null;
        }

        var planner = new NodeTypePlanner(graph);
        var current = form.Edited is { } edited ? graph.NodeTypes.Find(edited.Type.Id) : null;
        return current is null ? planner.Plan(form.Draft) : planner.Edit(current, form.Draft);
    }

    private void Open(NodeTypeForm form)
    {
        _form = form;
        Show();
        Dispatcher.BeginInvoke(() => NameBox.Focus(), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void NewType_Click(object sender, RoutedEventArgs e) => Open(new NodeTypeForm(edited: null, PlanType));

    private void EditType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TypeLine line })
        {
            Open(new NodeTypeForm(line.Type, PlanType));
        }
    }

    private void RemoveType_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null || Graph is not { } graph || sender is not FrameworkElement { DataContext: TypeLine line })
        {
            return;
        }

        var removal = new NodeTypePlanner(graph).Remove(line.Type);
        var window = Window.GetWindow(this)!;
        if (MessageDialog.Confirm(window, removal.Question, "Remove"))
        {
            _session.Write(new WpfQuestions(window), removal.Change, $"Node type {line.Type.Type.Id} removed");
            Show();
        }
    }

    private void AddField_Click(object sender, RoutedEventArgs e) => _form?.AddField();

    private void RemoveField_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FieldRow row })
        {
            _form?.RemoveField(row);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Save();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _form = null;
        Show();
    }

    private void Save()
    {
        if (_session is null || _form?.Plan() is not { Change: { } change } plan)
        {
            return;
        }

        var window = Window.GetWindow(this)!;
        if (_form.Removed is { Count: > 0 } removed
            && !MessageDialog.Confirm(window, $"Take {string.Join(", ", removed.Select(name => $"“{name}”"))} out of “{plan.Name}”?\n\nIts nodes keep their values, as their own fields.", "Take out"))
        {
            return;
        }

        var action = plan.Edited is null ? $"Node type {change.NodeType} added" : $"Node type {change.NodeType} changed";
        if (_session.Write(new WpfQuestions(window), change, action))
        {
            _form = null;
        }

        Show();
    }

    private void FormBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Save();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _form = null;
            Show();
            e.Handled = true;
        }
    }
}
