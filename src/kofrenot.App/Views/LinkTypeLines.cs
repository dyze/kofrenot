using System.Collections.ObjectModel;
using System.ComponentModel;
using Kofrenot.Core.Fields;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// A way a link type reads, as a line of Settings > Link types: its name, its other built-in
/// phrases and the user's aliases. The first line of a user's link type carries it (<see cref="Custom"/>),
/// with what is wrong with it. The first line of every link type, built-in or not, names its qualifiers.
/// </summary>
public sealed record WayLine(VerbPhrases Phrases, bool IsOtherWay, bool IsBothWays, CustomLinkType? Custom, string Warning, IReadOnlyList<FieldDefinition>? Fields = null)
{
    public string Name => Phrases.Verb.Name;

    public string BuiltIn => string.Join(", ", Phrases.BuiltIn.Skip(1));

    public bool IsYours => Custom is not null;

    /// <summary>The first line of a type: its qualifiers are changed from it.</summary>
    public bool HasQualifiers => !IsOtherWay;

    /// <summary>"Qualifiers: Date, Source", or nothing.</summary>
    public string QualifierNames => Fields is { Count: > 0 } fields ? $"Qualifiers: {string.Join(", ", fields.Select(qualifier => qualifier.Name))}" : string.Empty;
}

/// <summary>A line being typed in: its plan is shown as it changes, and applied by Enter or its button.</summary>
public abstract class LineEditor : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>What applying does, or why it cannot.</summary>
    public string Status { get; private set; } = string.Empty;

    public bool CanApply { get; private set; }

    /// <summary>Plans again from the text typed: after a keystroke, or once the vault changed.</summary>
    public abstract void Refresh();

    protected void Show(string status, bool canApply)
    {
        Status = status;
        CanApply = canApply;
        Notify(nameof(Status));
        Notify(nameof(CanApply));
    }

    protected void Notify(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

/// <summary>A new alias of one way of a link type, typed under that way's line.</summary>
public sealed class AliasEditor(LinkVerb verb, Func<LinkVerb, string, AliasPlan?> plan) : LineEditor
{
    private string _text = string.Empty;

    public LinkVerb Verb => verb;

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            Refresh();
        }
    }

    public AliasPlan? Plan() => plan(verb, _text);

    public override void Refresh()
    {
        var current = Plan();
        Show(current is null || current.Problem == AliasProblem.Empty ? string.Empty : current.Status, current?.CanAdd == true);
    }
}

/// <summary>
/// A new link type, at the end of the list, or new names for one of the user's (<see cref="Renamed"/>),
/// typed in place of its lines: its name, and how it reads the other way (empty: the same both ways).
/// </summary>
public sealed class TypeEditor(CustomLinkType? renamed, Func<CustomLinkType?, string, string, LinkTypePlan?> plan) : LineEditor
{
    private string _name = renamed?.Type.Name ?? string.Empty;
    private string _reverse = renamed?.Type.Reverse?.Name ?? string.Empty;

    /// <summary>Found again after each change: another device may have renamed it meanwhile.</summary>
    public CustomLinkType? Renamed { get; set; } = renamed;

    public bool IsRename => Renamed is not null;

    public bool IsEmpty => _name.Length == 0 && _reverse.Length == 0;

    public string ApplyText => IsRename ? "Rename" : "Add";

    /// <summary>A rename keeps the way it reads: a link type read the same both ways stays so.</summary>
    public bool CanTypeReverse => Renamed?.Type.IsSymmetric != true;

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            Refresh();
        }
    }

    public string Reverse
    {
        get => _reverse;
        set
        {
            _reverse = value;
            Refresh();
        }
    }

    public LinkTypePlan? Plan() => plan(Renamed, _name, _reverse);

    public void Clear()
    {
        _name = string.Empty;
        _reverse = string.Empty;
        Notify(nameof(Name));
        Notify(nameof(Reverse));
        Refresh();
    }

    public override void Refresh()
    {
        var current = Plan();
        Show(current is null || current.Problem == LinkTypeProblem.Empty ? string.Empty : current.Status, current?.CanApply == true);
    }
}

/// <summary>
/// The qualifiers of a link type, changed under its first line: a field list as a node type's
/// (<see cref="FieldRow"/>), saved at once (<see cref="LinkTypePlanner.Qualifiers"/>).
/// </summary>
public sealed class QualifiersEditor : LineEditor
{
    private readonly Func<LinkType, IReadOnlyList<FieldDraft>, QualifierListPlan?> _plan;

    public QualifiersEditor(LinkType type, IReadOnlyList<FieldDefinition> fields, Func<LinkType, IReadOnlyList<FieldDraft>, QualifierListPlan?> plan)
    {
        Type = type;
        Start = fields;
        _plan = plan;
        foreach (var field in fields)
        {
            Fields.Add(new FieldRow(FieldDraft.Of(field), Refresh));
        }

        Refresh();
    }

    public LinkType Type { get; }

    /// <summary>The qualifiers when the editor opened: those taken out are asked about.</summary>
    public IReadOnlyList<FieldDefinition> Start { get; }

    public ObservableCollection<FieldRow> Fields { get; } = [];

    public string Title => $"Qualifiers of “{Type.Name}”";

    /// <summary>The qualifiers taken out: their values stay on the links, as their own.</summary>
    public IReadOnlyList<string> Removed => Start.Where(qualifier => Fields.All(row => row.Id != qualifier.Id)).Select(qualifier => qualifier.Name).ToList();

    public QualifierListPlan? Plan() => _plan(Type, Fields.Select(row => row.Draft).ToList());

    public void AddField()
    {
        Fields.Add(new FieldRow(new FieldDraft(null, string.Empty, FieldKind.Text), Refresh));
        Refresh();
    }

    public void RemoveField(FieldRow row)
    {
        Fields.Remove(row);
        Refresh();
    }

    public override void Refresh()
    {
        var current = Plan();
        Show(current?.Status ?? string.Empty, current?.CanApply == true);
    }
}
