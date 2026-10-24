using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// Settings > Link types: lists the link types of the open vault, a line per way each reads; adds
/// or removes aliases (<see cref="AliasPlanner"/>), and adds, renames or removes the user's link
/// types (<see cref="LinkTypePlanner"/>), each typed in place.
/// </summary>
public partial class LinkTypesSection : UserControl
{
    private readonly TypeEditor _newType;
    private VaultSession? _session;
    private AliasEditor? _aliasEditor;
    private TypeEditor? _renameEditor;
    private LineEditor? _toFocus;
    private (string TypeId, string Status)? _removal;

    public LinkTypesSection()
    {
        InitializeComponent();
        _newType = new TypeEditor(renamed: null, PlanType);
        NewTypeLine.Content = _newType;
    }

    /// <summary>The vault whose link types are shown; set once by the Settings window.</summary>
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
        NewTypeLine.IsEnabled = graph is not null;
        if (_renameEditor is { Renamed: { } renamed })
        {
            _renameEditor.Renamed = graph?.LinkTypes.Find(renamed.Type.Id);
            _renameEditor = _renameEditor.Renamed is null ? null : _renameEditor;
        }

        LineList.ItemsSource = graph is null ? null : Lines(graph);
        _aliasEditor?.Refresh();
        _renameEditor?.Refresh();
        _newType.Refresh();
    }

    private List<object> Lines(KnowledgeGraph graph)
    {
        var lines = new List<object>();
        foreach (var type in graph.Aliases.ByType())
        {
            var custom = graph.LinkTypes.Find(type.Type.Id);
            if (custom is not null && _renameEditor?.Renamed?.Type == type.Type)
            {
                lines.Add(_renameEditor);
                continue;
            }

            AddWay(lines, new WayLine(type.Forward, IsOtherWay: false, IsBothWays: type.Reverse is null, custom, WarningOf(type.Type, custom)));
            if (type.Reverse is { } reverse)
            {
                AddWay(lines, new WayLine(reverse, IsOtherWay: true, IsBothWays: false, Custom: null, Warning: string.Empty));
            }
        }

        return lines;
    }

    private void AddWay(List<object> lines, WayLine line)
    {
        lines.Add(line);
        if (_aliasEditor?.Verb == line.Phrases.Verb)
        {
            lines.Add(_aliasEditor);
        }
    }

    private string WarningOf(LinkType type, CustomLinkType? custom) => custom switch
    {
        _ when _removal is { } removal && removal.TypeId == type.Id => removal.Status,
        { IsConflict: true } => "Renamed on two devices: rename it to settle which name to keep.",
        { TakenBy: { } other } => $"Another link type has one of its names already (added on another device): “{other.Name}” is read instead. Rename it.",
        _ => string.Empty,
    };

    private AliasPlan? PlanAlias(LinkVerb verb, string text) => Graph is { } graph ? new AliasPlanner(graph).Plan(verb, text) : null;

    private LinkTypePlan? PlanType(CustomLinkType? renamed, string name, string reverse)
    {
        if (Graph is not { } graph)
        {
            return null;
        }

        var planner = new LinkTypePlanner(graph);
        return renamed is null ? planner.Plan(name, reverse) : planner.Rename(renamed, name, reverse);
    }

    private void Edit(LineEditor editor)
    {
        _removal = null;
        _toFocus = editor;
        Show();
    }

    private void Apply(LineEditor editor)
    {
        if (_session is null)
        {
            return;
        }

        var window = Window.GetWindow(this)!;
        switch (editor)
        {
            case AliasEditor alias when alias.Plan() is { Change: { } change } plan:
                if (_session.Write(window, change, $"Alias added to {plan.Verb.Type.Id}{(plan.Verb.IsReverse ? " (reverse)" : string.Empty)}"))
                {
                    Close(alias);
                }

                break;
            case TypeEditor type when type.Plan() is { Change: { } change }:
                var action = type.IsRename ? $"Link type {change.LinkType} renamed" : $"Link type {change.LinkType} added";
                if (_session.Write(window, change, action))
                {
                    Close(type);
                }

                break;
            default:
                return;
        }

        Show();
    }

    private void Close(LineEditor editor)
    {
        if (editor == _newType)
        {
            _newType.Clear();
        }
        else if (editor == _aliasEditor)
        {
            _aliasEditor = null;
        }
        else
        {
            _renameEditor = null;
        }
    }

    private void AddAlias_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WayLine line })
        {
            _aliasEditor = new AliasEditor(line.Phrases.Verb, PlanAlias);
            Edit(_aliasEditor);
        }
    }

    private void RemoveAlias_Click(object sender, RoutedEventArgs e)
    {
        if (_session is not null && Graph is { } graph && sender is FrameworkElement { DataContext: LinkAlias alias })
        {
            _session.Write(Window.GetWindow(this)!, new AliasPlanner(graph).Remove(alias), $"Alias removed from {alias.Verb.Type.Id}");
            Show();
        }
    }

    private void RenameType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WayLine { Custom: { } custom } })
        {
            _renameEditor = new TypeEditor(custom, PlanType);
            Edit(_renameEditor);
        }
    }

    private void RemoveType_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null || Graph is not { } graph || sender is not FrameworkElement { DataContext: WayLine { Custom: { } custom } })
        {
            return;
        }

        var removal = new LinkTypePlanner(graph).Remove(custom);
        if (removal.Change is { } change)
        {
            _removal = null;
            _session.Write(Window.GetWindow(this)!, change, $"Link type {custom.Type.Id} removed");
        }
        else
        {
            _removal = (custom.Type.Id, removal.Status);
        }

        Show();
    }

    private void ApplyEditor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LineEditor editor })
        {
            Apply(editor);
        }
    }

    private void CancelEditor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: LineEditor editor })
        {
            Close(editor);
            Show();
        }
    }

    private void EditorBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LineEditor editor })
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            Apply(editor);
            e.Handled = true;
        }
        // Esc on the empty new link type is left to the window: it closes Settings.
        else if (e.Key == Key.Escape && !(editor == _newType && _newType.IsEmpty))
        {
            Close(editor);
            Show();
            e.Handled = true;
        }
    }

    // The line just opened takes the caret; the others, built again by Show, leave it where it is.
    private void EditorBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: LineEditor editor } box && editor == _toFocus)
        {
            _toFocus = null;
            box.Focus();
            box.SelectAll();
        }
    }
}
