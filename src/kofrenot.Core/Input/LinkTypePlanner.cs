using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>Why a link type cannot be added or renamed.</summary>
public enum LinkTypeProblem
{
    None,

    /// <summary>No name typed.</summary>
    Empty,

    /// <summary>The same name both ways: a link that reads the same both ways has no reverse name.</summary>
    SameBothWays,

    /// <summary>A rename would change which way it reads: its links would then read the wrong way.</summary>
    DirectionFixed,

    /// <summary>A name is a phrase of a link type already (<see cref="LinkTypePlan.TakenBy"/>).</summary>
    Taken,

    /// <summary>A rename to the names it has.</summary>
    Unchanged,
}

/// <summary>
/// Adds a link type named <see cref="Name"/>, read <see cref="ReverseName"/> the other way (none:
/// the same both ways), or renames <see cref="Renamed"/>. <see cref="Hidden"/> are the nodes whose
/// name contains one of its names: the input box would read them as links.
/// </summary>
public sealed record LinkTypePlan(
    CustomLinkType? Renamed,
    string Name,
    string? ReverseName,
    LinkTypeProblem Problem,
    string? Taken,
    LinkVerb? TakenBy,
    IReadOnlyList<Node> Hidden,
    LinkTypeDefined? Change)
{
    public bool CanApply => Change is not null;

    /// <summary>What applying does, or why it cannot.</summary>
    public string Status => Problem switch
    {
        LinkTypeProblem.Empty => "Type a name.",
        LinkTypeProblem.SameBothWays => "Type another name for the other way, or none for a link that reads the same both ways.",
        LinkTypeProblem.DirectionFixed when ReverseName is null => "Type how it reads the other way: links already made read both ways.",
        LinkTypeProblem.DirectionFixed => "Leave the other way empty: links already made read the same both ways.",
        LinkTypeProblem.Taken => $"“{Taken}” is a phrase of “{TakenBy!.Name}” already.",
        LinkTypeProblem.Unchanged => string.Empty,
        _ => Described + HiddenWarning,
    };

    private string Described =>
        (Renamed is null ? $"Adds “{Name}”" : $"Renames “{Renamed.Type.Name}” to “{Name}”")
        + (ReverseName is null ? ", read the same both ways." : $", read “{ReverseName}” the other way.");

    private string HiddenWarning => Hidden.Count switch
    {
        0 => string.Empty,
        1 => " 1 node name contains it: the input box could no longer find it.",
        _ => $" {Hidden.Count} node names contain it: the input box could no longer find them.",
    };
}

/// <summary>Removes <see cref="Type"/> with its aliases, unless links still have it (<see cref="Links"/>).</summary>
public sealed record LinkTypeRemoval(CustomLinkType Type, int Links, ChangesRemoved? Change)
{
    public bool CanRemove => Change is not null;

    /// <summary>What removing does, or why it cannot.</summary>
    public string Status => Links switch
    {
        0 => $"Removes “{Type.Type.Name}” and its aliases.",
        1 => $"1 link is “{Type.Type.Name}”: change its type first.",
        _ => $"{Links} links are “{Type.Type.Name}”: change their type first.",
    };
}

/// <summary>Turns names into the change adding or renaming a link type of the user's, and a link type into the change removing it.</summary>
public sealed class LinkTypePlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>A new link type; <paramref name="reverseText"/> empty for one that reads the same both ways.</summary>
    public LinkTypePlan Plan(string nameText, string? reverseText) => Plan(renamed: null, nameText, reverseText);

    /// <summary>New names for a link type of the user's; it keeps the way it reads.</summary>
    public LinkTypePlan Rename(CustomLinkType renamed, string nameText, string? reverseText) => Plan(renamed, nameText, reverseText);

    public LinkTypeRemoval Remove(CustomLinkType type)
    {
        var links = graph.Links.Count(link => link.Type == type.Type.Id && graph.FindNode(link.From) is not null && graph.FindNode(link.To) is not null);
        if (links > 0)
        {
            return new LinkTypeRemoval(type, links, Change: null);
        }

        var aliases = graph.Aliases.All.Where(alias => alias.Verb.Type == type.Type).Select(alias => alias.Id);
        return new LinkTypeRemoval(type, 0, new ChangesRemoved(Guid.NewGuid(), _clock.GetUtcNow(), [.. type.Definitions, .. aliases]));
    }

    private LinkTypePlan Plan(CustomLinkType? renamed, string nameText, string? reverseText)
    {
        var name = Phrase(nameText);
        var reverse = Phrase(reverseText ?? string.Empty) is { Length: > 0 } typed ? typed : null;
        LinkTypePlan Refused(LinkTypeProblem problem, string? taken = null, LinkVerb? takenBy = null) =>
            new(renamed, name, reverse, problem, taken, takenBy, Hidden: [], Change: null);

        if (name.Length == 0)
        {
            return Refused(LinkTypeProblem.Empty);
        }

        if (reverse is not null && NameKey.Of(reverse) == NameKey.Of(name))
        {
            return Refused(LinkTypeProblem.SameBothWays);
        }

        if (renamed is not null && renamed.Type.IsSymmetric != (reverse is null))
        {
            return Refused(LinkTypeProblem.DirectionFixed);
        }

        if (renamed is not null && name == renamed.Type.Name && reverse == renamed.Type.Reverse?.Name && !renamed.IsConflict)
        {
            return Refused(LinkTypeProblem.Unchanged);
        }

        var others = graph.Types.Verbs.Where(verb => verb.Type != renamed?.Type).ToList();
        foreach (var phrase in reverse is null ? [name] : new[] { name, reverse })
        {
            if (others.FirstOrDefault(verb => verb.Phrases.Any(taken => NameKey.Of(taken) == NameKey.Of(phrase))) is { } takenBy)
            {
                return Refused(LinkTypeProblem.Taken, phrase, takenBy);
            }
        }

        var id = Guid.NewGuid();
        var alone = new TypeCatalog([], [new LinkType("new", [name], reverse is null ? null : [reverse])]);
        var hidden = graph.Nodes.Where(node => InputParser.Parse(node.Name, alone) is LinkRequest).ToList();
        var change = new LinkTypeDefined(
            id,
            _clock.GetUtcNow(),
            renamed?.Type.Id ?? id.ToString(),
            name,
            reverse,
            renamed?.Names.Select(current => current.Id).ToList() ?? []);
        return new LinkTypePlan(renamed, name, reverse, LinkTypeProblem.None, Taken: null, TakenBy: null, hidden, change);
    }

    private static string Phrase(string text) => string.Join(' ', InputParser.Words(text));
}
