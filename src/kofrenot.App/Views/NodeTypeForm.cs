using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>Raises <see cref="PropertyChanged"/> for the bindings of the node types screen.</summary>
public abstract class Bindable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            Changed(name);
        }
    }

    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>An icon offered for a node type: its name and glyph; none has an empty name.</summary>
public sealed record IconChoice(string? Name, string Glyph, string Label)
{
    public static IReadOnlyList<IconChoice> All { get; } =
        [new(null, string.Empty, "No icon"), .. TypeIcons.All.Select(icon => new IconChoice(icon, TypeGlyphs.Of(icon), icon))];
}

/// <summary>A field typed in the node type form; <see cref="Choices"/> are a choice's options, separated by commas.</summary>
public sealed class FieldRow : Bindable
{
    private readonly Action _changed;
    private string _name;
    private FieldKind _kind;
    private string _choices;

    public FieldRow(FieldDraft draft, Action changed)
    {
        Id = draft.Id;
        _name = draft.Name;
        _kind = draft.Kind;
        _choices = string.Join(", ", draft.Choices);
        _changed = changed;
    }

    public static IReadOnlyList<FieldKind> Kinds => FieldKinds.Small;

    /// <summary>The field's id; null for a field added in the form.</summary>
    public string? Id { get; }

    public string Name
    {
        get => _name;
        set => Update(ref _name, value);
    }

    public FieldKind Kind
    {
        get => _kind;
        set
        {
            Update(ref _kind, value);
            Changed(nameof(IsChoice));
        }
    }

    public bool IsChoice => Kind == FieldKind.Choice;

    public string Choices
    {
        get => _choices;
        set => Update(ref _choices, value);
    }

    public FieldDraft Draft => new(Id, Name, Kind, Choices.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    private void Update<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        Set(ref field, value, name);
        _changed();
    }
}

/// <summary>
/// The form of a node type of the user's: a new one, or one changed (<see cref="Edited"/>). Its
/// status and Save follow what is typed (<see cref="NodeTypePlanner"/>).
/// </summary>
public sealed class NodeTypeForm : Bindable
{
    private readonly Func<NodeTypeForm, NodeTypePlan?> _plan;
    private string _name;
    private IconChoice _icon;
    private NodeColor _color;
    private string _status = string.Empty;
    private bool _canApply;

    public NodeTypeForm(CustomNodeType? edited, Func<NodeTypeForm, NodeTypePlan?> plan)
    {
        Edited = edited;
        _plan = plan;
        var draft = edited is null ? new NodeTypeDraft(string.Empty, null, NodeColor.None, []) : NodeTypeDraft.Of(edited.Type);
        _name = draft.Name;
        _icon = IconChoice.All.FirstOrDefault(icon => icon.Name == draft.Icon) ?? IconChoice.All[0];
        _color = draft.Color;
        foreach (var field in draft.Fields)
        {
            Fields.Add(new FieldRow(field, Refresh));
        }

        Refresh();
    }

    public static IReadOnlyList<NodeColor> Colors { get; } = Enum.GetValues<NodeColor>();

    public CustomNodeType? Edited { get; }

    public string Title => Edited is null ? "New node type" : $"Change “{Edited.Type.Name}”";

    public ObservableCollection<FieldRow> Fields { get; } = [];

    public string Name
    {
        get => _name;
        set
        {
            Set(ref _name, value);
            Refresh();
        }
    }

    public IconChoice Icon
    {
        get => _icon;
        set
        {
            Set(ref _icon, value);
            Refresh();
        }
    }

    public NodeColor Color
    {
        get => _color;
        set
        {
            Set(ref _color, value);
            Refresh();
        }
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public bool CanApply
    {
        get => _canApply;
        private set => Set(ref _canApply, value);
    }

    public NodeTypeDraft Draft => new(Name, Icon.Name, Color, Fields.Select(row => row.Draft).ToList());

    /// <summary>The fields of the type changed that the form takes out: their values stay on its nodes, as their own.</summary>
    public IReadOnlyList<string> Removed =>
        Edited?.Type.Fields.Where(definition => Fields.All(row => row.Id != definition.Id)).Select(definition => definition.Name).ToList() ?? [];

    public NodeTypePlan? Plan() => _plan(this);

    public void AddField() => Fields.Add(new FieldRow(new FieldDraft(null, string.Empty, FieldKind.Text), Refresh));

    public void RemoveField(FieldRow row)
    {
        Fields.Remove(row);
        Refresh();
    }

    public void Refresh()
    {
        var plan = Plan();
        Status = plan?.Status ?? string.Empty;
        CanApply = plan?.CanApply ?? false;
    }
}
