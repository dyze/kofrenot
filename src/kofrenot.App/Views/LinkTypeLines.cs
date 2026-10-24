using System.ComponentModel;
using Kofrenot.Core.Graph;
using Kofrenot.Core.Input;

namespace Kofrenot.UI.Views;

/// <summary>
/// A way a link type reads, as a line of Settings > Link types: its name, its other built-in
/// phrases and the user's aliases. The first line of a user's link type carries it (<see cref="Custom"/>),
/// with what is wrong with it.
/// </summary>
public sealed record WayLine(VerbPhrases Phrases, bool IsOtherWay, bool IsBothWays, CustomLinkType? Custom, string Warning)
{
    public string Name => Phrases.Verb.Name;

    public string BuiltIn => string.Join(", ", Phrases.BuiltIn.Skip(1));

    public bool IsYours => Custom is not null;
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
