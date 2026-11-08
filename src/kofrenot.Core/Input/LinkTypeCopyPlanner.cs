using Kofrenot.Core.Graph;

namespace Kofrenot.Core.Input;

/// <summary>What copying a link type's definition into the open vault does.</summary>
public enum LinkTypeCopyOutcome
{
    /// <summary>The open vault does not have it (or had it, removed), or a built-in one without its qualifiers: it is added.</summary>
    Adds,

    /// <summary>The open vault has it, defined otherwise: its definition is replaced.</summary>
    Updates,

    /// <summary>The open vault has it as it is, or there is no definition to copy (a built-in type with aliases only).</summary>
    Same,

    /// <summary>One of its names is a phrase of another link type of the open vault (<see cref="LinkTypeCopy.TakenBy"/>): rename one first.</summary>
    NameTaken,
}

/// <summary>
/// A link type of another vault to copy, with its aliases: what copying does, its definition to write
/// (none when <see cref="Outcome"/> writes nothing), the aliases to add, and those left out because
/// another link type here reads their phrase (<see cref="AliasesTaken"/>).
/// </summary>
public sealed record LinkTypeCopy(
    LinkType Type,
    bool IsBuiltIn,
    LinkTypeCopyOutcome Outcome,
    string? Taken,
    LinkVerb? TakenBy,
    LinkTypeDefined? Definition,
    IReadOnlyList<AliasAdded> Aliases,
    IReadOnlyList<string> AliasesTaken)
{
    /// <summary>What copying writes: its definition, then its aliases.</summary>
    public IReadOnlyList<Change> Changes => Definition is { } definition ? [definition, .. Aliases] : [.. Aliases];

    /// <summary>What copying it does, as shown beside it.</summary>
    public string Status => Outcome == LinkTypeCopyOutcome.NameTaken
        ? $"“{Taken}” is a phrase of “{TakenBy!.Name}” here already: rename one first"
        : Described + LeftOut;

    private string Described
    {
        get
        {
            var definition = (Outcome, IsBuiltIn) switch
            {
                (LinkTypeCopyOutcome.Adds, false) => "Adds it",
                (LinkTypeCopyOutcome.Updates, false) => "Updates it",
                (LinkTypeCopyOutcome.Adds, true) => "Adds its qualifiers",
                (LinkTypeCopyOutcome.Updates, true) => "Updates its qualifiers",
                _ => null,
            };
            var aliases = Aliases.Count switch
            {
                0 => null,
                1 => "1 alias",
                _ => $"{Aliases.Count} aliases",
            };
            return (definition, aliases) switch
            {
                (null, null) => "Already the same here",
                (null, _) => $"Adds {aliases}",
                (_, null) => definition,
                _ => $"{definition}, with {aliases}",
            };
        }
    }

    private string LeftOut => AliasesTaken.Count == 0
        ? string.Empty
        : $"; {string.Join(", ", AliasesTaken.Select(phrase => $"“{phrase}”"))} left out: another link type here reads {(AliasesTaken.Count == 1 ? "it" : "them")}";
}

/// <summary>
/// Turns the link types of another vault into the changes that copy them into the open one: the
/// user's (names and qualifiers), the built-in ones' qualifiers, and the aliases of both. A type keeps
/// its id: copied again, it is updated rather than added twice, and its links here keep reading it.
/// An alias already here, on the same way of the same type, is not added twice. Links are never copied.
/// </summary>
public sealed class LinkTypeCopyPlanner(KnowledgeGraph graph, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>A line per link type of <paramref name="source"/> that has a definition or aliases, the built-in ones first.</summary>
    public IReadOnlyList<LinkTypeCopy> Plan(KnowledgeGraph source)
    {
        var now = _clock.GetUtcNow();
        var ticks = 0;
        DateTimeOffset Next() => now.AddTicks(ticks++);
        var copies = new List<LinkTypeCopy>();
        foreach (var phrases in source.Aliases.ByType())
        {
            var defined = source.LinkTypes.Defined(phrases.Type.Id);
            var aliases = (phrases.Reverse is { } reverse ? [.. phrases.Forward.Aliases, .. reverse.Aliases] : phrases.Forward.Aliases)
                .Where(alias => alias.IsUsed)
                .ToList();
            if (defined is not null || aliases.Count > 0)
            {
                copies.Add(Plan(phrases.Type, isBuiltIn: source.LinkTypes.Find(phrases.Type.Id) is null, defined, aliases, Next));
            }
        }

        return copies;
    }

    private LinkTypeCopy Plan(LinkType type, bool isBuiltIn, CustomLinkType? defined, IReadOnlyList<LinkAlias> aliases, Func<DateTimeOffset> next)
    {
        if (!isBuiltIn && TakenPhrase(type) is { } taken)
        {
            return new LinkTypeCopy(type, isBuiltIn, LinkTypeCopyOutcome.NameTaken, taken.Phrase, taken.By, Definition: null, Aliases: [], AliasesTaken: []);
        }

        var (outcome, definition) = defined is null ? (LinkTypeCopyOutcome.Same, null) : Definition(defined, next);
        var added = new List<AliasAdded>();
        var left = new List<string>();
        foreach (var alias in aliases)
        {
            var key = NameKey.Of(alias.Phrase);
            switch (graph.Types.Verbs.FirstOrDefault(verb => verb.Phrases.Any(phrase => NameKey.Of(phrase) == key)))
            {
                case null:
                    added.Add(new AliasAdded(Guid.NewGuid(), next(), type.Id, alias.Verb.IsReverse, alias.Phrase));
                    break;
                case var reader when reader.Type.Id != type.Id || reader.IsReverse != alias.Verb.IsReverse:
                    left.Add(alias.Phrase);
                    break;
            }
        }

        return new LinkTypeCopy(type, isBuiltIn, outcome, Taken: null, TakenBy: null, definition, added, left);
    }

    // A name of a type of the user's that another link type here reads already: its own phrases here aside.
    private (string Phrase, LinkVerb By)? TakenPhrase(LinkType type)
    {
        var others = graph.Types.Verbs.Where(verb => verb.Type.Id != type.Id).ToList();
        foreach (var verb in type.Verbs)
        {
            var key = NameKey.Of(verb.Name);
            if (others.FirstOrDefault(other => other.Phrases.Any(phrase => NameKey.Of(phrase) == key)) is { } by)
            {
                return (verb.Name, by);
            }
        }

        return null;
    }

    private (LinkTypeCopyOutcome, LinkTypeDefined?) Definition(CustomLinkType copied, Func<DateTimeOffset> next)
    {
        var shown = copied.Names[^1];
        var here = graph.LinkTypes.Defined(copied.Type.Id);
        if (here is { IsConflict: false } && here.Names[^1] is var current
            && (current.Name, current.ReverseName) == (shown.Name, shown.ReverseName) && current.Fields.SequenceEqual(shown.Fields))
        {
            return (LinkTypeCopyOutcome.Same, null);
        }

        var change = new LinkTypeDefined(
            Guid.NewGuid(),
            next(),
            copied.Type.Id,
            shown.Name,
            shown.ReverseName,
            shown.Fields,
            here?.Names.Select(definition => definition.Id).ToList() ?? []);
        return (here is null ? LinkTypeCopyOutcome.Adds : LinkTypeCopyOutcome.Updates, change);
    }
}
