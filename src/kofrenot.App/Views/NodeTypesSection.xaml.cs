using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;
using Kofrenot.Core.Logging;
using Kofrenot.Core.Session;
using Kofrenot.Core.Settings;

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

/// <summary>A node type of another vault, ticked to copy it here unless copying writes nothing.</summary>
public sealed class CopyLine(TypeCopy copy) : Bindable
{
    private bool _isChecked = copy.Change is not null;

    public TypeCopy Copy { get; } = copy;

    public string Glyph => TypeGlyphs.Of(Copy.Type.Type.Icon);

    public bool CanCopy => Copy.Change is not null;

    public bool IsChecked
    {
        get => _isChecked;
        set => Set(ref _isChecked, value);
    }
}

/// <summary>
/// Settings > Node types: lists the user's node types of the open vault, adds, changes and removes
/// them (<see cref="NodeTypePlanner"/>) in a form shown in place of the list, and copies another
/// vault's (<see cref="TypeCopyPlanner"/>).
/// </summary>
public partial class NodeTypesSection : UserControl
{
    private VaultSession? _session;
    private NodeTypeForm? _form;
    private IReadOnlyList<CopyLine>? _copies;

    public NodeTypesSection() => InitializeComponent();

    public ILog Log { get; set; } = NullLog.Instance;

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
        CopyFromButton.IsEnabled = graph is not null && OtherVaults.Count > 0;
        _copies = graph is null ? null : _copies;
        if (_form is { Edited: { } edited } && graph?.NodeTypes.Find(edited.Type.Id) is null)
        {
            _form = null;
        }

        TypeList.ItemsSource = graph?.NodeTypes.All.Select(type => new TypeLine(type)).ToList();
        ListPanel.Visibility = _form is null && _copies is null ? Visibility.Visible : Visibility.Collapsed;
        CopyPanel.Visibility = _form is null && _copies is not null ? Visibility.Visible : Visibility.Collapsed;
        CopyList.ItemsSource = _copies;
        FormPanel.Visibility = _form is null ? Visibility.Collapsed : Visibility.Visible;
        FormPanel.DataContext = _form;
        _form?.Refresh();
    }

    private IReadOnlyList<RegisteredVault> OtherVaults =>
        _session is null ? [] : _session.Vaults.Where(vault => vault.Location != _session.Selected?.Location).ToList();

    private void CopyFrom_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = CopyFromButton };
        foreach (var vault in OtherVaults)
        {
            var item = new MenuItem { Header = _session!.NameOf(vault) };
            item.Click += (_, _) => ReadTypes(vault);
            menu.Items.Add(item);
        }

        menu.IsOpen = menu.Items.Count > 0;
    }

    // Another vault's types, read (its pass phrase asked when not remembered), each with what copying it does.
    private void ReadTypes(RegisteredVault vault)
    {
        var window = Window.GetWindow(this)!;
        if (_session is null || Graph is not { } graph || _session.NodeTypesOf(new WpfQuestions(window), vault) is not { } types)
        {
            return;
        }

        var name = _session.NameOf(vault);
        if (types.Count == 0)
        {
            MessageDialog.Inform(window, $"“{name}” has no node types of its own.");
            return;
        }

        CopyTitle.Text = $"Copy from “{name}”";
        _copies = new TypeCopyPlanner(graph).Plan(types).Select(copy => new CopyLine(copy)).ToList();
        Show();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var changes = _copies?.Where(line => line.IsChecked).Select(line => line.Copy.Change).OfType<Change>().ToList() ?? [];
        if (_session is null || changes.Count == 0)
        {
            return;
        }

        if (_session.Write(new WpfQuestions(Window.GetWindow(this)!), changes, $"Node types copied ({changes.Count})"))
        {
            _copies = null;
        }

        Show();
    }

    private void CancelCopy_Click(object sender, RoutedEventArgs e)
    {
        _copies = null;
        Show();
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

    /// <summary>Settings closing: a type being changed or added is discarded when the user agrees; false keeps Settings open on it.</summary>
    public bool LeaveForm()
    {
        if (_form is not { IsChanged: true } form)
        {
            return true;
        }

        var name = form.Edited?.Type.Name ?? form.Name.Trim();
        if (!MessageDialog.Confirm(Window.GetWindow(this)!, $"Discard your changes to the node type{(name.Length > 0 ? $" “{name}”" : string.Empty)}?", "Discard", "Keep editing"))
        {
            NameBox.Focus();
            return false;
        }

        Log.Info("Action", "Node type changes discarded");
        _form = null;
        Show();
        return true;
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
